#!/bin/sh
# 实机测试（PlayMode）：在 Claude 工作副本的 Unity 工程中运行，不占用主目录 Unity，不影响用户试玩。
# 用法：sh tools/playtest.sh [Unity.exe 路径]
UNITY_EXE="${1:-/d/software/unity/Editor/6000.6.3f1/Editor/Unity.exe}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
LOGS="$ROOT/UNITY/Logs"
mkdir -p "$LOGS"
# 先导入数据并生成场景，保证与当前代码一致
"$UNITY_EXE" -batchmode -projectPath "$ROOT/UNITY" -executeMethod Game.EditorTools.BatchTasks.RebuildAll -quit -logFile "$LOGS/playtest_rebuild.log" >/dev/null
echo "[playtest] 重建退出码 $?"
"$UNITY_EXE" -batchmode -projectPath "$ROOT/UNITY" -runTests -testPlatform PlayMode -testResults "$LOGS/playtest_results.xml" -logFile "$LOGS/playtest.log" >/dev/null
code=$?
grep -o '<test-run [^>]*' "$LOGS/playtest_results.xml" | grep -oE '(total|passed|failed)="[0-9]+"' | tr '\n' ' '
echo
grep -oE 'name="[^"]*" [^>]*result="Failed"' "$LOGS/playtest_results.xml" | sed 's/^/[playtest] 失败 /'
grep -E '^\[PlayTest\]' "$LOGS/playtest.log"
echo "[playtest] 退出码 $code"
exit $code
