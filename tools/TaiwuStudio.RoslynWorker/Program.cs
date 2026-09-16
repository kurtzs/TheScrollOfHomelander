using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Mono.Cecil;
using DiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true
};

try
{
    if (args.Length == 0 || !string.Equals(args[0], "build", StringComparison.OrdinalIgnoreCase))
    {
        WriteResult(CompileResult.Fail("TSW000", "Usage: TaiwuStudio.RoslynWorker build --project-root <path> --game-libs <path> --assembly-name <name>"), jsonOptions);
        Environment.Exit(2);
    }

    var options = WorkerOptions.Parse(args.Skip(1).ToArray());
    if (string.IsNullOrWhiteSpace(options.ProjectRoot) || string.IsNullOrWhiteSpace(options.GameLibs))
    {
        WriteResult(CompileResult.Fail("TSW000", "--project-root and --game-libs are required."), jsonOptions);
        Environment.Exit(2);
    }

    var result = ModCompiler.Compile(options);
    WriteResult(result, jsonOptions);
    Environment.Exit(result.Success ? 0 : 1);
}
catch (Exception ex)
{
    WriteResult(CompileResult.Fail("TSW000", ex.ToString()), jsonOptions);
    Environment.Exit(1);
}

static void WriteResult(CompileResult result, JsonSerializerOptions options)
{
    Console.OutputEncoding = Encoding.UTF8;
    Console.WriteLine(JsonSerializer.Serialize(result, options));
}

internal sealed class WorkerOptions
{
    public string ProjectRoot { get; private set; } = "";
    public string GameLibs { get; private set; } = "";
    public string AssemblyName { get; private set; } = "";
    public string OutputPluginPath { get; private set; } = "";
    public List<string> SourceDirs { get; } = new();
    public bool DebugBuild { get; private set; } = false;
    public bool AllowUnsafe { get; private set; } = true;

    public static WorkerOptions Parse(string[] args)
    {
        var options = new WorkerOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var key = args[i];
            string? value = i + 1 < args.Length ? args[i + 1] : null;
            switch (key)
            {
                case "--project-root":
                    options.ProjectRoot = value ?? "";
                    i++;
                    break;
                case "--game-libs":
                    options.GameLibs = value ?? "";
                    i++;
                    break;
                case "--assembly-name":
                    options.AssemblyName = value ?? "";
                    i++;
                    break;
                case "--output-plugin-path":
                    options.OutputPluginPath = value ?? "";
                    i++;
                    break;
                case "--source-dir":
                    if (!string.IsNullOrWhiteSpace(value))
                        options.SourceDirs.Add(value);
                    i++;
                    break;
                case "--configuration":
                    options.DebugBuild = string.Equals(value, "Debug", StringComparison.OrdinalIgnoreCase);
                    i++;
                    break;
                case "--allow-unsafe":
                    options.AllowUnsafe = bool.TryParse(value, out var allowUnsafe) ? allowUnsafe : options.AllowUnsafe;
                    i++;
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(options.AssemblyName))
            options.AssemblyName = Path.GetFileName(Path.TrimEndingDirectorySeparator(options.ProjectRoot));

        return options;
    }
}

internal static class ModCompiler
{
    private static readonly string[] FrontendRequiredReferences =
    {
        "Assembly-CSharp.dll",
        "GameData.Shared.dll",
        "TaiwuModdingLib.dll",
        "0Harmony.dll",
        "UnityEngine.CoreModule.dll"
    };

    private static readonly string[] BackendRequiredReferences =
    {
        "GameData.dll",
        "GameData.Shared.dll",
        "TaiwuModdingLib.dll",
        "0Harmony.dll"
    };

    private static readonly string[] NamespaceAssemblyPrefixes =
    {
        "Assembly-CSharp",
        "GameData."
    };

    private const string UsingPrefixCacheVersion = "using-prefix-v2";

    // Kept aligned with TaiwuModForge.Core.Services.Compile.CompileService.
    private static readonly string[] NonCriticalPrefixes =
    {
        "UnityEngine.UI.",
        "UnityEngine.IMGUIModule.",
        "UnityEngine.TextRenderingModule.",
        "Newtonsoft.Json.",
        "netstandard.",
        "GameData."
    };

