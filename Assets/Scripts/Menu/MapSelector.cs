using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Página de selección de mapa. La abre MainMenu.PlayGame() y se construye sola por código.
/// Las imágenes están en Assets/Resources/IslandPutt/.
/// Para añadir un mapa nuevo: añade una entrada a "mapas" (o cambia la de PRÓXIMAMENTE)
/// y pon su escena en File > Build Profiles.
/// </summary>
public class MapSelector : MonoBehaviour
{
    [System.Serializable]
    public class Mapa
    {
        public string nombre;
        public string descripcion;
        public string escena;
        [Tooltip("Ruta dentro de Resources, sin extensión")]
        public string miniatura;
        public bool disponible = true;
    }

    public Mapa[] mapas =
    {
        new Mapa { nombre = "ISLA DESIERTO", descripcion = "1 HOYO  ·  PAR 3", escena = "IslandDesert",
                   miniatura = "IslandPutt/MapaDesierto", disponible = true },
        new Mapa { nombre = "PRÓXIMAMENTE", descripcion = "EN CONSTRUCCIÓN", escena = "",
                   miniatura = "IslandPutt/MapaProximamente", disponible = false },
    };

    static readonly Color Marron = new Color(0.42f, 0.22f, 0.08f);
    static readonly Color MarronClaro = new Color(0.62f, 0.36f, 0.14f);

    GameObject root;
    RectTransform titulo;
    MainMenu menu;
    TMP_FontAsset fuente;
    bool cargando;
    public static bool DosJugadores { get; private set; }
    Image solo, pareja;
    TextMeshProUGUI etiquetaSolo, etiquetaPareja;
    RectTransform configuracion, panelJugadores;
    TextMeshProUGUI detalleModo;
    readonly Image[] acentos = new Image[2];
    readonly Outline[,] bordes = new Outline[2, 6];
    static readonly string[] jugadores = new string[2];
    public static string NombreJugador(int i) => string.IsNullOrWhiteSpace(jugadores[i]) ? $"Jugador {i + 1}" : jugadores[i];
    readonly Button[,] muestras = new Button[2, 6];
    readonly TextMeshProUGUI[] etiquetas = new TextMeshProUGUI[2];
    static readonly Color32[] colores = {
        new Color32(60, 175, 255, 255), new Color32(255, 145, 46, 255),
        new Color32(194, 104, 255, 255), new Color32(78, 219, 104, 255),
        new Color32(255, 83, 105, 255), new Color32(255, 226, 66, 255)
    };
    static readonly string[] nombres = { "Azul", "Naranja", "Violeta", "Verde", "Coral", "Amarillo" };
    static readonly int[] seleccion = { 0, 1 };
    public static Color ColorJugador(int jugador) => colores[seleccion[jugador]];

