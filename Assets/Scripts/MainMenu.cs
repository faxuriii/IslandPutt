using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public GameObject settingsMenu;
    public GameObject mainMenu;

    [SerializeField] Slider music;
    [SerializeField] TMP_Text musicLabel;
    [SerializeField] Button playButton;
    [SerializeField] Button settingsButton;
    MapSelector selector;
    static bool abrirSelectorAlEntrar;

    public static void CargarSelector()
    {
        abrirSelectorAlEntrar = true;
        SceneManager.LoadScene("MainMenu");
    }

    void Start()
    {
        music.onValueChanged.AddListener(UpdateMusicLabel);
        UpdateMusicLabel(music.value);
        if (!abrirSelectorAlEntrar) return;
        abrirSelectorAlEntrar = false;
        PlayGame();
    }

    void UpdateMusicLabel(float value)
    {
        musicLabel.text = $"MÚSICA   {Mathf.RoundToInt(value * 100f)}%";
    }

    void Update()
    {
        if (settingsMenu.activeSelf && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            OpenMainMenu();
    }

    public void OpenSettingsMenu()
    {
        settingsMenu.SetActive(true);
        mainMenu.SetActive(false);
        EventSystem.current?.SetSelectedGameObject(music.gameObject);
    }

    public void OpenMainMenu()
    {
        settingsMenu.SetActive(false);
        mainMenu.SetActive(true);
        EventSystem.current?.SetSelectedGameObject(settingsButton.gameObject);
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
        EventSystem.current?.SetSelectedGameObject(playButton.gameObject);
    }
}
