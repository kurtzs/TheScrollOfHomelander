#!/usr/bin/env python3
import argparse
import json
import os
import sqlite3
import sys
from pathlib import Path


def default_data_dir() -> Path:
    appdata = os.environ.get("APPDATA")
    if appdata:
        return Path(appdata) / "TaiwuStudio" / "Taiwu Studio" / "data"
    return Path.home() / "AppData" / "Roaming" / "TaiwuStudio" / "Taiwu Studio" / "data"


def discover_index(explicit: str | None) -> Path | None:
    candidates = []
    if explicit:
        candidates.append(Path(explicit))
    if os.environ.get("TAIWU_STUDIO_INDEX"):
        candidates.append(Path(os.environ["TAIWU_STUDIO_INDEX"]))
    candidates.append(default_data_dir() / "index.sqlite3")
    for candidate in candidates:
        if candidate.exists():
            return candidate
    return None


def discover_summaries(explicit_cache: str | None) -> list[Path]:
    roots = []
    if explicit_cache:
        roots.append(Path(explicit_cache))
    if os.environ.get("TAIWU_DECOMPILED_CACHE"):
        roots.append(Path(os.environ["TAIWU_DECOMPILED_CACHE"]))
    roots.append(default_data_dir() / ".taiwu-studio" / "decompiled")
    summaries: list[Path] = []
    for root in roots:
        if root.is_file() and root.name == "summary.json":
            summaries.append(root)
        elif root.exists():
            summaries.extend(root.glob("*/summary.json"))
    return sorted(set(summaries), key=lambda path: path.stat().st_mtime, reverse=True)


def rows_from_db(db_path: Path, query: str, params: tuple) -> list[sqlite3.Row]:
    conn = sqlite3.connect(db_path)
    conn.row_factory = sqlite3.Row
    try:
        return list(conn.execute(query, params))
    except sqlite3.Error:
        return []
    finally:
        conn.close()


def format_type(row: sqlite3.Row) -> str:
    source = row["source_path"] if "source_path" in row.keys() and row["source_path"] else "-"
    return f'{row["assembly_name"]}::{row["type_full_name"]} | source={source}'


def format_member(row: sqlite3.Row) -> str:
    source = row["source_path"] if "source_path" in row.keys() and row["source_path"] else "-"
    token = row["token"] if "token" in row.keys() else "-"
    return (
        f'{row["assembly_name"]}::{row["type_full_name"]}.{row["name"]} '
        f'[{row["kind"]}] {token} | {row["signature"]} | source={source}'
    )


def find_type(args: argparse.Namespace) -> int:
    db_path = discover_index(args.index)
    if db_path:
        rows = rows_from_db(
            db_path,
            """
            SELECT decompiled_assemblies.assembly_name,
                   decompiled_types.type_full_name,
                   decompiled_types.source_path
            FROM decompiled_types
            JOIN decompiled_assemblies ON decompiled_assemblies.id = decompiled_types.assembly_id
            WHERE decompiled_types.type_full_name LIKE ?
            ORDER BY decompiled_assemblies.assembly_name, decompiled_types.type_full_name
            LIMIT ?
            """,
            (f"%{args.query}%", args.limit),
        )
        if rows:
            for row in rows:
                print(format_type(row))
            return 0

    for summary_path in discover_summaries(args.cache):
        summary = json.loads(summary_path.read_text(encoding="utf-8-sig"))
        output_dir = Path(summary.get("outputDir", summary_path.parent))
        found = 0
        for assembly in summary.get("assemblies", []):
            for item in assembly.get("types", []):
                full_name = item.get("fullName", "")
                if args.query.lower() not in full_name.lower():
                    continue
                source = item.get("sourcePath")
                path = output_dir / source if source else Path("-")
                print(f'{assembly.get("name")}::{full_name} | source={path}')
                found += 1
                if found >= args.limit:
                    return 0
        if found:
            return 0
    return 1


