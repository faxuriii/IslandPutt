using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Tarjeta de mapa: crece al pasar el ratón, salta al pulsar, tiembla si está bloqueada.</summary>
public class TarjetaMapa : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    MapSelector sel;
    MapSelector.Mapa mapa;
    bool hover;
    float shake;
    float tilt;

    public void Init(MapSelector s, MapSelector.Mapa m) { sel = s; mapa = m; }

    public void OnPointerEnter(PointerEventData e) => hover = true;
    public void OnPointerExit(PointerEventData e) => hover = false;

    public void OnPointerClick(PointerEventData e)
    {
        if (mapa.disponible)
        {
            transform.localScale = Vector3.one * 1.18f;
            sel.Elegir(mapa);
        }
        else shake = 0.4f;
    }

    void Update()
    {
        if (GetComponent<Aparecer>() != null) return; // aún entrando
        float dt = Time.unscaledDeltaTime;
        float objetivo = hover ? (mapa.disponible ? 1.07f : 1.02f) : 1f;
        transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * objetivo, 1f - Mathf.Exp(-12f * dt));
        tilt = Mathf.Lerp(tilt, hover && mapa.disponible ? -2.5f : 0f, 1f - Mathf.Exp(-10f * dt));
        float sx = 0f;
        if (shake > 0f) { shake -= dt; sx = Mathf.Sin(Time.unscaledTime * 60f) * 12f * shake; }
        transform.localRotation = Quaternion.Euler(0, 0, tilt + sx * 0.3f);
    }
}