    void CrearSelectorColor(int jugador)
    {
        var anchor = Vector2.one * .5f;
        var rect = Rect("Color jugador " + (jugador + 1), panelJugadores, anchor, anchor,
            new Vector2(jugador == 0 ? -233 : 233, -8), new Vector2(440, 282));
        var borde = Rect("Color elegido", rect, anchor, anchor, new Vector2(0, 137), new Vector2(440, 8));
        acentos[jugador] = borde.gameObject.AddComponent<Image>();
        Texto(Rect("Jugador", rect, anchor, anchor, new Vector2(0, 97), new Vector2(400, 50)),
            $"JUGADOR {jugador + 1}", 34, Color.white);
        Texto(Rect("Etiqueta nombre", rect, anchor, anchor, new Vector2(0, 52), new Vector2(380, 30)),
            "Tu nombre", 22, Color.white).alignment = TextAlignmentOptions.Left;
        var campo = Rect("Nombre", rect, anchor, anchor, new Vector2(0, 7), new Vector2(386, 52));
        var fondoCampo = campo.gameObject.AddComponent<Image>();
        fondoCampo.color = new Color(.24f, .13f, .06f);
        var contenido = Rect("Área de texto", campo, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-24, 0));
        var texto = Texto(contenido, "", 26, Color.white);
        texto.richText = false;
        var pista = Texto(contenido, $"Jugador {jugador + 1}", 25, new Color(.85f, .78f, .69f));
        var entrada = campo.gameObject.AddComponent<TMP_InputField>();
        entrada.targetGraphic = fondoCampo;
        entrada.textViewport = contenido;
        entrada.textComponent = texto;
        entrada.placeholder = pista;
        entrada.characterLimit = 14;
        entrada.SetTextWithoutNotify(jugadores[jugador] ?? "");
        entrada.onValueChanged.AddListener(valor => jugadores[jugador] = valor.Trim());
        var fondo = rect.gameObject.AddComponent<Image>();
        fondo.color = Marron;
        var center = Vector2.one * .5f;
        etiquetas[jugador] = Texto(Rect("Jugador", rect, center, center,
            new Vector2(0, -46), new Vector2(380, 36)), "", 22, Color.white);
        etiquetas[jugador].alignment = TextAlignmentOptions.Left;
        for (int i = 0; i < colores.Length; i++)
        {
            int indice = i;
            var muestra = Rect(nombres[i], rect, center, center,
                new Vector2(-160 + i * 64, -100), new Vector2(48, 42));
            var image = muestra.gameObject.AddComponent<Image>();
            image.color = colores[i];
            bordes[jugador, i] = muestra.gameObject.AddComponent<Outline>();
            bordes[jugador, i].effectColor = Color.white;
            bordes[jugador, i].effectDistance = new Vector2(3, -3);
            var boton = muestra.gameObject.AddComponent<Button>();
            boton.targetGraphic = image;
            muestras[jugador, i] = boton;
            boton.onClick.AddListener(() =>
            {
                if (cargando || seleccion[1 - jugador] == indice) return;
                seleccion[jugador] = indice;
                ActualizarColores();
            });
        }
    }

    void ActualizarColores()
    {
        for (int jugador = 0; jugador < 2; jugador++)
        {
            etiquetas[jugador].text = "Bola · " + nombres[seleccion[jugador]];
            acentos[jugador].color = ColorJugador(jugador);
            for (int i = 0; i < colores.Length; i++)
            {
                var boton = muestras[jugador, i];
                bordes[jugador, i].enabled = i == seleccion[jugador];
                boton.interactable = i != seleccion[1 - jugador];
                boton.GetComponent<Image>().color = boton.interactable
                    ? colores[i] : Color.Lerp(colores[i], Marron, .75f);

            }
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetModo()
    {
        DosJugadores = false;
        jugadores[0] = jugadores[1] = null;
        seleccion[0] = 0;
        seleccion[1] = 1;
    }

    Image BotonModo(string texto, float x, bool doble)
    {
        var anchor = new Vector2(.5f, .17f);
        var rect = Rect(texto, root.transform, anchor, anchor, new Vector2(x, 0), new Vector2(320, 90));
        var image = rect.gameObject.AddComponent<Image>();
        var etiqueta = Texto(rect, texto, doble ? 30 : 34, Color.white);
        if (doble)
        {
            etiqueta.rectTransform.anchoredPosition = new Vector2(0, 13);
            detalleModo = Texto(rect, "2 jugadores · Por turnos", 18, Color.white);
            detalleModo.rectTransform.anchoredPosition = new Vector2(0, -22);

        }
        if (doble) etiquetaPareja = etiqueta;
        else etiquetaSolo = etiqueta;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() =>
        {
            SeleccionarModo(doble);
            configuracion.gameObject.SetActive(doble);

        });
        return image;
    }

    void SeleccionarModo(bool doble)
    {
        if (cargando) return;
        DosJugadores = doble;

        var activo = new Color32(253, 218, 150, 255);
        solo.color = doble ? Marron : activo;
        pareja.color = doble ? activo : Marron;
        etiquetaSolo.color = doble ? Color.white : Marron;
        etiquetaPareja.color = detalleModo.color = doble ? Marron : Color.white;
    }

    public void Mostrar(MainMenu m)
    {
        menu = m;
        if (root == null) Construir();
        root.SetActive(true);

    }

    public void Volver()
    {
        if (cargando) return;
        root.SetActive(false);
        if (menu != null) menu.VolverDesdeSelector();
    }

    void Update()
    {
        if (root != null && root.activeSelf && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (configuracion.gameObject.activeSelf) configuracion.gameObject.SetActive(false);
            else Volver();
        }
    }

    // ------------------------------------------------------------------
    void Construir()
    {
        foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            if (f.name.Contains("Bungee")) { fuente = f; break; }

        root = new GameObject("SeleccionMapa");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();
        root.AddComponent<Aparecer>();

        // Fondo (pergamino)
        var fondo = Rect("Fondo", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var raw = fondo.gameObject.AddComponent<RawImage>();
        raw.texture = Resources.Load<Texture2D>("IslandPutt/FondoSeleccion");
        if (raw.texture == null) raw.color = new Color(0.98f, 0.88f, 0.68f);

        // Título
        titulo = Rect("Titulo", root.transform, new Vector2(0.5f, 0.81f), new Vector2(0.5f, 0.81f), Vector2.zero, new Vector2(1200, 130));
        var tt = Texto(titulo, "ELIGE UN MAPA", 92, Marron);
        tt.characterSpacing = 4;

        // Tarjetas
        float ancho = 560, sep = 80;
        float total = mapas.Length * ancho + (mapas.Length - 1) * sep;
        for (int i = 0; i < mapas.Length; i++)
        {
            float x = -total / 2f + ancho / 2f + i * (ancho + sep);
            CrearTarjeta(mapas[i], new Vector2(x, -10), new Vector2(ancho, 450), i);
        }

        solo = BotonModo("INDIVIDUAL", 0, false);
        pareja = BotonModo("MULTIJUGADOR", 360, true);
        SeleccionarModo(DosJugadores);

        // Botón volver
        var volver = Rect("Volver", root.transform, new Vector2(0.5f, 0.17f), new Vector2(0.5f, 0.17f), new Vector2(-360, 0), new Vector2(320, 90));
        var vImg = volver.gameObject.AddComponent<Image>();
        vImg.color = MarronClaro;
        volver.gameObject.AddComponent<Outline>().effectColor = Marron;
        var vt = Texto(volver, "VOLVER", 46, Color.white);
        vt.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.5f);
        var vb = volver.gameObject.AddComponent<Button>();
        vb.targetGraphic = vImg;
        vb.onClick.AddListener(Volver);


        var center = Vector2.one * .5f;
        configuracion = Rect("Configurar multijugador", root.transform, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero);
        configuracion.gameObject.AddComponent<Image>().color = new Color(.13f, .08f, .04f, .92f);
        panelJugadores = Rect("Panel de jugadores", configuracion, center, center, Vector2.zero, new Vector2(968, 614));
        panelJugadores.gameObject.AddComponent<Image>().color = new Color32(66, 40, 29, 255);
        Texto(Rect("Cómo jugar", panelJugadores, center, center, new Vector2(0, 169), new Vector2(900, 48)),
            "Compartid el teclado y el ratón. Un golpe por turno.", 26, Color.white);
        Texto(Rect("Título", panelJugadores, center, center, new Vector2(0, 237),
            new Vector2(740, 70)), "MULTIJUGADOR", 54, Color.white);
        CrearSelectorColor(0);
        CrearSelectorColor(1);
        ActualizarColores();
        var listo = Rect("Elegir mapa", panelJugadores, center, center, new Vector2(0, -226), new Vector2(396, 76));
        var fondoListo = listo.gameObject.AddComponent<Image>();
        fondoListo.color = new Color32(253, 218, 150, 255);
        Texto(listo, "ELEGIR MAPA", 32, Marron);
        var confirmar = listo.gameObject.AddComponent<Button>();
        confirmar.targetGraphic = fondoListo;
        confirmar.onClick.AddListener(() => configuracion.gameObject.SetActive(false));
        configuracion.gameObject.SetActive(false);

        panelJugadores.gameObject.AddComponent<Aparecer>();
        foreach (var boton in root.GetComponentsInChildren<Button>(true)) HoverPop.Instalar(boton);
    }

    void CrearTarjeta(Mapa mapa, Vector2 pos, Vector2 size, int indice)
    {
        var card = Rect("Mapa_" + indice, root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        var marco = card.gameObject.AddComponent<Image>();
        marco.color = mapa.disponible ? MarronClaro : new Color(0.45f, 0.38f, 0.35f);
        card.gameObject.AddComponent<Outline>().effectColor = Marron;
        card.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(8, -10);

        // Miniatura
        var mini = Rect("Miniatura", card, new Vector2(0, 0.27f), Vector2.one, Vector2.zero, Vector2.zero);
        mini.offsetMin = new Vector2(14, 0); mini.offsetMax = new Vector2(-14, -14);
        var img = mini.gameObject.AddComponent<RawImage>();
        img.texture = Resources.Load<Texture2D>(mapa.miniatura);
        if (img.texture == null) img.color = new Color(0.4f, 0.6f, 0.9f);
        img.raycastTarget = false;

        if (!mapa.disponible)
        {
            var q = Rect("?", mini, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var qt = Texto(q, "?", 200, new Color(1f, 1f, 1f, 0.85f));
            qt.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(6, -6);
            q.gameObject.AddComponent<Latido>();
        }

        // Nombre y descripción
        var nom = Rect("Nombre", card, new Vector2(0, 0.1f), new Vector2(1, 0.27f), Vector2.zero, Vector2.zero);
        var nt = Texto(nom, mapa.nombre, 46, Color.white);
        nt.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.5f);
        var des = Rect("Descripcion", card, new Vector2(0, 0f), new Vector2(1, 0.12f), Vector2.zero, Vector2.zero);
        Texto(des, mapa.descripcion, 26, new Color(1f, 0.92f, 0.75f));

        if (!mapa.disponible)
        {
            // Cinta "EN OBRAS"
            var cinta = Rect("Cinta", card, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-70, -50), new Vector2(260, 54));
            cinta.localRotation = Quaternion.Euler(0, 0, -20);
            cinta.gameObject.AddComponent<Image>().color = new Color(1f, 0.78f, 0.1f);
            Texto(cinta, "EN OBRAS", 28, new Color(0.2f, 0.15f, 0.05f));
        }

        var tarjeta = card.gameObject.AddComponent<TarjetaMapa>();
        tarjeta.Init(this, mapa);

    }

    public void Elegir(Mapa mapa)
    {
        if (cargando || !mapa.disponible) return;
        if (!Application.CanStreamedLevelBeLoaded(mapa.escena))
        {
            Debug.LogError($"[IslandPutt] La escena '{mapa.escena}' no está en File > Build Profiles.");
            return;
        }
        if (MusicManager.Instancia != null) MusicManager.Instancia.PrepararEscena(mapa.escena);
        cargando = true;
        TransicionEscena.Cargar(mapa.escena);
    }

    // ------------------------------------------------------------------
    static RectTransform Rect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    TextMeshProUGUI Texto(RectTransform parent, string s, float size, Color c)
    {
        var rt = Rect("Texto", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (fuente != null) t.font = fuente;
        t.text = s; t.fontSize = size; t.color = c;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }
}
