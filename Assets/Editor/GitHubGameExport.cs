#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Helper tool to build and publish the game to GitHub Pages (playable in browser)
/// or GitHub Releases (playable as a Windows .exe application).
/// </summary>
public static class GitHubGameExport
{
    private static readonly string[] ScenesToBuild = new string[]
    {
        "Assets/RPG_FPS_game_assets_industrial/Map_v1.unity"
    };

    [MenuItem("Tools/GitHub/1. Build WebGL (Play Online on GitHub Pages)")]
    public static void BuildWebGLForGitHubPages()
    {
        BuildWebGLInternal(true);
    }

    /// <summary>Headless build entry point for CI / command line. No modal dialogs.</summary>
    public static void BuildWebGLBatch()
    {
        BuildWebGLInternal(false);
    }

    /// <summary>Headless sanity check: compiles all scripts without building. Logs a marker on success.</summary>
    public static void CompileCheck()
    {
        Debug.Log("[GitHubGameExport] COMPILE_CHECK_OK");
    }

    private static void BuildWebGLInternal(bool showDialog)
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
        {
            string msg = "WebGL Build Support is not installed in Unity 6000.4.6f1.";
            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "WebGL Support Required",
                    msg + "\n\nInstall it from Unity Hub -> Installs -> Add Modules -> WebGL Build Support.",
                    "OK"
                );
                return;
            }
            throw new System.Exception(msg);
        }

        string exportDir = Path.Combine(Directory.GetCurrentDirectory(), "docs");
        if (!Directory.Exists(exportDir))
        {
            Directory.CreateDirectory(exportDir);
        }

        // CRITICAL FOR GITHUB PAGES: Enable decompression fallback so files load without server header configuration
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled; // Direct loading on GitHub Pages

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = ScenesToBuild,
            locationPathName = exportDir,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        Debug.Log("[GitHubGameExport] Starting WebGL Build to 'docs/'...");
        BuildReport report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result == BuildResult.Succeeded)
        {
            // Create .nojekyll so GitHub Pages serves all assets and subdirectories
            File.WriteAllText(Path.Combine(exportDir, ".nojekyll"), "");

            Debug.Log("[GitHubGameExport] WebGL build succeeded -> docs/");
            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Build Succeeded! 🎮",
                    "Your WebGL game has been built successfully into the 'docs' folder!\n\n" +
                    "Steps to activate your online link:\n" +
                    "1. Go to your repository on GitHub: https://github.com/ashrafnsali-bit/MyBUPGI\n" +
                    "2. Go to Settings -> Pages.\n" +
                    "3. Under 'Build and deployment', choose 'Deploy from a branch'.\n" +
                    "4. Branch: main, Folder: /docs, then click Save.\n\n" +
                    "Your game will be live and playable at:\n" +
                    "https://ashrafnsali-bit.github.io/MyBUPGI/",
                    "Awesome!"
                );
            }
        }
        else
        {
            string msg = "WebGL build failed. Check the Unity Console for details.";
            Debug.LogError("[GitHubGameExport] " + msg);
            if (showDialog) EditorUtility.DisplayDialog("Build Failed", msg, "OK");
            else throw new System.Exception(msg);
        }
    }

    [MenuItem("Tools/GitHub/2. Build Windows Executable (.exe)")]
    public static void BuildWindowsExe()
    {
        string exportDir = Path.Combine(Directory.GetCurrentDirectory(), "Build_Windows");
        if (!Directory.Exists(exportDir))
        {
            Directory.CreateDirectory(exportDir);
        }

        string exePath = Path.Combine(exportDir, "MyBUPGI.exe");

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = ScenesToBuild,
            locationPathName = exePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        Debug.Log("[GitHubGameExport] Starting Windows 64-bit Build...");
        BuildReport report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result == BuildResult.Succeeded)
        {
            EditorUtility.DisplayDialog(
                "Windows Build Succeeded! 🖥️",
                $"Your Windows executable is ready in:\n{exportDir}\n\n" +
                "You can compress this folder into a .zip file and share it on GitHub Releases:\n" +
                "https://github.com/ashrafnsali-bit/MyBUPGI/releases",
                "Open Folder"
            );
            EditorUtility.RevealInFinder(exePath);
        }
        else
        {
            EditorUtility.DisplayDialog("Build Failed", "Windows build failed. Check the Unity Console for details.", "OK");
        }
    }
}
#endif