    public static CompileResult Compile(WorkerOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new CompileResult();
        var projectRoot = Path.GetFullPath(options.ProjectRoot);
        var gameLibs = Path.GetFullPath(options.GameLibs);

        if (!Directory.Exists(projectRoot))
            return CompileResult.Fail("TSW000", $"Project root does not exist: {projectRoot}");

        if (!Directory.Exists(gameLibs))
            return CompileResult.Fail("TSW000", $"GameLibs/Managed directory does not exist: {gameLibs}");

        var sourceFiles = CollectSourceFiles(projectRoot, options.SourceDirs);
        if (sourceFiles.Count == 0)
        {
            result.Diagnostics.Add(new CompileDiagnostic
            {
                Code = "TSW001",
                Severity = "Warning",
                Message = "No C# source files found under Scripts/.",
                FilePath = "Scripts"
            });
        }

        var references = ResolveReferences(gameLibs, projectRoot, result);
        if (result.Diagnostics.Any(d => d.Severity == "Error"))
        {
            result.ElapsedMs = stopwatch.ElapsedMilliseconds;
            return result;
        }

        var parseOptions = new CSharpParseOptions(
            languageVersion: LanguageVersion.Latest,
            preprocessorSymbols: new[] { "TAIWU_MOD", "UNITY_STANDALONE_WIN", "UNITY_64", options.DebugBuild ? "DEBUG" : "RELEASE" });

        var usingPrefix = BuildGameUsingPrefix(gameLibs);
        var syntaxTrees = new List<SyntaxTree>();
        foreach (var file in sourceFiles)
        {
            var code = File.ReadAllText(file, Encoding.UTF8);
            var escapedPath = file.Replace("\\", "\\\\");
            var fullCode = usingPrefix + $"#line 1 \"{escapedPath}\"\n" + code;
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(
                SourceText.From(fullCode, Encoding.UTF8),
                options: parseOptions,
                path: file));
        }

