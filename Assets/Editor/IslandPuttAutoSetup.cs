using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Monta el mapa automáticamente (sin tocar ningún menú) cuando:
///  - se abre la escena IslandDesert,
///  - Unity termina de compilar,
///  - o le das a Play sin que esté montado.
/// </summary>
[InitializeOnLoad]
public static class IslandPuttAutoSetup
{
    const string SceneName = "IslandDesert";

    static IslandPuttAutoSetup()
    {
        EditorApplication.delayCall += AsegurarEscenasEnBuild;
        EditorApplication.delayCall += Check;
        EditorSceneManager.sceneOpened += (s, m) => EditorApplication.delayCall += Check;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    /// <summary>MainMenu primero e IslandDesert activada en Build Profiles, para poder cargarla desde el menú.</summary>
    static void AsegurarEscenasEnBuild()
    {
        string[] necesarias = { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/IslandDesert.unity" };
        var lista = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool cambio = false;
        for (int i = necesarias.Length - 1; i >= 0; i--)
        {
            string path = necesarias[i];
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
            int idx = lista.FindIndex(s => s.path == path);
            if (idx >= 0)
            {
                if (!lista[idx].enabled) { lista[idx] = new EditorBuildSettingsScene(path, true); cambio = true; }
                if (i == 0 && idx != 0) { var e = lista[idx]; lista.RemoveAt(idx); lista.Insert(0, e); cambio = true; }
            }
            else
            {
                if (i == 0) lista.Insert(0, new EditorBuildSettingsScene(path, true));
                else lista.Add(new EditorBuildSettingsScene(path, true));
                cambio = true;
            }
        }
        if (cambio)
        {
            EditorBuildSettings.scenes = lista.ToArray();
            Debug.Log("[IslandPutt] Escenas MainMenu e IslandDesert añadidas a Build Profiles.");
        }
    }

    static void OnPlayMode(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            Check();
            // Deseleccionar evita los errores 'MissingReferenceException' del Inspector al salir de Play
            Selection.activeObject = null;
        }
    }

    // Usa la fuente del menú (Bungee) en el HUD del juego si no hay ninguna puesta
    static void AsignarFuente(UnityEngine.SceneManagement.Scene scene)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Fonts/Bungee-Regular SDF.asset");
        if (font == null) return;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var ctrl in root.GetComponentsInChildren<MiniGolfShotController>(true))
                if (ctrl.uiFont == null)
                {
                    Undo.RecordObject(ctrl, "Fuente HUD");
                    ctrl.uiFont = font;
                    EditorUtility.SetDirty(ctrl);
                    EditorSceneManager.MarkSceneDirty(scene);
                }
    }

    static void Check()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != SceneName) return;
        if (!IslandPuttMapSetup.EstaMontado())
        {
            Debug.Log("[IslandPutt] Montando el mapa automáticamente...");
            IslandPuttMapSetup.Montar(true);
        }
        AsignarFuente(scene);
        if (!IslandPuttMapSetup.CieloMontado())
        {
            Debug.Log("[IslandPutt] Montando el cielo automáticamente...");
            IslandPuttMapSetup.MontarCielo(true);
        }
    }
}
