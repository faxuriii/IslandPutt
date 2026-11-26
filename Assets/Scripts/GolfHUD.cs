using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Interfaz y efectos del minigolf: barra de potencia animada, contador de golpes con "pop",
/// celebración al meter el hoyo (texto animado, confeti, sonidos) y efectos al golpear.
/// Lo crea MiniGolfShotController automáticamente; no hace falta ponerlo a mano.
/// Los sonidos se generan por código (no necesita archivos de audio).
/// </summary>
public class GolfHUD : MonoBehaviour
{
    public TMP_FontAsset fuente;
    public Gradient gradiente;
    [Range(0f, 1f)] public float volumen = 0.7f;

    public void CancelCharge()
    {
        charging = false;
        powerShown = 0f;
        ultimoTick = 0f;
    }

    // --- UI
    RectTransform powerPanel, powerFill, golpesPill, mensaje, subMensaje;
    Image powerFillImg, golpesImg;
    TextMeshProUGUI powerPct, powerLabel, golpesTxt, mensajeTxt, subMensajeTxt;
    CanvasGroup powerGroup;
    Canvas canvas;

    // --- estado
    float powerShown, powerAlpha, shakeT;
    bool charging;
    int par = 3;

    // --- audio / partículas
    AudioSource audioSrc;
    AudioClip clipGolpe, clipRebote, clipHoyo, clipFanfarria, clipMax, clipCarga;
    ParticleSystem confeti, polvo;
    Material matParticulas;
    float ultimoTick;

    static readonly Color Azul = new Color(0.16f, 0.45f, 0.95f);
    static readonly Color Verde = new Color(0.30f, 0.78f, 0.25f);
    static readonly Color Amarillo = new Color(1f, 0.80f, 0.15f);
    static readonly Color Rojo = new Color(0.92f, 0.25f, 0.20f);
    static readonly Color Oscuro = new Color(0.08f, 0.10f, 0.14f, 0.85f);

    public void Init(TMP_FontAsset font, Gradient grad, int parHoyo)
    {
        fuente = font;
        gradiente = grad;
        par = parHoyo;
        CrearUI();
        CrearAudio();
        CrearParticulas();
        SetGolpes(0, false);
    }

