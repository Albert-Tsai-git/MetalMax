# -*- coding: utf-8 -*-
"""
路径所有权检查（Claude / Codex）。规则表是路径所有权的唯一来源。
用法：
  python tools/ownership.py who <路径>...        查询路径归属
  python tools/ownership.py check <Claude|Codex>  检查暂存区文件是否越界（供 commit-msg 钩子调用）
"""
import fnmatch
import subprocess
import sys

# 按顺序匹配，先命中者生效；.meta 跟随其资源文件。shared = 双方均可改（改前需在 INTERFACE.md 达成一致）
RULES = [
    # 对接文档与公共文件
    ("docs/INTERFACE.md", "shared"),
    ("docs/COLLABORATION.md", "shared"),
    ("docs/GDD.md", "shared"),
    ("docs/STORY.md", "Codex"),
    ("docs/ART_BIBLE.md", "Codex"),
    ("UNITY/Packages/*", "shared"),
    # 仓库管理
    (".gitignore", "Claude"), (".vsconfig", "Claude"), ("UNITY/.vsconfig", "Claude"), (".gitattributes", "Claude"),
    (".githooks/*", "Claude"), ("tools/*", "Claude"),
    # 美术源文件
    ("ArtSource/*", "Codex"),
    # 数据
    ("UNITY/Data/Text/*", "Codex"),
    ("UNITY/Data/*", "Claude"),
    ("UNITY/Assets/GameData/*", "Claude"),
    ("UNITY/Assets/Resources/Text/*", "Claude"),      # 由导入器生成
    ("UNITY/Assets/Resources/Visuals/*", "Codex"),
    ("UNITY/Assets/Resources/Icons/*", "Codex"),
    # 脚本
    ("UNITY/Assets/Scripts/Presentation/*", "Codex"),
    ("UNITY/Assets/Scripts/*", "Claude"),
    # 场景
    ("UNITY/Assets/Scenes/Logic/*", "Claude"),
    ("UNITY/Assets/Scenes/Art/*", "Codex"),
    # 美术 / 界面 / 动效 / 音频资源
    *[(f"UNITY/Assets/{d}/*", "Codex") for d in
      ("Art", "Models", "Materials", "Prefabs", "Animations", "VFX", "Audio", "UI", "Fonts", "Settings")],
    ("UNITY/Assets/InputSystem_Actions.inputactions", "Claude"),
    # 工程设置
    *[(f"UNITY/ProjectSettings/{f}", "Claude") for f in
      ("TagManager.asset", "DynamicsManager.asset", "Physics2DSettings.asset",
       "EditorBuildSettings.asset", "InputManager.asset", "TimeManager.asset")],
    *[(f"UNITY/ProjectSettings/{f}", "Codex") for f in
      ("GraphicsSettings.asset", "QualitySettings.asset", "URPProjectSettings.asset",
       "ShaderGraphSettings.asset", "AudioManager.asset")],
    ("UNITY/ProjectSettings/*", "shared"),
    # 目录自身的 .meta（如 Assets/Models.meta）
    *[(f"UNITY/Assets/{d}.meta", "Codex") for d in
      ("Art", "Models", "Materials", "Prefabs", "Animations", "VFX", "Audio", "UI", "Fonts", "Settings")],
    ("UNITY/Assets/Scripts/Presentation.meta", "Codex"),
    ("UNITY/Assets/Scenes/Art.meta", "Codex"),
    ("UNITY/Assets/Resources/Visuals.meta", "Codex"),
    ("UNITY/Assets/Resources/Icons.meta", "Codex"),
    ("UNITY/Assets/*.meta", "Claude"),
    ("UNITY/Assets/Resources/*.meta", "Claude"),
    ("UNITY/Assets/Scenes/*.meta", "Claude"),
]


def owner(path: str) -> str | None:
    path = path.replace("\\", "/")
    for pattern, who in RULES:
        if fnmatch.fnmatch(path, pattern):
            return who
    return None


def staged_files() -> list[str]:
    out = subprocess.run(["git", "diff", "--cached", "--name-only", "-z"],
                         capture_output=True, text=True, encoding="utf-8", check=True).stdout
    return [p for p in out.split("\0") if p]


def check(agent: str) -> int:
    bad = []
    for p in staged_files():
        who = owner(p)
        if who is None:
            bad.append(f"  {p}  （未登记路径：先在 tools/ownership.py 登记归属）")
        elif who not in ("shared", agent):
            bad.append(f"  {p}  （归属 {who}）")
    if bad:
        print(f"[ownership] {agent} 的提交包含越界文件，已拒绝：")
        print("\n".join(bad))
        print("[ownership] 只提交自己的路径：git commit -m \"[%s] ...\" -- <路径>" % agent)
        return 1
    return 0


if __name__ == "__main__":
    if len(sys.argv) >= 3 and sys.argv[1] == "check":
        sys.exit(check(sys.argv[2]))
    if len(sys.argv) >= 3 and sys.argv[1] == "who":
        for p in sys.argv[2:]:
            print(f"{owner(p) or '未登记'}\t{p}")
        sys.exit(0)
    print(__doc__)
    sys.exit(2)
