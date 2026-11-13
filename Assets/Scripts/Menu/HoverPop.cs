using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Pequeño crecimiento al pasar el ratón (botones).</summary>
public class HoverPop : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    bool hover;
    public void OnPointerEnter(PointerEventData e) => hover = true;
    public void OnPointerExit(PointerEventData e) => hover = false;
    void Update()
    {
        float o = hover ? 1.1f : 1f;
        transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * o, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
    }
}