def find_member(args: argparse.Namespace) -> int:
    db_path = discover_index(args.index)
    if db_path:
        rows = rows_from_db(
            db_path,
            """
            SELECT decompiled_assemblies.assembly_name,
                   decompiled_types.type_full_name,
                   decompiled_members.kind,
                   decompiled_members.name,
                   decompiled_members.signature,
                   decompiled_members.token,
                   decompiled_members.source_path
            FROM decompiled_members
            JOIN decompiled_types ON decompiled_types.id = decompiled_members.type_id
            JOIN decompiled_assemblies ON decompiled_assemblies.id = decompiled_members.assembly_id
            WHERE decompiled_members.name LIKE ?
               OR decompiled_members.signature LIKE ?
               OR decompiled_types.type_full_name LIKE ?
            ORDER BY decompiled_assemblies.assembly_name, decompiled_types.type_full_name, decompiled_members.name
            LIMIT ?
            """,
            (f"%{args.query}%", f"%{args.query}%", f"%{args.query}%", args.limit),
        )
        if rows:
            for row in rows:
                print(format_member(row))
            return 0

    for summary_path in discover_summaries(args.cache):
        summary = json.loads(summary_path.read_text(encoding="utf-8-sig"))
        output_dir = Path(summary.get("outputDir", summary_path.parent))
        found = 0
        for assembly in summary.get("assemblies", []):
            for item in assembly.get("types", []):
                full_name = item.get("fullName", "")
                for member in item.get("members", []):
                    haystack = " ".join([full_name, member.get("name", ""), member.get("signature", "")])
                    if args.query.lower() not in haystack.lower():
                        continue
                    source = item.get("sourcePath")
                    path = output_dir / source if source else Path("-")
                    print(
                        f'{assembly.get("name")}::{full_name}.{member.get("name")} '
                        f'[{member.get("kind")}] {member.get("token")} | '
                        f'{member.get("signature")} | source={path}'
                    )
                    found += 1
                    if found >= args.limit:
                        return 0
        if found:
            return 0
    return 1


def show_source(args: argparse.Namespace) -> int:
    path = Path(args.path)
    if not path.exists():
        db_path = discover_index(args.index)
        if db_path:
            rows = rows_from_db(
                db_path,
                """
                SELECT source_path, start_line, end_line
                FROM decompiled_types
                WHERE type_full_name LIKE ?
                UNION ALL
                SELECT source_path, start_line, end_line
                FROM decompiled_members
                WHERE name LIKE ? OR token = ?
                LIMIT 1
                """,
                (f"%{args.path}%", f"%{args.path}%", args.path),
            )
            if rows:
                path = Path(rows[0]["source_path"])
                if args.start is None and rows[0]["start_line"]:
                    args.start = int(rows[0]["start_line"])
                if args.end is None and rows[0]["end_line"]:
                    args.end = int(rows[0]["end_line"])
    if not path.exists():
        for summary_path in discover_summaries(args.cache):
            summary = json.loads(summary_path.read_text(encoding="utf-8-sig"))
            output_dir = Path(summary.get("outputDir", summary_path.parent))
            for assembly in summary.get("assemblies", []):
                for item in assembly.get("types", []):
                    full_name = item.get("fullName", "")
                    source = item.get("sourcePath")
                    if source and args.path.lower() in full_name.lower():
                        path = output_dir / source
                        span = item.get("sourceSpan") or {}
                        if args.start is None and span.get("startLine"):
                            args.start = int(span["startLine"])
                        if args.end is None and span.get("endLine"):
                            args.end = int(span["endLine"])
                        break
                    for member in item.get("members", []):
                        if args.path.lower() not in (member.get("name", "") + " " + member.get("token", "")).lower():
                            continue
                        if source:
                            path = output_dir / source
                            span = member.get("sourceSpan") or {}
                            if args.start is None and span.get("startLine"):
                                args.start = int(span["startLine"])
                            if args.end is None and span.get("endLine"):
                                args.end = int(span["endLine"])
                            break
                if path.exists():
                    break
            if path.exists():
                break
    if not path.exists():
        print(f"source not found: {args.path}", file=sys.stderr)
        return 1

    lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
    start = max((args.start or 1) - 1, 0)
    end = min(args.end or (start + args.context), len(lines))
    for index in range(start, end):
        print(f"{index + 1:5}: {lines[index]}")
    return 0


