using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Música de fondo para todo el juego. Se crea sola al arrancar y no se destruye al cambiar de escena,
/// así la música del menú sigue sonando sin cortes por todas sus pantallas.
/// Al elegir un mapa cambia (con fundido) a la música de ese mapa.
/// Las pistas están en Assets/Resources/IslandPutt/Musica/.
/// El slider "Music" del menú de opciones controla el volumen (se guarda entre partidas).
/// </summary>
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instancia { get; private set; }

    const string PrefVolumen = "volumenMusica";
    const float VolumenPorDefecto = 0.6f;
    const float DuracionFundido = 1.5f;

    AudioSource a, b, actual;
    string pistaActual;
    float volumen;
    Coroutine fundido;

    /// <summary>Qué pista suena en cada escena.</summary>
    public static string PistaPara(string escena)
    {
        switch (escena)
        {
            case "IslandDesert": return "IslandPutt/Musica/Desierto";
            default: return "IslandPutt/Musica/Menu"; // MainMenu y cualquier otra pantalla
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Crear()
    {
        if (Instancia != null) return;
        var go = new GameObject("Musica");
        DontDestroyOnLoad(go);
        Instancia = go.AddComponent<MusicManager>();
    }

    void Awake()
    {
        a = NuevaFuente();
        b = NuevaFuente();
        actual = a;
        volumen = PlayerPrefs.GetFloat(PrefVolumen, VolumenPorDefecto);
        SceneManager.sceneLoaded += AlCargarEscena;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= AlCargarEscena;
        if (Instancia == this) Instancia = null;
    }

    AudioSource NuevaFuente()
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.loop = true;
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        s.priority = 0;
        s.volume = 0f;
        return s;
    }

    void AlCargarEscena(Scene escena, LoadSceneMode modo)
    {
        Reproducir(PistaPara(escena.name));
        EnlazarSlider();
    }

    /// <summary>Llamar al elegir un mapa: la música cambia ya, durante el fundido a negro.</summary>
    public void PrepararEscena(string escena) => Reproducir(PistaPara(escena));

    public void Reproducir(string pista)
    {
        if (pista == pistaActual) return;
        var clip = Resources.Load<AudioClip>(pista);
        if (clip == null) { Debug.LogWarning("[IslandPutt] No encuentro la música " + pista); return; }
        pistaActual = pista;

        var vieja = actual;
        var nueva = actual == a ? b : a;
        nueva.clip = clip;
        nueva.volume = 0f;
        nueva.Play();
        actual = nueva;

        if (fundido != null) StopCoroutine(fundido);
        fundido = StartCoroutine(Fundir(vieja, nueva));
    }

    IEnumerator Fundir(AudioSource sale, AudioSource entra)
    {
        float inicioSale = sale.volume;
        float t = 0f;
        while (t < DuracionFundido)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / DuracionFundido);
            sale.volume = inicioSale * (1f - k);
            entra.volume = volumen * k;
            yield return null;
        }
        sale.Stop();
        sale.volume = 0f;
        entra.volume = volumen;
        fundido = null;
    }

    public float Volumen
    {
        get => volumen;
        set
        {
            volumen = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(PrefVolumen, volumen);
            if (fundido == null && actual != null) actual.volume = volumen;
        }
    }

    // Conecta el slider "Music" del menú de opciones (aunque esté oculto)
    void EnlazarSlider()
    {
        foreach (var s in Resources.FindObjectsOfTypeAll<Slider>())
        {
            if (s.name != "Music" || !s.gameObject.scene.IsValid()) continue;
            s.minValue = 0f;
            s.maxValue = 1f;
            s.SetValueWithoutNotify(volumen);
            s.onValueChanged.RemoveListener(AlCambiarSlider);
            s.onValueChanged.AddListener(AlCambiarSlider);
        }
    }

    void AlCambiarSlider(float v) => Volumen = v;
}
