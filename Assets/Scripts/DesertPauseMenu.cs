using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
public class DesertPauseMenu : MonoBehaviour
{
    static readonly Color Brown = new Color32(61, 38, 29, 255);
    static readonly Color Clay = new Color32(112, 69, 47, 255);
    static readonly Color Sand = new Color32(250, 213, 146, 255);
    static readonly Color Cream = new Color32(255, 241, 216, 255);

    MiniGolfShotController shot;
    TMP_FontAsset font;
    RectTransform pauseButton, overlay;
    GameObject settingsPanel, controls, ownedEventSystem;
    Button resumeButton, controlsButton, backButton;
    float previousTimeScale;
    int resumedFrame = -1;
    bool paused;
    bool showingResults;

    public bool CapturesGameplay => paused || resumedFrame == Time.frameCount;

    public void Init(MiniGolfShotController controller, GolfHUD golfHud)
    {
        shot = controller;
        font = golfHud.fuente;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        pauseButton = Place("Botón de pausa", transform, new Vector2(1, 1),
            new Vector2(1, 1), new Vector2(-48, -40), new Vector2(230, 72));
        MakeButton(pauseButton, "Pausa   Esc", Clay, Cream, Pause);

        overlay = Place("Fondo de pausa", transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
        overlay.anchorMax = Vector2.one;
        Paint(overlay, new Color32(22, 14, 10, 176)).raycastTarget = true;
        var panel = Box(overlay, "Panel de pausa", 0, 0, 720, 620);
        settingsPanel = panel.gameObject;
        Paint(panel, Brown);
        Paint(Box(panel, "Borde superior", 0, 305, 720, 10), Sand);
        Label(Box(panel, "Mapa", 0, 252, 620, 40), "ISLA DESIERTO", 25, Sand);
        Label(Box(panel, "Título", 0, 193, 620, 70), "Pausa", 65, Cream);

        var options = panel;
        resumeButton = MakeButton(Box(options, "Reanudar", 0, 99, 568, 72),
            "Reanudar", Sand, Brown, Resume);

        var music = MusicManager.Instancia;
        VolumeRow(options, "Música", 0, music != null ? music.Volumen : 0f,
            value => { if (music != null) music.Volumen = value; });
        controlsButton = MakeButton(Box(options, "Controles", 0, -113, 568, 68),
            "Ver controles", Clay, Cream, ShowControls);
        MakeButton(Box(options, "Menú principal", 0, -219, 568, 68),
            "Salir al menú principal", Clay, Cream, ExitToMainMenu);

        var guide = Box(overlay, "Guía de controles", 0, 0, 1000, 790);
        controls = guide.gameObject;
        Paint(guide, Brown);
        Paint(Box(guide, "Borde superior", 0, 390, 1000, 10), Sand);
        Label(Box(guide, "Título", 0, 326, 880, 75), "Controles", 62, Cream);

        ControlHeading(guide, -232, 248, "GOLPE");
        ControlRow(guide, -232, 178, "Ratón · Flechas izq. y der.", "Apunta el golpe");
        ControlRow(guide, -232, 81, "Clic izquierdo", "Arrastra hacia abajo y suelta");
        ControlRow(guide, -232, -16, "Espacio", "Mantén para cargar y suelta");
        ControlRow(guide, -232, -113, "Clic derecho", "Cancela la carga");
        ControlRow(guide, -232, -210, "Z · Deshacer golpe", "Vuelve al punto del último tiro");

        ControlHeading(guide, 232, 248, "CÁMARA");
        ControlRow(guide, 232, 178, "Clic derecho · WASD", "Gira la cámara");
        ControlRow(guide, 232, 81, "Rueda", "Ajusta el zoom");
        ControlHeading(guide, 232, 10, "VISTA LIBRE");
        ControlRow(guide, 232, -62, "F · WASD", "Alterna la vista libre y muévete");
        ControlRow(guide, 232, -159, "Q · E · Mayús", "Baja, sube o acelera");

        ControlRow(guide, 232, -252, "R", "Reinicia el hoyo");

        backButton = MakeButton(Box(guide, "Volver", 0, -345, 420, 64),
            "Volver", Clay, Cream, ShowSettings);

        controls.SetActive(false);
        overlay.gameObject.SetActive(false);
    }

    void Start()
    {
        if (EventSystem.current != null) return;
        ownedEventSystem = new GameObject("IslandDesert EventSystem",
            typeof(EventSystem), typeof(InputSystemUIInputModule));
        SceneManager.MoveGameObjectToScene(ownedEventSystem, gameObject.scene);
    }

    void Update()
    {
        if (showingResults || Keyboard.current?.escapeKey.wasPressedThisFrame != true) return;
        if (!paused) Pause();
        else if (controls.activeSelf) ShowSettings();
        else Resume();
    }

    public bool PointerOverPauseButton()
    {
        return Mouse.current != null && RectTransformUtility.RectangleContainsScreenPoint(
            pauseButton, Mouse.current.position.ReadValue());
    }

    void Pause()
    {
        if (paused || shot.Embocada) return;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        paused = true;
        shot.CancelChargeForPause();
        pauseButton.gameObject.SetActive(false);
        overlay.gameObject.SetActive(true);
        ShowSettings();
    }

    void Resume()
    {
        if (!paused) return;
        RestoreGameplay();
        resumedFrame = Time.frameCount;
        overlay.gameObject.SetActive(false);
        pauseButton.gameObject.SetActive(true);
        EventSystem.current?.SetSelectedGameObject(null);
    }

    public void ShowResults(string result, int strokes, int par)
    {
        if (showingResults) return;
        showingResults = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        paused = true;
        pauseButton.gameObject.SetActive(false);
        settingsPanel.SetActive(false);
        controls.SetActive(false);
        overlay.gameObject.SetActive(true);

        var card = Box(overlay, "Tarjeta de resultados", 0, 0, 936, 536);
        Paint(card, Brown);
        Paint(Box(card, "Borde superior", 0, 263, 936, 10), Sand);
        Label(Box(card, "Campo", 0, 208, 864, 44), "ISLA DESIERTO  ·  HOYO 1 COMPLETADO", 28, Sand);
        Label(Box(card, "Resultado", 0, 117, 864, 98), result, 68, Cream);

        int difference = strokes - par;
        ResultStat(card, -288, "GOLPES", strokes.ToString());
        ResultStat(card, 0, "PAR DEL HOYO", par.ToString());
        ResultStat(card, 288, "RESPECTO AL PAR", difference.ToString("+0;-0;0"));

        var replay = MakeButton(Box(card, "Repetir", -288, -182, 264, 76),
            "Repetir", Sand, Brown, () =>
            {
                if (!paused) return;
                RestoreGameplay();
                SceneManager.LoadScene(gameObject.scene.name);
            });
        MakeButton(Box(card, "Cambiar mapa", 0, -182, 264, 76),
            "Cambiar mapa", Clay, Cream, () =>
            {
                if (!paused) return;
                RestoreGameplay();
                MainMenu.CargarSelector();
            });
        MakeButton(Box(card, "Menú principal", 288, -182, 264, 76),
            "Menú principal", Clay, Cream, ExitToMainMenu);
        Select(replay);
    }

    void ResultStat(Transform parent, float x, string title, string value)
    {
        var tile = Box(parent, title, x, -32, 264, 132);
        Paint(tile, Clay);
        Label(Box(tile, "Valor", 0, 18, 248, 70), value, 52, Cream);
        Label(Box(tile, "Concepto", 0, -38, 248, 38), title, 22, Sand);
    }

    void ExitToMainMenu()
    {
        if (!paused) return;
        RestoreGameplay();
        SceneManager.LoadScene("MainMenu");
    }

    void RestoreGameplay()
    {
        Time.timeScale = previousTimeScale;
        paused = false;
        PlayerPrefs.Save();
    }

    void OnDisable()
    {
        if (paused)
        {
            RestoreGameplay();
            overlay.gameObject.SetActive(false);
            pauseButton.gameObject.SetActive(true);
        }
    }

    void OnDestroy()
    {
        if (ownedEventSystem != null) Destroy(ownedEventSystem);
    }

    void ShowControls()
    {
        settingsPanel.SetActive(false);
        controls.SetActive(true);
        Select(backButton);
    }

    void ShowSettings()
    {
        bool returning = controls.activeSelf;
        controls.SetActive(false);
        settingsPanel.SetActive(true);
        Select(returning ? controlsButton : resumeButton);
    }

    static void Select(Selectable selectable)
    {
        EventSystem.current?.SetSelectedGameObject(selectable.gameObject);
    }

    void VolumeRow(Transform parent, string title, float y, float value, UnityEngine.Events.UnityAction<float> change)
    {
        var row = Box(parent, title, 0, y, 568, 100);
        Label(Box(row, "Nombre", -204, 29, 160, 35), title, 29, Cream, TextAlignmentOptions.Left);
        var percent = Label(Box(row, "Porcentaje", 234, 29, 100, 35), "", 26, Sand, TextAlignmentOptions.Right);
        var area = Box(row, "Volumen de " + title, 0, -16, 568, 52);
        var hitArea = Paint(area, new Color(0, 0, 0, 0));
        hitArea.raycastTarget = true;
        var track = Box(area, "Pista", 0, 0, 540, 12);
        Paint(track, Clay);
        var fill = Place("Progreso", track, Vector2.zero, new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
        fill.anchorMax = Vector2.one;
        Paint(fill, Sand);
        var handleArea = Box(area, "Recorrido", 0, 0, 540, 30);
        var handle = Box(handleArea, "Tirador", 0, 0, 26, 30);
        var handleImage = Paint(handle, Cream);
        var slider = area.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.SetValueWithoutNotify(value);
        percent.text = Mathf.RoundToInt(value * 100f) + "%";
        slider.onValueChanged.AddListener(v =>
        {
            percent.text = Mathf.RoundToInt(v * 100f) + "%";
            change(v);
        });
    }

    void ControlHeading(Transform parent, float x, float y, string title)
    {
        Label(Box(parent, title, x, y, 416, 38), title, 25, Sand, TextAlignmentOptions.Left);
    }

    void ControlRow(Transform parent, float x, float y, string key, string action)
    {
        var row = Box(parent, key, x, y, 432, 86);
        Paint(row, new Color32(79, 50, 36, 255));
        Paint(Box(row, "Acento", -214, 0, 4, 86), Sand);
        Label(Box(row, "Tecla", 0, 18, 396, 34), key, 30, Sand, TextAlignmentOptions.Left);
        var description = Label(Box(row, "Acción", 0, -20, 396, 34), action, 26, Cream, TextAlignmentOptions.Left);
        description.fontStyle = FontStyles.Normal;
    }

    Button MakeButton(RectTransform rect, string title, Color background, Color text, UnityEngine.Events.UnityAction click)
    {
        var image = Paint(rect, background);
        image.raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.colorMultiplier = 1.15f;
        colors.highlightedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.normalColor = new Color(0.88f, 0.88f, 0.88f);
        button.colors = colors;
        button.onClick.AddListener(click);
        Label(Box(rect, "Texto", 0, 0, rect.sizeDelta.x - 20, rect.sizeDelta.y), title, 30, text);
        return button;
    }

    TextMeshProUGUI Label(RectTransform rect, string text, float size, Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.fontStyle = FontStyles.Bold;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        return label;
    }

    static Image Paint(RectTransform rect, Color color)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static RectTransform Box(Transform parent, string name, float x, float y, float width, float height)
    {
        var center = new Vector2(0.5f, 0.5f);
        return Place(name, parent, center, center, new Vector2(x, y), new Vector2(width, height));
    }

    static RectTransform Place(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }
}
