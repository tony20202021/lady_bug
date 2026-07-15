using UnityEditor;

public static class BuildScript
{
    public static void PerformBuild()
    {
        var buildOptions = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = "Builds/LadyBugHitTheRoad.app",
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(buildOptions);
        UnityEngine.Debug.Log("Build result: " + report.summary.result);
    }
}
