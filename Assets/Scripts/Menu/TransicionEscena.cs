using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class TransicionEscena : MonoBehaviour
{
    static TransicionEscena actual;
    public static bool Activa => actual != null;
    public static void Cargar(string escena)
    {
        if (Activa) return;
        var raiz = new GameObject("Transición", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        DontDestroyOnLoad(raiz);
        actual = raiz.AddComponent<TransicionEscena>();
        actual.StartCoroutine(actual.Cambiar(escena));
    }

    IEnumerator Cambiar(string escena)
    {
        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        var sombra = new GameObject("Fundido", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        var rect = sombra.rectTransform;
        rect.SetParent(transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var sistema = EventSystem.current;
        bool navegacion = sistema != null && sistema.sendNavigationEvents;
        if (sistema != null) sistema.sendNavigationEvents = false;
        yield return Fundir(sombra, 0, 1);
        if (sistema != null) sistema.sendNavigationEvents = navegacion;
        yield return SceneManager.LoadSceneAsync(escena);
        sistema = EventSystem.current;
        navegacion = sistema != null && sistema.sendNavigationEvents;
        if (sistema != null) sistema.sendNavigationEvents = false;
        yield return Fundir(sombra, 1, 0);
        if (sistema != null) sistema.sendNavigationEvents = navegacion;
        Destroy(gameObject);
    }

    static IEnumerator Fundir(Image imagen, float inicio, float fin)
    {
        float t = 0;
        imagen.color = new Color(0, 0, 0, inicio);
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / .24f);
            imagen.color = new Color(0, 0, 0, Mathf.SmoothStep(inicio, fin, t));
            yield return null;
        }
    }
}
