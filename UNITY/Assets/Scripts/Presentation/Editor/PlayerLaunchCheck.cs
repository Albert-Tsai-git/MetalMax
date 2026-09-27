#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>构建可启动的 Windows 开发版，供表现与玩法实测。</summary>
    public static class PlayerLaunchCheck
    {
        public static void Build()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("[Launch] Build Settings 中没有场景");
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../ArtSource/TestBuild"));
            Directory.CreateDirectory(output);
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(output, "MetalMax.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[Launch] 构建结果={report.summary.result}, 错误={report.summary.totalErrors}, 警告={report.summary.totalWarnings}, 输出={options.locationPathName}");
            if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
#endif
