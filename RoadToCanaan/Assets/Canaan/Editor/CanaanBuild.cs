using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Canaan.EditorTools
{
    /// <summary>Headless player build, run by Tools/build.sh. Output: Builds/Canaan.app.</summary>
    public static class CanaanBuild
    {
        public static void Build()
        {
            try
            {
                // Apple Silicon only: halves the build time and the target machine is an M2.
                var settings = Type.GetType("UnityEditor.OSXStandalone.UserBuildSettings, UnityEditor.OSXStandalone.Extensions");
                var arch = settings?.GetProperty("architecture");
                if (arch != null) arch.SetValue(null, Enum.Parse(arch.PropertyType, "ARM64"));

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { CanaanSetup.BootScene },
                    locationPathName = "Builds/Canaan.app",
                    target = BuildTarget.StandaloneOSX,
                    options = BuildOptions.None
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                var s = report.summary;
                Debug.Log("CANAAN_BUILD " + s.result + " errors=" + s.totalErrors + " size=" + (s.totalSize / 1048576) +
                          "MB time=" + s.totalTime);
                EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception e)
            {
                Debug.LogError("CANAAN_BUILD_FAIL " + e);
                EditorApplication.Exit(1);
            }
        }
    }
}
