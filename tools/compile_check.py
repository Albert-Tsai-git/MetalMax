# -*- coding: utf-8 -*-
"""
离线编译检查：不启动 Unity，用 Unity 自带的 dotnet + Roslyn 按 csproj 的引用编译各程序集。
用于 Unity 被对方占用时快速发现编译错误。csproj 由 Unity 生成，新增 asmdef 后需 Unity 刷新一次。
用法：python tools/compile_check.py [Unity 安装的 Editor 目录]
"""
import glob
import os
import re
import subprocess
import sys
import tempfile

ROOT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "UNITY")


def main_unity_dir() -> str:
    """csproj 与 Library 只存在于主工作目录；在独立工作副本中运行时，从主目录取引用"""
    if os.path.isdir(os.path.join(ROOT, "Library")):
        return ROOT
    out = subprocess.run(["git", "worktree", "list", "--porcelain"], capture_output=True, text=True, cwd=ROOT).stdout
    main = out.splitlines()[0].split(" ", 1)[1]
    return os.path.join(main, "UNITY")


PROJ = main_unity_dir()
EDITOR = sys.argv[1] if len(sys.argv) > 1 else r"D:\software\unity\Editor\6000.6.3f1\Editor"
DOTNET = os.path.join(EDITOR, "Data", "NetCoreRuntime", "dotnet.exe")
CSC = glob.glob(os.path.join(EDITOR, "Data", "DotNetSdk", "sdk", "*", "Roslyn", "bincore", "csc.dll"))
# 按依赖顺序编译，后者引用前者的输出
ASSEMBLIES = [("Game.Runtime", "Assets/Scripts", ["Assets/Scripts/Editor", "Assets/Scripts/Presentation", "Assets/Scripts/Tests"]),
              ("Game.Presentation", "Assets/Scripts/Presentation", []),
              ("Game.Editor", "Assets/Scripts/Editor", [])]


def refs_and_defines(csproj: str) -> tuple[list[str], list[str]]:
    text = open(csproj, encoding="utf-8").read()
    refs = [r for r in re.findall(r"<HintPath>(.*?)</HintPath>", text)
            if not re.search(r"[\\/]Game\.(Runtime|Presentation|Editor)\.dll$", r)]
    defines = re.search(r"<DefineConstants>(.*?)</DefineConstants>", text)
    return refs, (defines.group(1).split(";") if defines else [])


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    if not os.path.exists(DOTNET) or not CSC:
        print(f"[compile] 找不到 Unity 自带的 dotnet / csc：{EDITOR}")
        return 2
    out_dir = tempfile.mkdtemp(prefix="compile_check_")
    built: list[str] = []
    failed = False
    for name, src, exclude in ASSEMBLIES:
        csproj = os.path.join(PROJ, f"{name}.csproj")
        if not os.path.exists(csproj):
            print(f"[compile] 缺少 {name}.csproj，需先让 Unity 生成一次工程文件")
            return 2
        refs, defines = refs_and_defines(csproj)
        files = [f for f in glob.glob(os.path.join(ROOT, src, "**", "*.cs"), recursive=True)
                 if not any(os.path.normpath(f).startswith(os.path.normpath(os.path.join(ROOT, e))) for e in exclude)]
        dll = os.path.join(out_dir, f"{name}.dll")
        rsp = os.path.join(out_dir, f"{name}.rsp")
        with open(rsp, "w", encoding="utf-8") as f:
            f.write("-nologo\n-target:library\n-nostdlib\n-langversion:latest\n-nowarn:1701,1702\n")
            f.write(f"-out:\"{dll}\"\n-define:{';'.join(defines)}\n")
            for r in refs + built:
                f.write(f"-r:\"{r}\"\n")
            for s in files:
                f.write(f"\"{s}\"\n")
        res = subprocess.run([DOTNET, CSC[0], f"@{rsp}"], capture_output=True, text=True, encoding="utf-8", errors="replace", cwd=PROJ)
        errors = [l for l in res.stdout.splitlines() if "error CS" in l]
        warnings = [l for l in res.stdout.splitlines() if "warning CS" in l]
        print(f"[compile] {name}：{len(files)} 个文件，错误 {len(errors)}，警告 {len(warnings)}")
        for l in errors + warnings:
            print("  " + l.replace(ROOT + os.sep, ""))
        if res.returncode != 0:
            if not errors:
                print((res.stdout + res.stderr)[-2000:])
            failed = True
            break
        built.append(dll)
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
