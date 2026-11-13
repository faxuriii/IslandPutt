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
    CanvasGroup grupo;
    Image fundido;
    MainMenu menu;
    TMP_FontAsset fuente;
    bool cargando;

    public void Mostrar(MainMenu m)
    {
        menu = m;
        if (root == null) Construir();
        root.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(Entrada());
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
            Volver();
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
        grupo = root.AddComponent<CanvasGroup>();

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

        // Botón volver
        var volver = Rect("Volver", root.transform, new Vector2(0.5f, 0.17f), new Vector2(0.5f, 0.17f), Vector2.zero, new Vector2(320, 90));
        var vImg = volver.gameObject.AddComponent<Image>();
        vImg.color = MarronClaro;
        volver.gameObject.AddComponent<Outline>().effectColor = Marron;
        var vt = Texto(volver, "VOLVER", 46, Color.white);
        vt.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.5f);
        var vb = volver.gameObject.AddComponent<Button>();
        vb.targetGraphic = vImg;
        vb.onClick.AddListener(Volver);
        volver.gameObject.AddComponent<HoverPop>();

        // Fundido a negro para las transiciones
        var f2 = Rect("Fundido", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        fundido = f2.gameObject.AddComponent<Image>();
        fundido.color = new Color(0, 0, 0, 0);
        fundido.raycastTarget = false;
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
        card.localScale = Vector3.zero;
        card.gameObject.AddComponent<Aparecer>().retraso = 0.1f + indice * 0.12f;
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
        StartCoroutine(Cargar(mapa.escena));
    }

    IEnumerator Cargar(string escena)
    {
        cargando = true;
        fundido.raycastTarget = true;
        float t = 0f;
        while (t < 0.45f)
        {
            t += Time.unscaledDeltaTime;
            fundido.color = new Color(0, 0, 0, t / 0.45f);
            yield return null;
        }
        SceneManager.LoadScene(escena);
    }

    IEnumerator Entrada()
    {
        grupo.alpha = 0f;
        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.unscaledDeltaTime;
            grupo.alpha = t / 0.25f;
            titulo.localScale = Vector3.one * (1f + 0.25f * (1f - t / 0.25f));
            yield return null;
        }
        grupo.alpha = 1f;
        titulo.localScale = Vector3.one;
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
