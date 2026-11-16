using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Late.EditorTools
{
    /// <summary>
    /// Builds del juego desde el menú Late › Builds o desde la línea de comandos:
    ///   Unity -batchmode -quit -projectPath LateGame -executeMethod Late.EditorTools.LateBuilds.BuildAndroid
    /// Se guardan en LateGame/Builds/ (fuera de git).
    /// </summary>
    public static class LateBuilds
    {
        private const string OutputRoot = "Builds";

        [MenuItem("Late/Builds/Android (desarrollo: permite HTTP para probar con el servidor local)")]
        public static void BuildAndroidDevelopment()
        {
            // Android bloquea HTTP sin cifrar. Para probar contra el Docker del PC (http://IP:5080) se permite
            // solo en esta build de desarrollo; la build final sigue exigiendo HTTPS.
            InsecureHttpOption previous = PlayerSettings.insecureHttpOption;
            PlayerSettings.insecureHttpOption = InsecureHttpOption.DevelopmentOnly;
            try
            {
                Build(BuildTarget.Android, Path.Combine(OutputRoot, "Android", "Late-dev.apk"),
                    BuildOptions.Development);
            }
            finally
            {
                PlayerSettings.insecureHttpOption = previous;
            }
        }

        [MenuItem("Late/Builds/Android")]
        public static void BuildAndroid() =>
            Build(BuildTarget.Android, Path.Combine(OutputRoot, "Android", "Late.apk"), BuildOptions.None);

        [MenuItem("Late/Builds/Windows")]
        public static void BuildWindows() =>
            Build(BuildTarget.StandaloneWindows64, Path.Combine(OutputRoot, "Windows", "Late.exe"), BuildOptions.None);

        private static void Build(BuildTarget target, string path, BuildOptions options)
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = target,
                options = options,
            });

            BuildSummary summary = report.summary;
            string message = $"[Builds] {target}: {summary.result}, {summary.totalSize / (1024f * 1024f):0.0} MB, " +
                             $"{summary.totalTime.TotalSeconds:0} s, {summary.totalErrors} errores → {Path.GetFullPath(path)}";

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log(message);
                if (!Application.isBatchMode) EditorUtility.RevealInFinder(path);
            }
            else
            {
                Debug.LogError(message);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
