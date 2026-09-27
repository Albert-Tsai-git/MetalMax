# -*- coding: utf-8 -*-
"""
Claude 独立工作副本 → 主目录的集成：
  1. 工作副本必须已提交干净；
  2. 变基到最新 main；
  3. 离线编译检查通过；
  4. 主目录快进合并 claude/dev（只含 Claude 的提交，不影响 Codex 未提交的文件）并推送。
用法（在工作副本中）：python tools/integrate.py
"""
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def git(*args, cwd=HERE, check=True) -> str:
    r = subprocess.run(["git", *args], cwd=cwd, capture_output=True, text=True, encoding="utf-8")
    if check and r.returncode != 0:
        sys.exit(f"[integrate] git {' '.join(args)} 失败：\n{r.stdout}{r.stderr}")
    return r.stdout.strip()


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")
    main_dir = git("worktree", "list", "--porcelain").splitlines()[0].split(" ", 1)[1]
    if os.path.normcase(os.path.abspath(main_dir)) == os.path.normcase(HERE):
        sys.exit("[integrate] 请在 Claude 的独立工作副本中运行")
    if git("status", "--porcelain"):
        sys.exit("[integrate] 工作副本有未提交改动，先提交")
    git("rebase", "main")
    r = subprocess.run([sys.executable, os.path.join(HERE, "tools", "compile_check.py")], cwd=HERE)
    if r.returncode != 0:
        sys.exit("[integrate] 编译检查未通过，未合并")
    branch = git("rev-parse", "--abbrev-ref", "HEAD")
    git("merge", "--ff-only", branch, cwd=main_dir)
    git("push", "-q", "origin", "main", cwd=main_dir)
    print(f"[integrate] 已合并到 main 并推送：{git('log', '--oneline', '-1', cwd=main_dir)}")


if __name__ == "__main__":
    main()