        var compilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            allowUnsafe: options.AllowUnsafe,
            optimizationLevel: options.DebugBuild ? OptimizationLevel.Debug : OptimizationLevel.Release,
            platform: Platform.AnyCpu,
            nullableContextOptions: NullableContextOptions.Enable,
            assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default);

        var compilation = CSharpCompilation.Create(
            SanitizeAssemblyName(options.AssemblyName),
            syntaxTrees,
            references,
            compilationOptions);

        var outputDll = ResolveOutputDll(projectRoot, options);
        var outputDir = Path.GetDirectoryName(outputDll) ?? Path.Combine(projectRoot, "Plugins");
        Directory.CreateDirectory(outputDir);
        var outputPdb = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(outputDll)}.pdb");

        using var dllStream = new MemoryStream();
        using var pdbStream = new MemoryStream();
        var emitResult = compilation.Emit(
            dllStream,
            pdbStream,
            options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));

        foreach (var diagnostic in emitResult.Diagnostics)
        {
            if (diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning or DiagnosticSeverity.Info)
                result.Diagnostics.Add(MapDiagnostic(diagnostic, projectRoot));
        }

        result.Success = emitResult.Success && result.Diagnostics.All(d => d.Severity != "Error");
        if (result.Success)
        {
            File.WriteAllBytes(outputDll, dllStream.ToArray());
            if (pdbStream.Length > 0)
                File.WriteAllBytes(outputPdb, pdbStream.ToArray());
            result.OutputDllPath = outputDll;
            result.OutputPdbPath = outputPdb;
        }

        result.ElapsedMs = stopwatch.ElapsedMilliseconds;
        return result;
    }

    private static List<string> CollectSourceFiles(string projectRoot, IReadOnlyList<string> sourceDirs)
    {
        if (sourceDirs.Count == 0)
        {
            var scriptsDir = Path.Combine(projectRoot, "Scripts");
            return Directory.Exists(scriptsDir)
                ? Directory.GetFiles(scriptsDir, "*.cs", SearchOption.AllDirectories).ToList()
                : new List<string>();
        }

        return sourceDirs
            .Select(dir => Path.GetFullPath(Path.Combine(projectRoot, dir)))
            .Where(Directory.Exists)
            .SelectMany(dir => Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ResolveOutputDll(string projectRoot, WorkerOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.OutputPluginPath))
        {
            var normalized = options.OutputPluginPath.Replace('/', Path.DirectorySeparatorChar);
            if (!normalized.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                normalized += ".dll";
            return Path.GetFullPath(Path.Combine(projectRoot, "Plugins", normalized));
        }

        return Path.Combine(projectRoot, "Plugins", $"{SanitizeAssemblyName(options.AssemblyName)}.dll");
    }

    private static List<MetadataReference> ResolveReferences(
        string gameLibs,
        string projectRoot,
        CompileResult result)
    {
        var allDlls = Directory.GetFiles(gameLibs, "*.dll", SearchOption.TopDirectoryOnly);
        var dllByFileName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dll in allDlls)
        {
            var fileName = Path.GetFileName(dll);
            if (!string.IsNullOrWhiteSpace(fileName))
                dllByFileName.TryAdd(fileName, dll);
        }
        var references = new List<MetadataReference>();
        var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var requiredReferences = File.Exists(Path.Combine(gameLibs, "Assembly-CSharp.dll"))
            ? FrontendRequiredReferences
            : BackendRequiredReferences;

        foreach (var required in requiredReferences)
        {
            if (!dllByFileName.TryGetValue(required, out var path))
            {
                result.Diagnostics.Add(new CompileDiagnostic
                {
                    Code = "TSW002",
                    Severity = "Error",
                    Message = $"Required reference assembly not found: {required}",
                    FilePath = gameLibs
                });
                continue;
            }

            AddReference(path, references, added, result, required);
        }

        foreach (var dll in allDlls)
        {
            if (added.Contains(Path.GetFileName(dll)))
                continue;

            var fileName = Path.GetFileName(dll);
            var isNonCritical = NonCriticalPrefixes.Any(prefix =>
                fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

            if (!isNonCritical && !IsManagedAssembly(dll))
                continue;
            AddReference(dll, references, added, result, fileName, false);
        }

        AddCsprojHintPathReferences(references, added, projectRoot, result);

        return references;
    }

    private static void AddReference(
        string path,
        List<MetadataReference> references,
        HashSet<string> added,
        CompileResult result,
        string label,
        bool required = true)
    {
        try
        {
            references.Add(MetadataReference.CreateFromFile(path));
            added.Add(Path.GetFileName(path));
        }
        catch (Exception ex)
        {
            if (required)
            {
                result.Diagnostics.Add(new CompileDiagnostic
                {
                    Code = "TSW003",
                    Severity = "Error",
                    Message = $"Reference assembly could not be loaded: {label}. {ex.Message}",
                    FilePath = path
                });
            }
        }
    }

    private static void AddCsprojHintPathReferences(
        List<MetadataReference> references,
        HashSet<string> added,
        string projectRoot,
        CompileResult result)
    {
        if (!Directory.Exists(projectRoot))
            return;

        foreach (var csprojPath in Directory.GetFiles(projectRoot, "*.csproj", SearchOption.TopDirectoryOnly))
        {
            var csprojDir = Path.GetDirectoryName(csprojPath) ?? projectRoot;
            var xml = File.ReadAllText(csprojPath, Encoding.UTF8);
            foreach (Match match in Regex.Matches(xml, @"<HintPath>(.*?)</HintPath>", RegexOptions.IgnoreCase))
            {
                var relativePath = match.Groups[1].Value.Trim();
                if (string.IsNullOrWhiteSpace(relativePath))
                    continue;

                try
                {
                    var path = Path.GetFullPath(Path.Combine(csprojDir, relativePath));
                    var fileName = Path.GetFileName(path);
                    if (added.Contains(fileName) || !File.Exists(path) || !IsManagedAssembly(path))
                        continue;
                    AddReference(path, references, added, result, fileName, false);
                }
                catch (Exception ex)
                {
                    result.Diagnostics.Add(new CompileDiagnostic
                    {
                        Code = "TSW004",
                        Severity = "Warning",
                        Message = $"Could not resolve csproj HintPath reference '{relativePath}': {ex.Message}",
                        FilePath = csprojPath
                    });
                }
            }
        }
    }

    private static bool IsManagedAssembly(string dllPath)
    {
        try
        {
            using var _ = AssemblyDefinition.ReadAssembly(dllPath, new ReaderParameters { InMemory = true });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildGameUsingPrefix(string gameLibs)
    {
        var namespaceDlls = Directory.GetFiles(gameLibs, "*.dll", SearchOption.TopDirectoryOnly)
            .Where(IsNamespaceAssemblyCandidate)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var cachePath = TryGetUsingPrefixCachePath(gameLibs, namespaceDlls);
        if (cachePath is not null)
        {
            try
            {
                if (File.Exists(cachePath))
                    return File.ReadAllText(cachePath, Encoding.UTF8);
            }
            catch
            {
                // Cache is a speed hint only.
            }
        }

        var namespaces = new HashSet<string>();
        foreach (var dll in namespaceDlls)
        {
            try
            {
                using var assembly = AssemblyDefinition.ReadAssembly(
                    dll,
                    new ReaderParameters
                    {
                        InMemory = true,
                        ReadingMode = ReadingMode.Deferred
                    });
                CollectNamespaces(assembly.MainModule.Types, namespaces);
            }
            catch
            {
                // Namespace auto-import is a convenience. Reference loading still decides success.
            }
        }

        var usingPrefix = string.Concat(namespaces.OrderBy(ns => ns).Select(ns => $"using {ns};\n"));
        if (cachePath is not null)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
                File.WriteAllText(cachePath, usingPrefix, Encoding.UTF8);
            }
            catch
            {
                // Compilation should not depend on cache writability.
            }
        }

        return usingPrefix;
    }

    private static bool IsNamespaceAssemblyCandidate(string dll)
    {
        var name = Path.GetFileNameWithoutExtension(dll);
        return NamespaceAssemblyPrefixes.Any(prefix =>
            name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static string? TryGetUsingPrefixCachePath(string gameLibs, IReadOnlyList<string> namespaceDlls)
    {
        try
        {
            var seed = new StringBuilder();
            seed.AppendLine(UsingPrefixCacheVersion);
            seed.AppendLine(Path.GetFullPath(gameLibs));
            foreach (var dll in namespaceDlls)
            {
                var file = new FileInfo(dll);
                seed.Append(file.Name)
                    .Append('|')
                    .Append(file.Length)
                    .Append('|')
                    .AppendLine(file.LastWriteTimeUtc.Ticks.ToString());
            }

            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed.ToString())))
                .ToLowerInvariant();
            return Path.Combine(
                Path.GetTempPath(),
                "TaiwuStudio",
                "RoslynWorker",
                "using-prefix",
                $"{hash}.cs");
        }
        catch
        {
            return null;
        }
    }

    private static void CollectNamespaces(IEnumerable<TypeDefinition> types, HashSet<string> namespaces)
    {
        foreach (var type in types)
        {
            if (!string.IsNullOrWhiteSpace(type.Namespace))
                namespaces.Add(type.Namespace);
            if (type.HasNestedTypes)
                CollectNamespaces(type.NestedTypes, namespaces);
        }
    }

    private static CompileDiagnostic MapDiagnostic(Microsoft.CodeAnalysis.Diagnostic diagnostic, string projectRoot)
    {
        var severity = diagnostic.Severity switch
        {
            DiagnosticSeverity.Error => "Error",
            DiagnosticSeverity.Warning => "Warning",
            _ => "Info"
        };

        var filePath = diagnostic.Location.SourceTree?.FilePath ?? "";
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            try { filePath = Path.GetRelativePath(projectRoot, filePath); }
            catch { /* keep original */ }
        }

        var span = diagnostic.Location.GetMappedLineSpan();
        return new CompileDiagnostic
        {
            Code = diagnostic.Id,
            Severity = severity,
            Message = diagnostic.GetMessage(),
            FilePath = string.IsNullOrWhiteSpace(span.Path) ? filePath : span.Path,
            Line = span.IsValid ? span.StartLinePosition.Line + 1 : 0,
            Column = span.IsValid ? span.StartLinePosition.Character + 1 : 0
        };
    }

    private static string SanitizeAssemblyName(string name)
    {
        var chars = name.Select(ch => char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_').ToArray();
        var value = new string(chars);
        return string.IsNullOrWhiteSpace(value) ? "TaiwuStudioMod" : value;
    }
}

internal sealed class CompileResult
{
    public bool Success { get; set; }
    public string OutputDllPath { get; set; } = "";
    public string OutputPdbPath { get; set; } = "";
    public long ElapsedMs { get; set; }
    public List<CompileDiagnostic> Diagnostics { get; set; } = new();

    public static CompileResult Fail(string code, string message) => new()
    {
        Success = false,
        Diagnostics =
        {
            new CompileDiagnostic
            {
                Code = code,
                Severity = "Error",
                Message = message
            }
        }
    };
}

internal sealed class CompileDiagnostic
{
    public string Code { get; set; } = "";
    public string Severity { get; set; } = "Info";
    public string Message { get; set; } = "";
    public string FilePath { get; set; } = "";
    public int Line { get; set; }
    public int Column { get; set; }
}
