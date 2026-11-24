// Headless build entry point used by the GitHub Actions workflow:
//   Unity -batchmode -quit -projectPath Game -executeMethod BBBuild.Build
// Creates the (empty) boot scene, keeps the shader variants the runtime-built world needs, and builds a Windows .exe.
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BBBuild
{
    const string ScenePath = "Assets/Scenes/Main.unity";

    public static void Build()
    {
        Directory.CreateDirectory("Assets/Scenes");
        Directory.CreateDirectory("Assets/Resources");

        // A material with emission on, referenced from the scene, so the _EMISSION variant isn't stripped
        // (night windows, lamps and the press light are made emissive at runtime).
        var mat = new Material(Shader.Find("Standard"));
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", Color.white);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        AssetDatabase.CreateAsset(mat, "Assets/Resources/BBKeepEmission.mat");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // Fog settings in a build scene keep the fog shader variants.
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;

        var keep = GameObject.CreatePrimitive(PrimitiveType.Cube);
        keep.name = "ShaderKeep";
        keep.GetComponent<Renderer>().sharedMaterial = mat;
        keep.SetActive(false);

        var textKeep = new GameObject("TextKeep");
        var tm = textKeep.AddComponent<TextMesh>();
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tm.font = font;
        textKeep.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        textKeep.SetActive(false);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();

        PlayerSettings.productName = "Blind Box Wars";
        PlayerSettings.companyName = "Plushville";
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = false;

        var opts = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Build/BlindBoxWars/BlindBoxWars.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(opts);
        Debug.Log("BUILD RESULT: " + report.summary.result + "  errors: " + report.summary.totalErrors);
        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
