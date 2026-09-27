using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 命令行一键流程：导入数据 → 生成逻辑场景 → 战斗冒烟测试。
    /// Unity -batchmode -projectPath UNITY -executeMethod Game.EditorTools.BatchTasks.RebuildAll -quit -logFile -
    /// </summary>
    public static class BatchTasks
    {
        public static void RebuildAll()
        {
            try
            {
                CsvDataImporter.ImportAll();
                PrototypeSceneBuilder.Build();
                BattleSmokeTest.Run();
                EconomySmokeTest.Run();
                SaveSmokeTest.Run();
                TownSmokeTest.Run();
                ProgressionSmokeTest.Run();
                StorySmokeTest.Run();
                Debug.Log("[Batch] RebuildAll 完成");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Batch] RebuildAll 失败：{e}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
