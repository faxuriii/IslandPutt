using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public GameObject settingsMenu;
    public GameObject mainMenu;

    MapSelector selector;
    static bool abrirSelectorAlEntrar;

    public static void CargarSelector()
    {
        abrirSelectorAlEntrar = true;
        SceneManager.LoadScene("MainMenu");
    }

    void Start()
    {
        if (!abrirSelectorAlEntrar) return;
        abrirSelectorAlEntrar = false;
        PlayGame();
    }

    public void OpenSettingsMenu()
    {
        settingsMenu.SetActive(true);
        mainMenu.SetActive(false);
    }

    public void OpenMainMenu()
    {
        settingsMenu.SetActive(false);
        mainMenu.SetActive(true);
    }

    public void QuitGame()
    {
        Application.Quit();

        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }

    /// <summary>Botón PLAY: abre la página para elegir mapa.</summary>
    public void PlayGame()
    {
        if (selector == null) selector = gameObject.AddComponent<MapSelector>();
        if (mainMenu != null) mainMenu.SetActive(false);
        if (settingsMenu != null) settingsMenu.SetActive(false);
        selector.Mostrar(this);
    }

    /// <summary>Lo llama la página de mapas al pulsar VOLVER.</summary>
    public void VolverDesdeSelector()
    {
        if (mainMenu != null) mainMenu.SetActive(true);
    }
}
