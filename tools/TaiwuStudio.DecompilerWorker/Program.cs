using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class Program
{
    private const string WorkerVersion = "0.1.0";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly string[] CoreAssemblyNames =
    [
        "Assembly-CSharp.dll",
        "TaiwuModdingLib.dll",
        "0Harmony.dll"
    ];

    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Length == 0 || args.Any(arg => arg.Equals("--help", StringComparison.OrdinalIgnoreCase)
                                                || arg.Equals("-h", StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine(HelpText);
            return 0;
        }

        try
        {
            var options = CliOptions.Parse(args);
            var result = options.Command switch
            {
                "scan" => RunScan(options),
                "find-type" => RunFindType(options),
                "find-member" => RunFindMember(options),
                "decompile-type" => RunDecompileType(options),
                "decompile-member" => RunDecompileMember(options),
                "export-core" => RunExportCore(options),
                "export-dir" => RunExportDir(options),
                _ => WorkerResult.Fail($"Unknown command: {options.Command}")
            };
            WriteJson(result);
            return result.Ok ? 0 : 2;
        }
        catch (Exception ex)
        {
            WriteJson(WorkerResult.Fail(ex.Message, ex.ToString()));
            return 1;
        }
    }

    private const string HelpText = """
TaiwuStudio.DecompilerWorker 0.1.0

Usage:
  TaiwuStudio.DecompilerWorker --command <command> [options]

Commands:
  scan
      Scan core Taiwu assemblies and return type/member metadata as JSON.

  find-type
      Search type names.
      Required: --managed-dir <dir> --query <text>
      Optional: --assembly <name-or-path> --limit <n>

  find-member
      Search member names and signatures.
      Required: --managed-dir <dir> --query <text>
      Optional: --assembly <name-or-path> --limit <n>

  decompile-type
      Decompile one type.
      Required: --managed-dir <dir> --assembly <name-or-path> --type <full.type.Name>

  decompile-member
      Decompile the declaring type for one member and return the member source span.
      Required: --managed-dir <dir> --assembly <name-or-path>
      Required one of:
        --token <metadata-token>
        --type <full.type.Name> --member <memberName>

  export-core
      Export core assemblies to source files plus summary.json.
      Required: --managed-dir <dir> --out-dir <dir>

  export-dir
      Export all .dll/.exe files in a managed directory to source files plus summary.json.
      Required: --managed-dir <dir> --out-dir <dir>

Common Options:
  --managed-dir <dir>     Taiwu Managed directory.
  --assembly <value>      Assembly id, file name, or absolute path.
  --query <text>          Search text.
  --type <fullName>       Full type name.
  --member <name>         Member name.
  --token <token>         Metadata token, for example 0x0600AEB7.
  --out-dir <dir>         Export output directory.
  --limit <n>             Max search results, default 50.
  --help, -h              Show this help.
""";

    private static WorkerResult RunScan(CliOptions options)
    {
        var managedDir = RequireManagedDir(options);
        var assemblies = DiscoverCoreAssemblies(managedDir)
            .Select(path => ScanAssembly(managedDir, path, includeMembers: true))
            .ToList();

        return WorkerResult.Success(new ScanPayload
        {
            ManagedDir = managedDir.FullName,
            Assemblies = assemblies,
            DecompilerVersion = WorkerVersion,
            GeneratedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
    }

    private static WorkerResult RunFindType(CliOptions options)
    {
        var managedDir = RequireManagedDir(options);
        var query = Require(options.Query ?? options.TypeName, "--query or --type");
        var assemblyPaths = !string.IsNullOrWhiteSpace(options.Assembly)
            ? [ResolveAssembly(managedDir, options.Assembly)]
            : DiscoverAssemblies(managedDir);

        var matches = new List<TypeSearchResult>();
        var diagnostics = new List<WorkerDiagnostic>();
        foreach (var assemblyPath in assemblyPaths)
        {
            Mono.Cecil.AssemblyDefinition assembly;
            try
            {
                assembly = ReadAssembly(managedDir, assemblyPath);
            }
            catch (BadImageFormatException ex)
            {
                diagnostics.Add(new WorkerDiagnostic("warning", $"Skipped non-.NET assembly: {assemblyPath.Name}", ex.Message));
                continue;
            }

            using (assembly)
            {
            foreach (var type in assembly.MainModule.Types.SelectMany(FlattenTypes))
            {
                if (type.FullName == "<Module>")
                    continue;

                var fullName = NormalizeTypeName(type.FullName);
                if (!ContainsOrdinalIgnoreCase(fullName, query) && !ContainsOrdinalIgnoreCase(type.Name, query))
                    continue;

                matches.Add(new TypeSearchResult
                {
                    Assembly = AssemblyId(assemblyPath),
                    AssemblyFileName = assemblyPath.Name,
                    Namespace = type.Namespace ?? "",
                    Name = type.Name,
                    FullName = fullName,
                    Token = Token(type.MetadataToken)
                });
            }
            }
        }

        return WorkerResult.Success(new SearchPayload
        {
            Query = query,
            Results = matches
                .OrderBy(match => match.Assembly, StringComparer.OrdinalIgnoreCase)
                .ThenBy(match => match.FullName, StringComparer.OrdinalIgnoreCase)
                .Take(options.Limit)
                .ToList(),
            TotalMatches = matches.Count,
            Limit = options.Limit,
            DecompilerVersion = WorkerVersion,
            GeneratedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        }, diagnostics);
    }

    private static WorkerResult RunFindMember(CliOptions options)
    {
        var managedDir = RequireManagedDir(options);
        var query = Require(options.Query ?? options.MemberName, "--query or --member");
        var assemblyPaths = !string.IsNullOrWhiteSpace(options.Assembly)
            ? [ResolveAssembly(managedDir, options.Assembly)]
            : DiscoverAssemblies(managedDir);

        var matches = new List<MemberSearchResult>();
        var diagnostics = new List<WorkerDiagnostic>();
        foreach (var assemblyPath in assemblyPaths)
        {
            Mono.Cecil.AssemblyDefinition assembly;
            try
            {
                assembly = ReadAssembly(managedDir, assemblyPath);
            }
            catch (BadImageFormatException ex)
            {
                diagnostics.Add(new WorkerDiagnostic("warning", $"Skipped non-.NET assembly: {assemblyPath.Name}", ex.Message));
                continue;
            }

            using (assembly)
            {
            foreach (var type in assembly.MainModule.Types.SelectMany(FlattenTypes))
            {
                if (type.FullName == "<Module>")
                    continue;

                var fullName = NormalizeTypeName(type.FullName);
                var members = type.Methods.Cast<IMemberDefinition>()
                    .Concat(type.Fields)
                    .Concat(type.Properties)
                    .Where(member => !string.IsNullOrWhiteSpace(member.Name));

                foreach (var member in members)
                {
                    var signature = member.ToString() ?? member.Name;
                    if (!ContainsOrdinalIgnoreCase(member.Name, query)
                        && !ContainsOrdinalIgnoreCase(signature, query)
                        && !ContainsOrdinalIgnoreCase(fullName, query))
                    {
                        continue;
                    }

                    matches.Add(new MemberSearchResult
                    {
                        Assembly = AssemblyId(assemblyPath),
                        AssemblyFileName = assemblyPath.Name,
                        TypeFullName = fullName,
                        Kind = MemberKind(member),
                        Name = member.Name,
                        Signature = signature,
                        Token = Token(member.MetadataToken)
                    });
                }
            }
            }
        }

        return WorkerResult.Success(new SearchPayload
        {
            Query = query,
            Results = matches
                .OrderBy(match => match.Assembly, StringComparer.OrdinalIgnoreCase)
                .ThenBy(match => match.TypeFullName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(match => match.Name, StringComparer.OrdinalIgnoreCase)
                .Take(options.Limit)
                .ToList(),
            TotalMatches = matches.Count,
            Limit = options.Limit,
            DecompilerVersion = WorkerVersion,
            GeneratedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        }, diagnostics);
    }

    private static WorkerResult RunDecompileType(CliOptions options)
    {
        var managedDir = RequireManagedDir(options);
        var assemblyPath = ResolveAssembly(managedDir, Require(options.Assembly, "--assembly"));
        var typeName = Require(options.TypeName, "--type");
        var source = DecompileType(managedDir, assemblyPath, typeName);

        return WorkerResult.Success(new DecompilePayload
        {
            Assembly = AssemblyId(assemblyPath),
            TypeFullName = typeName,
            Source = source,
            SourceSpan = FindTypeSpan(source, typeName),
            DecompilerVersion = WorkerVersion,
            GeneratedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
    }

    private static WorkerResult RunDecompileMember(CliOptions options)
    {
        var managedDir = RequireManagedDir(options);
        var assemblyPath = ResolveAssembly(managedDir, Require(options.Assembly, "--assembly"));
        var member = ResolveMember(assemblyPath, options.TypeName, options.MemberName, options.Token);
        var source = DecompileType(managedDir, assemblyPath, member.TypeFullName);

        return WorkerResult.Success(new DecompilePayload
        {
            Assembly = AssemblyId(assemblyPath),
            TypeFullName = member.TypeFullName,
            MemberName = member.MemberName,
            Token = member.Token,
            Source = source,
            SourceSpan = FindMemberSpan(source, member.MemberName),
            DecompilerVersion = WorkerVersion,
            GeneratedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
    }

    private static WorkerResult RunExportCore(CliOptions options)
    {
        var managedDir = RequireManagedDir(options);
        var outputDir = new DirectoryInfo(Require(options.OutputDir, "--out-dir"));
        return RunExport(managedDir, outputDir, DiscoverCoreAssemblies(managedDir));
    }

    private static WorkerResult RunExportDir(CliOptions options)
    {
        var managedDir = RequireManagedDir(options);
        var outputDir = new DirectoryInfo(Require(options.OutputDir, "--out-dir"));
        return RunExport(managedDir, outputDir, DiscoverAssemblies(managedDir));
    }

    private static WorkerResult RunExport(DirectoryInfo managedDir, DirectoryInfo outputDir, List<FileInfo> assemblyPaths)
    {
        outputDir.Create();
        var sourcesDir = Directory.CreateDirectory(Path.Combine(outputDir.FullName, "sources"));
        var assemblies = new List<AssemblyScan>();
        var exported = 0;
        var diagnostics = new List<WorkerDiagnostic>();

        foreach (var assemblyPath in assemblyPaths)
        {
            var assembly = ScanAssembly(managedDir, assemblyPath, includeMembers: true);
            assemblies.Add(assembly);
            var decompiler = CreateDecompiler(managedDir, assemblyPath);

            foreach (var type in assembly.Types)
            {
                try
                {
                    var source = DecompileType(decompiler, type.FullName);
                    var sourceLines = SplitSourceLines(source);
                    var relativePath = SourceRelativePath(assembly.Name, type.FullName);
                    var targetPath = Path.Combine(sourcesDir.FullName, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                    File.WriteAllText(targetPath, source, Encoding.UTF8);

                    type.SourcePath = Path.Combine("sources", relativePath).Replace('\\', '/');
                    type.SourceSpan = FindTypeSpan(sourceLines, type.FullName);
                    foreach (var member in type.Members)
                    {
                        member.SourceSpan = FindMemberSpan(sourceLines, member.Name);
                    }
                    exported++;
                }
                catch (Exception ex)
                {
                    diagnostics.Add(new WorkerDiagnostic(
                        "warning",
                        $"Failed to decompile type: {assembly.Name}::{type.FullName}",
                        ex.Message));
                }
            }
        }

        var summary = new ExportPayload
        {
            ManagedDir = managedDir.FullName,
            OutputDir = outputDir.FullName,
            SourcesDir = sourcesDir.FullName,
            Assemblies = assemblies,
            ExportedTypes = exported,
            DecompilerVersion = WorkerVersion,
            GeneratedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        File.WriteAllText(
            Path.Combine(outputDir.FullName, "summary.json"),
            JsonSerializer.Serialize(summary, JsonOptions),
            Encoding.UTF8);

        return WorkerResult.Success(summary, diagnostics);
    }

    private static AssemblyScan ScanAssembly(DirectoryInfo managedDir, FileInfo assemblyPath, bool includeMembers)
    {
        using var assembly = ReadAssembly(managedDir, assemblyPath);
        var types = assembly.MainModule.Types
            .SelectMany(FlattenTypes)
            .Where(type => type.FullName != "<Module>")
            .OrderBy(type => type.FullName, StringComparer.OrdinalIgnoreCase)
            .Select(type => new TypeScan
            {
                Namespace = type.Namespace ?? "",
                Name = type.Name,
                FullName = NormalizeTypeName(type.FullName),
                Token = Token(type.MetadataToken),
                Members = includeMembers
                    ? type.Methods.Cast<IMemberDefinition>()
                        .Concat(type.Fields)
                        .Concat(type.Properties)
                        .Where(member => !string.IsNullOrWhiteSpace(member.Name))
                        .OrderBy(member => MemberKind(member), StringComparer.OrdinalIgnoreCase)
                        .ThenBy(member => member.Name, StringComparer.OrdinalIgnoreCase)
                        .Select(MemberScan.From)
                        .ToList()
                    : []
            })
            .ToList();

        return new AssemblyScan
        {
            Name = assembly.Name.Name,
            Version = assembly.Name.Version?.ToString() ?? "",
            RuntimeVersion = assembly.MainModule.RuntimeVersion,
            Path = assemblyPath.FullName,
            FileName = assemblyPath.Name,
            Fingerprint = FileFingerprint(assemblyPath),
            Types = types
        };
    }

    private static IEnumerable<Mono.Cecil.TypeDefinition> FlattenTypes(Mono.Cecil.TypeDefinition type)
    {
        yield return type;
        foreach (var nested in type.NestedTypes)
        {
            foreach (var child in FlattenTypes(nested))
            {
                yield return child;
            }
        }
    }

    private static string DecompileType(DirectoryInfo managedDir, FileInfo assemblyPath, string typeName)
    {
        var decompiler = CreateDecompiler(managedDir, assemblyPath);
        return DecompileType(decompiler, typeName);
    }

    private static CSharpDecompiler CreateDecompiler(DirectoryInfo managedDir, FileInfo assemblyPath)
    {
        var resolver = new UniversalAssemblyResolver(assemblyPath.FullName, false, null);
        resolver.AddSearchDirectory(managedDir.FullName);
        var settings = new DecompilerSettings(LanguageVersion.Latest)
        {
            ThrowOnAssemblyResolveErrors = false,
            RemoveDeadCode = true,
            UsingDeclarations = true
        };
        return new CSharpDecompiler(assemblyPath.FullName, resolver, settings);
    }

    private static string DecompileType(CSharpDecompiler decompiler, string typeName)
    {
        return decompiler.DecompileTypeAsString(new FullTypeName(typeName));
    }

    private static MemberRef ResolveMember(
        FileInfo assemblyPath,
        string? typeName,
        string? memberName,
        string? token)
    {
        using var assembly = ReadAssembly(assemblyPath.Directory!, assemblyPath);
        var allTypes = assembly.MainModule.Types.SelectMany(FlattenTypes).ToList();
        if (!string.IsNullOrWhiteSpace(token))
        {
            var tokenValue = ParseToken(token);
            foreach (var type in allTypes)
            {
                foreach (var member in type.Methods.Cast<IMemberDefinition>().Concat(type.Fields).Concat(type.Properties))
                {
                    if (member.MetadataToken.ToInt32() == tokenValue)
                    {
                        return new MemberRef(
                            NormalizeTypeName(type.FullName),
                            member.Name,
                            Token(member.MetadataToken));
                    }
                }
            }

            throw new InvalidOperationException($"Member token was not found: {token}");
        }

        var requiredType = Require(typeName, "--type or --token");
        var requiredMember = Require(memberName, "--member or --token");
        var matchedType = allTypes.FirstOrDefault(type =>
            string.Equals(NormalizeTypeName(type.FullName), requiredType, StringComparison.Ordinal)
            || string.Equals(type.FullName, requiredType, StringComparison.Ordinal));
        if (matchedType is null)
        {
            throw new InvalidOperationException($"Type was not found: {requiredType}");
        }

        var matchedMember = matchedType.Methods.Cast<IMemberDefinition>()
            .Concat(matchedType.Fields)
            .Concat(matchedType.Properties)
            .FirstOrDefault(member => string.Equals(member.Name, requiredMember, StringComparison.Ordinal));
        if (matchedMember is null)
        {
            throw new InvalidOperationException($"Member was not found: {requiredType}.{requiredMember}");
        }

        return new MemberRef(
            NormalizeTypeName(matchedType.FullName),
            matchedMember.Name,
            Token(matchedMember.MetadataToken));
    }

    private static Mono.Cecil.AssemblyDefinition ReadAssembly(DirectoryInfo managedDir, FileInfo assemblyPath)
    {
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(managedDir.FullName);
        return Mono.Cecil.AssemblyDefinition.ReadAssembly(assemblyPath.FullName, new ReaderParameters
        {
            AssemblyResolver = resolver,
            ReadSymbols = false,
            ReadingMode = ReadingMode.Deferred
        });
    }

    private static List<FileInfo> DiscoverAssemblies(DirectoryInfo managedDir)
    {
        var files = managedDir.EnumerateFiles("*.*", SearchOption.TopDirectoryOnly)
            .Where(file => file.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase)
                || file.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (files.Count == 0)
        {
            throw new DirectoryNotFoundException($"No assemblies were found in directory: {managedDir.FullName}");
        }

        return files;
    }

    private static List<FileInfo> DiscoverCoreAssemblies(DirectoryInfo managedDir)
    {
        var files = DiscoverAssemblies(managedDir)
            .Where(file => CoreAssemblyNames.Contains(file.Name, StringComparer.OrdinalIgnoreCase)
                || file.Name.Equals("GameData.exe", StringComparison.OrdinalIgnoreCase)
                || file.Name.StartsWith("GameData.", StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (files.Count == 0)
        {
            throw new DirectoryNotFoundException($"No core assemblies were found in Managed directory: {managedDir.FullName}");
        }

        return files;
    }

    private static FileInfo ResolveAssembly(DirectoryInfo managedDir, string assembly)
    {
        var direct = new FileInfo(assembly);
        if (direct.Exists)
        {
            return direct;
        }

        foreach (var candidate in CandidateAssemblyNames(assembly))
        {
            var path = new FileInfo(Path.Combine(managedDir.FullName, candidate));
            if (path.Exists)
            {
                return path;
            }
        }

        throw new FileNotFoundException($"Assembly was not found: {assembly}", Path.Combine(managedDir.FullName, assembly));
    }

    private static IEnumerable<string> CandidateAssemblyNames(string assembly)
    {
        if (assembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            || assembly.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            yield return assembly;
            yield break;
        }

        yield return assembly + ".dll";
        yield return assembly + ".exe";
    }

    private static DirectoryInfo RequireManagedDir(CliOptions options)
    {
        var managedDir = new DirectoryInfo(Require(options.ManagedDir, "--managed-dir"));
        if (!managedDir.Exists)
        {
            throw new DirectoryNotFoundException($"Managed directory does not exist: {managedDir.FullName}");
        }

        return managedDir;
    }

    private static SourceSpan? FindTypeSpan(string source, string typeName)
    {
        return FindTypeSpan(SplitSourceLines(source), typeName);
    }

    private static SourceSpan? FindTypeSpan(IReadOnlyList<string> lines, string typeName)
    {
        var shortName = typeName.Split('.').Last().Split('+').Last();
        return FindLineSpan(lines, $" {shortName}");
    }

    private static SourceSpan? FindMemberSpan(string source, string memberName)
    {
        return FindMemberSpan(SplitSourceLines(source), memberName);
    }

    private static SourceSpan? FindMemberSpan(IReadOnlyList<string> lines, string memberName)
    {
        if (memberName is ".ctor" or ".cctor")
        {
            return FindLineSpan(lines, memberName == ".cctor" ? "static " : "public ");
        }

        return FindLineSpan(lines, memberName);
    }

    private static IReadOnlyList<string> SplitSourceLines(string source)
    {
        return source.Replace("\r\n", "\n").Split('\n');
    }

    private static SourceSpan? FindLineSpan(IReadOnlyList<string> lines, string needle)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Contains(needle, StringComparison.Ordinal))
            {
                return new SourceSpan(i + 1, Math.Min(lines.Count, i + 80));
            }
        }

        return null;
    }

    private static int ParseToken(string token)
    {
        var trimmed = token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? token[2..] : token;
        return Convert.ToInt32(trimmed, 16);
    }

    private static string SourceRelativePath(string assemblyName, string fullTypeName)
    {
        var safeAssembly = SanitizePathSegment(assemblyName);
        var typePath = fullTypeName.Replace('+', '.').Split('.').Select(SanitizePathSegment);
        return Path.Combine([safeAssembly, .. typePath]) + ".cs";
    }

    private static string SanitizePathSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(invalid.Contains(ch) ? '_' : ch);
        }

        return builder.Length == 0 ? "_" : builder.ToString();
    }

    private static string NormalizeTypeName(string value) => value.Replace('/', '+');

    private static string FileFingerprint(FileInfo file)
    {
        using var stream = File.OpenRead(file.FullName);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string AssemblyId(FileInfo file) => Path.GetFileNameWithoutExtension(file.Name);

    private static bool ContainsOrdinalIgnoreCase(string value, string query)
    {
        return value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static string MemberKind(IMemberDefinition member) => member switch
    {
        MethodDefinition => "method",
        FieldDefinition => "field",
        PropertyDefinition => "property",
        _ => "member"
    };

    private static string Token(MetadataToken token) => $"0x{token.ToInt32():X8}";

    private static string Require(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Missing argument {name}");
        }

        return value;
    }

    private static void WriteJson(WorkerResult result)
    {
        Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
    }
}

internal sealed record CliOptions
{
    public string Command { get; init; } = "";
    public string? ManagedDir { get; init; }
    public string? Assembly { get; init; }
    public string? TypeName { get; init; }
    public string? MemberName { get; init; }
    public string? Token { get; init; }
    public string? OutputDir { get; init; }
    public string? Query { get; init; }
    public int Limit { get; init; } = 50;

    public static CliOptions Parse(string[] args)
    {
        var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = arg[2..];
            var value = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[++i]
                : "true";
            map[key] = value;
        }

        return new CliOptions
        {
            Command = map.GetValueOrDefault("command") ?? map.GetValueOrDefault("cmd") ?? "",
            ManagedDir = map.GetValueOrDefault("managed-dir"),
            Assembly = map.GetValueOrDefault("assembly"),
            TypeName = map.GetValueOrDefault("type"),
            MemberName = map.GetValueOrDefault("member"),
            Token = map.GetValueOrDefault("token"),
            OutputDir = map.GetValueOrDefault("out-dir"),
            Query = map.GetValueOrDefault("query"),
            Limit = int.TryParse(map.GetValueOrDefault("limit"), out var limit) && limit > 0 ? limit : 50
        };
    }
}

internal sealed record WorkerResult(bool Ok, object? Payload, List<WorkerDiagnostic> Diagnostics)
{
    public static WorkerResult Success(object payload, IEnumerable<WorkerDiagnostic>? diagnostics = null)
    {
        return new WorkerResult(true, payload, diagnostics?.ToList() ?? []);
    }

    public static WorkerResult Fail(string message, string? detail = null)
    {
        return new WorkerResult(false, null, [new WorkerDiagnostic("error", message, detail)]);
    }
}

internal sealed record WorkerDiagnostic(string Severity, string Message, string? Detail = null);
internal sealed record SourceSpan(int StartLine, int EndLine);
internal sealed record MemberRef(string TypeFullName, string MemberName, string Token);

internal sealed record SearchPayload
{
    public string Query { get; init; } = "";
    public object Results { get; init; } = Array.Empty<object>();
    public int TotalMatches { get; init; }
    public int Limit { get; init; }
    public string DecompilerVersion { get; init; } = "";
    public long GeneratedAtUnixMs { get; init; }
}

internal sealed record TypeSearchResult
{
    public string Assembly { get; init; } = "";
    public string AssemblyFileName { get; init; } = "";
    public string Namespace { get; init; } = "";
    public string Name { get; init; } = "";
    public string FullName { get; init; } = "";
    public string Token { get; init; } = "";
}

internal sealed record MemberSearchResult
{
    public string Assembly { get; init; } = "";
    public string AssemblyFileName { get; init; } = "";
    public string TypeFullName { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Name { get; init; } = "";
    public string Signature { get; init; } = "";
    public string Token { get; init; } = "";
}

internal sealed record ScanPayload
{
    public string ManagedDir { get; init; } = "";
    public List<AssemblyScan> Assemblies { get; init; } = [];
    public string DecompilerVersion { get; init; } = "";
    public long GeneratedAtUnixMs { get; init; }
}

internal sealed record ExportPayload
{
    public string ManagedDir { get; init; } = "";
    public string OutputDir { get; init; } = "";
    public string SourcesDir { get; init; } = "";
    public List<AssemblyScan> Assemblies { get; init; } = [];
    public int ExportedTypes { get; init; }
    public string DecompilerVersion { get; init; } = "";
    public long GeneratedAtUnixMs { get; init; }
}

internal sealed record DecompilePayload
{
    public string Assembly { get; init; } = "";
    public string TypeFullName { get; init; } = "";
    public string? MemberName { get; init; }
    public string? Token { get; init; }
    public string Source { get; init; } = "";
    public SourceSpan? SourceSpan { get; init; }
    public string DecompilerVersion { get; init; } = "";
    public long GeneratedAtUnixMs { get; init; }
}

internal sealed record AssemblyScan
{
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
    public string RuntimeVersion { get; init; } = "";
    public string Path { get; init; } = "";
    public string FileName { get; init; } = "";
    public string Fingerprint { get; init; } = "";
    public List<TypeScan> Types { get; init; } = [];
}

internal sealed record TypeScan
{
    public string Namespace { get; init; } = "";
    public string Name { get; init; } = "";
    public string FullName { get; init; } = "";
    public string Token { get; init; } = "";
    public string? SourcePath { get; set; }
    public SourceSpan? SourceSpan { get; set; }
    public List<MemberScan> Members { get; init; } = [];
}

internal sealed record MemberScan
{
    public string Kind { get; init; } = "";
    public string Name { get; init; } = "";
    public string Signature { get; init; } = "";
    public string Token { get; init; } = "";
    public SourceSpan? SourceSpan { get; set; }

    public static MemberScan From(IMemberDefinition member)
    {
        return new MemberScan
        {
            Kind = MemberKind(member),
            Name = member.Name,
            Signature = member.ToString() ?? member.Name,
            Token = FormatToken(member.MetadataToken)
        };
    }

    private static string MemberKind(IMemberDefinition member) => member switch
    {
        MethodDefinition => "method",
        FieldDefinition => "field",
        PropertyDefinition => "property",
        _ => "member"
    };

    private static string FormatToken(MetadataToken token) => $"0x{token.ToInt32():X8}";
}