def find_patch_targets(args: argparse.Namespace) -> int:
    db_path = discover_index(args.index)
    rows = []
    if db_path:
        rows = rows_from_db(
            db_path,
            """
            SELECT decompiled_assemblies.assembly_name,
                   decompiled_types.type_full_name,
                   decompiled_members.name,
                   decompiled_members.kind,
                   decompiled_members.signature,
                   decompiled_members.token,
                   decompiled_members.source_path
            FROM decompiled_members
            JOIN decompiled_types ON decompiled_types.id = decompiled_members.type_id
            JOIN decompiled_assemblies ON decompiled_assemblies.id = decompiled_members.assembly_id
            WHERE decompiled_members.kind = 'method'
              AND (decompiled_members.name LIKE ?
                   OR decompiled_members.signature LIKE ?
                   OR decompiled_types.type_full_name LIKE ?)
            ORDER BY decompiled_assemblies.assembly_name, decompiled_types.type_full_name, decompiled_members.name
            LIMIT ?
            """,
            (f"%{args.query}%", f"%{args.query}%", f"%{args.query}%", args.limit),
        )
    for row in rows:
        method = row["name"]
        type_name = row["type_full_name"]
        if method in (".ctor", "#ctor", "ctor"):
            target = f"[HarmonyPatch(typeof({type_name}), MethodType.Constructor)]"
        elif method == ".cctor":
            target = f"[HarmonyPatch(typeof({type_name}), MethodType.StaticConstructor)]"
        else:
            target = f'[HarmonyPatch(typeof({type_name}), "{method}")]'
        print(f"{target} | {row['assembly_name']} | {row['token']} | {row['source_path']}")
    if rows:
        return 0

    for summary_path in discover_summaries(args.cache):
        summary = json.loads(summary_path.read_text(encoding="utf-8-sig"))
        output_dir = Path(summary.get("outputDir", summary_path.parent))
        found = 0
        for assembly in summary.get("assemblies", []):
            for item in assembly.get("types", []):
                full_name = item.get("fullName", "")
                source = item.get("sourcePath")
                for member in item.get("members", []):
                    if member.get("kind") != "method":
                        continue
                    haystack = " ".join([full_name, member.get("name", ""), member.get("signature", "")])
                    if args.query.lower() not in haystack.lower():
                        continue
                    method = member.get("name", "")
                    if method in (".ctor", "#ctor", "ctor"):
                        target = f"[HarmonyPatch(typeof({full_name}), MethodType.Constructor)]"
                    elif method == ".cctor":
                        target = f"[HarmonyPatch(typeof({full_name}), MethodType.StaticConstructor)]"
                    else:
                        target = f'[HarmonyPatch(typeof({full_name}), "{method}")]'
                    path = output_dir / source if source else Path("-")
                    print(f"{target} | {assembly.get('name')} | {member.get('token')} | {path}")
                    found += 1
                    if found >= args.limit:
                        return 0
        if found:
            return 0
    return 1


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="Query Taiwu Studio decompiled API cache.")
    parser.add_argument("--index", help="Path to Taiwu Studio index.sqlite3")
    parser.add_argument("--cache", help="Path to decompiled cache root or summary.json")
    sub = parser.add_subparsers(dest="command", required=True)

    p = sub.add_parser("find-type")
    p.add_argument("query")
    p.add_argument("--limit", type=int, default=20)
    p.set_defaults(func=find_type)

    p = sub.add_parser("find-member")
    p.add_argument("query")
    p.add_argument("--limit", type=int, default=20)
    p.set_defaults(func=find_member)

    p = sub.add_parser("show-source")
    p.add_argument("path")
    p.add_argument("--start", type=int)
    p.add_argument("--end", type=int)
    p.add_argument("--context", type=int, default=80)
    p.set_defaults(func=show_source)

    p = sub.add_parser("find-patch-targets")
    p.add_argument("query")
    p.add_argument("--limit", type=int, default=20)
    p.set_defaults(func=find_patch_targets)

    args = parser.parse_args(argv)
    return args.func(args)


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