    void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
        if (confeti != null) Destroy(confeti.gameObject);
        if (polvo != null) Destroy(polvo.gameObject);
    }

    // =============================== API ===============================

    public void SetPower(float p, bool cargando)
    {
        if (cargando && !charging) { Punch(powerPanel, 0.12f); }
        charging = cargando;
        if (cargando)
        {
            // pequeño "tic" que sube de tono con la fuerza
            if (Mathf.Abs(p - ultimoTick) > 0.1f)
            {
                ultimoTick = p;
                audioSrc.pitch = 0.8f + p * 1.2f;
                audioSrc.PlayOneShot(clipCarga, volumen * 0.25f);
                audioSrc.pitch = 1f;
            }
            if (p >= 0.995f && powerShown < 0.995f) audioSrc.PlayOneShot(clipMax, volumen * 0.5f);
        }
        else ultimoTick = 0f;
        powerShown = cargando ? p : powerShown;
    }

    public void OnShot(float p, int golpes, Vector3 pos, float radio)
    {
        audioSrc.pitch = 0.9f + p * 0.3f;
        audioSrc.PlayOneShot(clipGolpe, volumen * Mathf.Lerp(0.4f, 1f, p));
        audioSrc.pitch = 1f;
        SetGolpes(golpes, true);
        Polvo(pos + Vector3.down * radio * 0.8f, radio, Mathf.Lerp(6, 22, p));
        CameraFollow.Shake(p * p * 0.9f);
        StartCoroutine(OcultarBarra());
        FloatingText("+1", golpesPill, ColorGolpes(golpes));
    }

    public void OnBounce(float fuerza)
    {
        audioSrc.pitch = Random.Range(0.9f, 1.15f);
        audioSrc.PlayOneShot(clipRebote, volumen * Mathf.Clamp01(fuerza / 25f));
        audioSrc.pitch = 1f;
        if (fuerza > 15f) CameraFollow.Shake(Mathf.Clamp01(fuerza / 60f) * 0.5f);
    }

    public string OnHole(int golpes, Vector3 hoyoPos, float radio)
    {
        audioSrc.PlayOneShot(clipHoyo, volumen);
        StartCoroutine(Fanfarria(0.25f));
        Confeti(hoyoPos, radio);
        CameraFollow.Shake(0.5f);

        string titulo, sub;
        Color c;
        int dif = golpes - par;
        if (golpes == 1) { titulo = "¡HOYO EN UNO!"; c = Amarillo; }
        else if (dif <= -3) { titulo = "¡ALBATROS!"; c = Amarillo; }
        else if (dif == -2) { titulo = "¡EAGLE!"; c = Amarillo; }
        else if (dif == -1) { titulo = "¡BIRDIE!"; c = Verde; }
        else if (dif == 0) { titulo = "¡PAR!"; c = Azul; }
        else if (dif == 1) { titulo = "BOGEY"; c = new Color(1f, 0.55f, 0.2f); }
        else if (dif == 2) { titulo = "DOBLE BOGEY"; c = Rojo; }
        else { titulo = "+" + dif; c = Rojo; }
        sub = golpes == 1 ? "1 GOLPE" : golpes + " GOLPES";

        StopCoroutine(nameof(AnimMensaje));
        StartCoroutine(AnimMensaje(titulo, sub, c));
        return titulo;
    }

    TextMeshProUGUI turnoTexto;
    public void MostrarTurno(int jugador)
    {
        if (turnoTexto == null)
        {
            var anchor = new Vector2(0, 1);
            var panel = Rect("Turno", canvas.transform, anchor, anchor, anchor,
                new Vector2(48, -40), new Vector2(430, 72));
            panel.gameObject.AddComponent<Image>().color = Oscuro;
            turnoTexto = Texto("Jugador", panel, "", 34, Color.white, TextAlignmentOptions.Center);
            Fill(turnoTexto.rectTransform, Vector2.zero, Vector2.one);
        }
        turnoTexto.text = "TURNO · " + MapSelector.NombreJugador(jugador);
        turnoTexto.richText = false;
        turnoTexto.enableAutoSizing = true;
        turnoTexto.fontSizeMin = 20;
        turnoTexto.fontSizeMax = 34;
        turnoTexto.color = Color.Lerp(MapSelector.ColorJugador(jugador), Color.white, .3f);
    }

    public void OnReset()
    {
        SetGolpes(0, false);
        mensaje.gameObject.SetActive(false);
        subMensaje.gameObject.SetActive(false);
        powerShown = 0f;
    }

    // =============================== Update ===============================

    void Update()
    {
        if (powerPanel == null || Time.timeScale == 0f) return;
        float dt = Time.unscaledDeltaTime;

        // Barra visible al cargar, se desvanece después
        float targetAlpha = charging ? 1f : (powerShown > 0.001f ? powerAlpha : 0.35f);
        powerGroup.alpha = Mathf.Lerp(powerGroup.alpha, charging ? 1f : targetAlpha, 1f - Mathf.Exp(-10f * dt));

        float p = powerShown;
        powerFill.anchorMax = new Vector2(Mathf.Max(p, 0.001f), 1f);
        powerFillImg.color = gradiente.Evaluate(p);
        powerPct.text = Mathf.RoundToInt(p * 100f) + "%";
        powerPct.color = Color.Lerp(Color.white, gradiente.Evaluate(1f), p * p);

        // Al máximo: tiembla y avisa
        bool max = charging && p >= 0.97f;
        powerLabel.text = max ? "¡MÁXIMO!" : "POTENCIA";
        powerLabel.color = max ? Color.Lerp(Amarillo, Rojo, Mathf.PingPong(Time.time * 6f, 1f)) : Color.white;
        float shake = charging ? Mathf.Pow(p, 3f) * 7f : 0f;
        powerPanel.anchoredPosition = new Vector2(60, 150) + Random.insideUnitCircle * shake;
        float s = charging ? 1f + p * 0.08f : 1f;
        powerPanel.localScale = Vector3.Lerp(powerPanel.localScale, Vector3.one * s, 1f - Mathf.Exp(-15f * dt));

        // Vuelta a la escala normal de los elementos con "pop"
        golpesPill.localScale = Vector3.Lerp(golpesPill.localScale, Vector3.one, 1f - Mathf.Exp(-9f * dt));
    }

    IEnumerator OcultarBarra()
    {
        powerAlpha = 1f;
        yield return new WaitForSeconds(0.8f);
        float t = 0f;
        while (t < 0.6f) { t += Time.deltaTime; powerAlpha = 1f - t / 0.6f; yield return null; }
        powerShown = 0f;
        powerAlpha = 0f;
    }

    // =============================== UI ===============================

    public void SetGolpes(int g, bool animar)
    {
        golpesTxt.text = "TOQUES  " + g;
        golpesImg.color = ColorGolpes(g);
        if (animar) Punch(golpesPill, 0.45f);
    }

    Color ColorGolpes(int g)
    {
        if (g == 0) return Azul;
        if (g < par) return Verde;
        if (g == par) return Amarillo;
        return Rojo;
    }

    void Punch(RectTransform rt, float amount)
    {
        if (rt != null) rt.localScale = Vector3.one * (1f + amount);
    }

    void CrearUI()
    {
        var root = new GameObject("GolfUI");
        canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // ---- Barra de potencia (abajo a la izquierda)
        powerPanel = Rect("Potencia", root.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
                          new Vector2(60, 150), new Vector2(520, 110));
        powerGroup = powerPanel.gameObject.AddComponent<CanvasGroup>();
        powerGroup.alpha = 0.35f;

        powerLabel = Texto("Label", powerPanel, "POTENCIA", 44, Color.white, TextAlignmentOptions.BottomLeft);
        Fill(powerLabel.rectTransform, new Vector2(0, 0.55f), new Vector2(0.7f, 1f));
        powerPct = Texto("Pct", powerPanel, "0%", 44, Color.white, TextAlignmentOptions.BottomRight);
        Fill(powerPct.rectTransform, new Vector2(0.6f, 0.55f), new Vector2(1f, 1f));

        var barBg = Rect("Fondo", powerPanel, Vector2.zero, new Vector2(1, 0.5f), Vector2.zero, Vector2.zero, Vector2.zero);
        var bgImg = barBg.gameObject.AddComponent<Image>();
        bgImg.color = Oscuro;
        barBg.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f);

        var fillArea = Rect("Area", barBg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Vector2.zero);
        fillArea.offsetMin = new Vector2(6, 6); fillArea.offsetMax = new Vector2(-6, -6);
        powerFill = Rect("Relleno", fillArea, Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero, Vector2.zero);
        powerFillImg = powerFill.gameObject.AddComponent<Image>();

        // marcas cada 25%
        for (int i = 1; i < 4; i++)
        {
            var m = Rect("Marca", fillArea, new Vector2(i * 0.25f, 0), new Vector2(i * 0.25f, 1), new Vector2(0.5f, 0.5f),
                         Vector2.zero, new Vector2(3, 0));
            m.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, 0.25f);
        }

        golpesPill = Rect("Golpes", root.transform, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(180, 88), new Vector2(240, 76));
        golpesImg = golpesPill.gameObject.AddComponent<Image>();
        golpesTxt = Texto("T", golpesPill, "TOQUES  0", 32, Color.white, TextAlignmentOptions.Center);
        Fill(golpesTxt.rectTransform, Vector2.zero, Vector2.one);

        // ---- Mensaje grande central
        mensaje = Rect("Mensaje", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                       new Vector2(0, 90), new Vector2(1600, 220));
        mensajeTxt = Texto("T", mensaje, "", 150, Amarillo, TextAlignmentOptions.Center);
        Fill(mensajeTxt.rectTransform, Vector2.zero, Vector2.one);
        mensaje.gameObject.SetActive(false);

        subMensaje = Rect("Sub", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                          new Vector2(0, -40), new Vector2(1000, 90));
        subMensajeTxt = Texto("T", subMensaje, "", 64, Color.white, TextAlignmentOptions.Center);
        Fill(subMensajeTxt.rectTransform, Vector2.zero, Vector2.one);
        subMensaje.gameObject.SetActive(false);
    }

    IEnumerator AnimMensaje(string titulo, string sub, Color c)
    {
        mensajeTxt.text = titulo;
        mensajeTxt.color = c;
        subMensajeTxt.text = sub;
        mensaje.gameObject.SetActive(true);
        subMensaje.gameObject.SetActive(false);

        // Entrada con rebote y giro
        float t = 0f;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / 0.6f);
            float s = ElasticOut(k);
            mensaje.localScale = Vector3.one * s;
            mensaje.localRotation = Quaternion.Euler(0, 0, (1f - k) * -18f);
            yield return null;
        }
        mensaje.localScale = Vector3.one;
        mensaje.localRotation = Quaternion.identity;

        subMensaje.gameObject.SetActive(true);
        subMensaje.localScale = Vector3.one * 1.4f;

        // Latido mientras se muestra
        float end = Time.time + 10f;
        while (mensaje.gameObject.activeSelf && Time.time < end)
        {
            float b = 1f + Mathf.Sin(Time.time * 6f) * 0.04f;
            mensaje.localScale = Vector3.one * b;
            mensaje.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 3f) * 3f);
            subMensaje.localScale = Vector3.Lerp(subMensaje.localScale, Vector3.one, 1f - Mathf.Exp(-10f * Time.deltaTime));
            yield return null;
        }
    }

    void FloatingText(string txt, RectTransform cerca, Color c)
    {
        var rt = Rect("Flotante", canvas.transform, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f),
                      cerca.anchoredPosition + new Vector2(0, 40), new Vector2(200, 80));
        var t = Texto("T", rt, txt, 56, c, TextAlignmentOptions.Center);
        Fill(t.rectTransform, Vector2.zero, Vector2.one);
        StartCoroutine(AnimFlotante(rt, t));
    }

    IEnumerator AnimFlotante(RectTransform rt, TextMeshProUGUI t)
    {
        Vector2 start = rt.anchoredPosition;
        float d = 0f;
        while (d < 0.9f)
        {
            d += Time.deltaTime;
            float k = d / 0.9f;
            rt.anchoredPosition = start + new Vector2(0, k * 90f);
            rt.localScale = Vector3.one * (k < 0.15f ? Mathf.Lerp(0.5f, 1.3f, k / 0.15f) : Mathf.Lerp(1.3f, 1f, (k - 0.15f) / 0.85f));
            var col = t.color; col.a = 1f - k * k; t.color = col;
            yield return null;
        }
        Destroy(rt.gameObject);
    }

    static float ElasticOut(float x)
    {
        if (x <= 0f) return 0f;
        if (x >= 1f) return 1f;
        const float c4 = (2f * Mathf.PI) / 3f;
        return Mathf.Pow(2f, -10f * x) * Mathf.Sin((x * 10f - 0.75f) * c4) + 1f;
    }

    RectTransform Rect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return rt;
    }

    static void Fill(RectTransform rt, Vector2 aMin, Vector2 aMax)
    {
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    TextMeshProUGUI Texto(string name, Transform parent, string s, float size, Color c, TextAlignmentOptions al)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (fuente != null) t.font = fuente;
        t.text = s; t.fontSize = size; t.color = c; t.alignment = al;
        t.fontStyle = FontStyles.Bold;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        var sh = go.AddComponent<Shadow>();
        sh.effectColor = new Color(0, 0, 0, 0.75f);
        sh.effectDistance = new Vector2(3, -3);
        return t;
    }

    // =============================== Audio (generado) ===============================

    void CrearAudio()
    {
        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;
        audioSrc.spatialBlend = 0f;

        const int SR = 44100;
        // Golpe: "toc" seco (seno que cae de tono + clic de ruido)
        clipGolpe = Sintetizar("Golpe", 0.16f, SR, (t, r) =>
            Mathf.Sin(2 * Mathf.PI * Mathf.Lerp(900, 180, t / 0.16f) * t) * Mathf.Exp(-t * 32f) * 0.9f
            + (float)(r.NextDouble() * 2 - 1) * Mathf.Exp(-t * 180f) * 0.6f);
        // Rebote en pared: "tic" de madera
        clipRebote = Sintetizar("Rebote", 0.08f, SR, (t, r) =>
            (Mathf.Sin(2 * Mathf.PI * 1250 * t) * 0.6f + Mathf.Sin(2 * Mathf.PI * 2300 * t) * 0.3f) * Mathf.Exp(-t * 70f)
            + (float)(r.NextDouble() * 2 - 1) * Mathf.Exp(-t * 300f) * 0.3f);
        // Hoyo: "plop" que cae dentro de la copa
        clipHoyo = Sintetizar("Hoyo", 0.35f, SR, (t, r) =>
            Mathf.Sin(2 * Mathf.PI * Mathf.Lerp(520, 140, Mathf.Sqrt(t / 0.35f)) * t) * Mathf.Exp(-t * 9f)
            + Mathf.Sin(2 * Mathf.PI * 330 * (t - 0.12f)) * (t > 0.12f ? Mathf.Exp(-(t - 0.12f) * 25f) : 0f) * 0.5f);
        // Fanfarria: arpegio Do-Mi-Sol-Do
        float[] notas = { 523.25f, 659.25f, 783.99f, 1046.5f };
        clipFanfarria = Sintetizar("Fanfarria", 1.3f, SR, (t, r) =>
        {
            float v = 0f;
            for (int i = 0; i < notas.Length; i++)
            {
                float t0 = i * 0.12f;
                if (t < t0) continue;
                float lt = t - t0;
                float env = Mathf.Min(1f, lt * 60f) * Mathf.Exp(-lt * (i == 3 ? 2.5f : 6f));
                v += (Mathf.Sin(2 * Mathf.PI * notas[i] * lt) + 0.3f * Mathf.Sin(4 * Mathf.PI * notas[i] * lt)) * env * 0.35f;
            }
            return v;
        });
        clipMax = Sintetizar("Max", 0.12f, SR, (t, r) =>
            Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 1568 * t)) * Mathf.Exp(-t * 30f) * 0.25f);
        clipCarga = Sintetizar("Carga", 0.03f, SR, (t, r) =>
            Mathf.Sin(2 * Mathf.PI * 880 * t) * Mathf.Exp(-t * 150f) * 0.5f);
    }

    IEnumerator Fanfarria(float delay)
    {
        yield return new WaitForSeconds(delay);
        audioSrc.PlayOneShot(clipFanfarria, volumen * 0.8f);
    }

    static AudioClip Sintetizar(string name, float dur, int sr, System.Func<float, System.Random, float> f)
    {
        int n = Mathf.CeilToInt(dur * sr);
        var data = new float[n];
        var r = new System.Random(name.GetHashCode());
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float fade = Mathf.Clamp01((n - i) / (sr * 0.005f)); // evita "clic" al final
            data[i] = Mathf.Clamp(f(t, r), -1f, 1f) * fade;
        }
        var clip = AudioClip.Create(name, n, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }

    // =============================== Partículas ===============================

    void CrearParticulas()
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        matParticulas = new Material(sh);

        // Confeti de colores
        confeti = NuevoSistema("Confeti");
        var main = confeti.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 18f);
        main.gravityModifier = 1.1f;
        main.startRotation3D = true;
        main.startRotationX = main.startRotationY = main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        var colores = new Gradient();
        colores.SetKeys(new[] {
                new GradientColorKey(new Color(1f, 0.25f, 0.3f), 0f), new GradientColorKey(new Color(1f, 0.8f, 0.1f), 0.25f),
                new GradientColorKey(new Color(0.3f, 0.85f, 0.3f), 0.5f), new GradientColorKey(new Color(0.2f, 0.55f, 1f), 0.75f),
                new GradientColorKey(new Color(0.85f, 0.3f, 1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        main.startColor = new ParticleSystem.MinMaxGradient(colores) { mode = ParticleSystemGradientMode.RandomColor };
        var shape = confeti.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 30f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        var rot = confeti.rotationOverLifetime;
        rot.enabled = true; rot.separateAxes = true;
        rot.x = rot.y = rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
        var drag = confeti.limitVelocityOverLifetime;
        drag.enabled = true; drag.drag = 1.2f;
        var col = confeti.colorOverLifetime;
        col.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        col.color = fade;
        confeti.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.Billboard;

        // Polvo/hierba al golpear
        polvo = NuevoSistema("Polvo");
        var pm = polvo.main;
        pm.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        pm.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
        pm.gravityModifier = 0.6f;
        pm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.95f, 1f, 0.9f), new Color(0.55f, 0.85f, 0.4f));
        var ps = polvo.shape;
        ps.shapeType = ParticleSystemShapeType.Hemisphere;
        ps.rotation = new Vector3(-90f, 0f, 0f);
        var pc = polvo.colorOverLifetime;
        pc.enabled = true;
        pc.color = fade;
        var size = polvo.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
    }

    ParticleSystem NuevoSistema(string name)
    {
        var go = new GameObject(name);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 400;
        var em = ps.emission;
        em.rateOverTime = 0f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = matParticulas;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return ps;
    }

    void Confeti(Vector3 pos, float radio)
    {
        confeti.transform.position = pos;
        var main = confeti.main;
        main.startSize = new ParticleSystem.MinMaxCurve(radio * 0.25f, radio * 0.5f);
        confeti.Emit(160);
    }

    void Polvo(Vector3 pos, float radio, float cantidad)
    {
        polvo.transform.position = pos;
        var main = polvo.main;
        main.startSize = new ParticleSystem.MinMaxCurve(radio * 0.15f, radio * 0.35f);
        polvo.Emit(Mathf.RoundToInt(cantidad));
    }
}
