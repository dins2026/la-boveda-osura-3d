using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Herramientas del editor (menú "Boveda"):
///   1) Crea la escena con un único GameObject (GameBootstrap) y la agrega a Build Settings.
///   2) Genera el Build de Windows (.exe).
/// BuildWindowsCI se usa para compilar en la nube con GitHub Actions (sin Unity instalado).
/// Este archivo debe estar en una carpeta llamada "Editor".
/// </summary>
public static class BuildTools
{
    const string ScenePath = "Assets/Scenes/Boveda.unity";

    [MenuItem("Boveda/1) Crear escena de juego")]
    public static void CreateScene()
    {
        System.IO.Directory.CreateDirectory("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var go = new GameObject("GameBootstrap");
        go.AddComponent<GameBootstrap>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.Refresh();
        Debug.Log("Escena creada y agregada a Build Settings: " + ScenePath);
    }

    [MenuItem("Boveda/2) Build Windows (.exe)")]
    public static void BuildWindows()
    {
        Build("Build/Boveda/LaBovedaOscura.exe");
    }

    // Llamado desde GitHub Actions (game-ci/unity-builder)
    public static void BuildWindowsCI()
    {
        string path = "Build/Boveda";
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-customBuildPath") path = args[i + 1];

        if (!path.EndsWith(".exe")) path = System.IO.Path.Combine(path, "LaBovedaOscura.exe");
        CreateScene();
        Build(path);
    }

    static void Build(string exePath)
    {
        if (!System.IO.File.Exists(ScenePath)) CreateScene();

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(exePath));
        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = exePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log("Resultado del build: " + report.summary.result + " -> " + exePath);

        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded && Application.isBatchMode)
            EditorApplication.Exit(1);
    }
}
