#!/bin/sh
# 在主目录用 Unity 导入数据并运行批量战斗模拟，打印关键表格。需先持有 unity 锁。
# 用法：sh tools/sim.sh [Unity.exe 路径]
UNITY_EXE="${1:-/d/software/unity/Editor/6000.6.3f1/Editor/Unity.exe}"
MAIN=$(git worktree list --porcelain | head -1 | cut -d' ' -f2)
"$UNITY_EXE" -batchmode -projectPath "$MAIN/UNITY" -executeMethod Game.EditorTools.CsvDataImporter.ImportAll -quit -logFile "$MAIN/UNITY/Logs/sim_import.log" >/dev/null
"$UNITY_EXE" -batchmode -projectPath "$MAIN/UNITY" -executeMethod Game.EditorTools.BattleSimulator.Run -quit -logFile "$MAIN/UNITY/Logs/sim_run.log" >/dev/null
"$UNITY_EXE" -batchmode -projectPath "$MAIN/UNITY" -executeMethod Game.EditorTools.EconomySimulator.Run -quit -logFile "$MAIN/UNITY/Logs/sim_econ.log" >/dev/null
"$UNITY_EXE" -batchmode -projectPath "$MAIN/UNITY" -executeMethod Game.EditorTools.BattleSimulator.RunAct2 -quit -logFile "$MAIN/UNITY/Logs/sim_run2.log" >/dev/null
"$UNITY_EXE" -batchmode -projectPath "$MAIN/UNITY" -executeMethod Game.EditorTools.EconomySimulator.RunAct2 -quit -logFile "$MAIN/UNITY/Logs/sim_econ2.log" >/dev/null
"$UNITY_EXE" -batchmode -projectPath "$MAIN/UNITY" -executeMethod Game.EditorTools.BattleSimulator.RunAct3 -quit -logFile "$MAIN/UNITY/Logs/sim_run3.log" >/dev/null
"$UNITY_EXE" -batchmode -projectPath "$MAIN/UNITY" -executeMethod Game.EditorTools.EconomySimulator.RunAct3 -quit -logFile "$MAIN/UNITY/Logs/sim_econ3.log" >/dev/null
grep -hE "error CS|Exception" "$MAIN/UNITY/Logs/sim_import.log" "$MAIN/UNITY/Logs/sim_run.log" "$MAIN/UNITY/Logs/sim_econ.log" "$MAIN/UNITY/Logs/sim_run2.log" "$MAIN/UNITY/Logs/sim_econ2.log" "$MAIN/UNITY/Logs/sim_run3.log" "$MAIN/UNITY/Logs/sim_econ3.log" | head -5
PYTHONIOENCODING=utf-8 python -c "import sys;print(open(sys.argv[1],encoding='utf-8').read())" "$MAIN/docs/balance/economy_latest.md"
PYTHONIOENCODING=utf-8 python -c "import sys;print(open(sys.argv[1],encoding='utf-8').read())" "$MAIN/docs/balance/economy_act2_latest.md"
PYTHONIOENCODING=utf-8 python -c "import sys;print(open(sys.argv[1],encoding='utf-8').read())" "$MAIN/docs/balance/economy_act3_latest.md"
PYTHONIOENCODING=utf-8 python - "$MAIN/docs/balance/sim_latest.md" "$MAIN/docs/balance/sim_act2_latest.md" "$MAIN/docs/balance/sim_act3_latest.md" <<'EOF'
import sys
for path in sys.argv[1:]:
  s = open(path, encoding="utf-8").read()
  print(s.splitlines()[0]); print()
  for sec in ["## 胜率", "## 平均回合数", "## 平均净收益", "## 压制检查", "## 各场景最优"]:
    i = s.index(sec)
    j = s.find("\n## ", i + 3)
    print(s[i:j if j > 0 else None].strip())
    print()
EOF
