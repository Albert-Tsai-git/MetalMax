#!/bin/sh
# 在 Claude 工作副本中导入数据并运行指定幕的战斗/经济模拟（不占主目录 Unity 锁），打印胜率与经济摘要。
# 用法：sh tools/sim_acts.sh 2 3     （参数为幕号：1、2、3）
UNITY_EXE="${UNITY_EXE:-/d/software/unity/Editor/6000.6.3f1/Editor/Unity.exe}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
LOGS="$ROOT/UNITY/Logs"
run() { "$UNITY_EXE" -batchmode -projectPath "$ROOT/UNITY" -executeMethod "Game.EditorTools.$1" -quit -logFile "$LOGS/sim_$1.log" >/dev/null || echo "[sim] $1 失败，见 $LOGS/sim_$1.log"; }
run CsvDataImporter.ImportAll
for act in "$@"; do
  case $act in
    1) run BattleSimulator.Run; run EconomySimulator.Run; b=sim_latest; e=economy_latest ;;
    *) run BattleSimulator.RunAct$act; run EconomySimulator.RunAct$act; b=sim_act${act}_latest; e=economy_act${act}_latest ;;
  esac
  PYTHONIOENCODING=utf-8 python - "$ROOT/docs/balance/$b.md" "$ROOT/docs/balance/$e.md" <<'EOF'
import sys
s = open(sys.argv[1], encoding="utf-8").read()
print(s.splitlines()[0])
for sec in ["## 胜率", "## 失败原因"]:
    i = s.index(sec); j = s.find("\n## ", i + 3)
    print(s[i:j].strip())
e = open(sys.argv[2], encoding="utf-8").read().splitlines()
print("\n".join(l for l in e if l.startswith("|") or l.startswith("- ") or l.startswith("# ")))
EOF
done
