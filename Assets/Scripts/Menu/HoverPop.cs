using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class HoverPop : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, ISubmitHandler
{
    Button button;
    Vector3 reposo;
    bool encima, pulsado;
    float finPulso;

    public static void Instalar(Button button)
    {
        if (button.TryGetComponent<Animator>(out var animator))
        {
            animator.enabled = false;
            button.transition = Selectable.Transition.ColorTint;
        }
        if (!button.TryGetComponent<HoverPop>(out _)) button.gameObject.AddComponent<HoverPop>();
    }

    void Awake() { button = GetComponent<Button>(); reposo = transform.localScale; }
    void OnDisable() { encima = pulsado = false; finPulso = 0; transform.localScale = reposo; }
    public void OnPointerEnter(PointerEventData e) => encima = true;
    public void OnPointerExit(PointerEventData e) => encima = false;
    public void OnPointerDown(PointerEventData e) => pulsado = e.button == PointerEventData.InputButton.Left;
    public void OnPointerUp(PointerEventData e) => pulsado = false;
    public void OnSubmit(BaseEventData e) => finPulso = Time.unscaledTime + .09f;

    void Update()
    {
        bool foco = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
        float escala = 1f;
        if (button.IsInteractable())
        {
            if (pulsado || Time.unscaledTime < finPulso) escala = .96f;
            else if (encima || foco) escala = 1.045f;
        }
        transform.localScale = Vector3.Lerp(transform.localScale, reposo * escala,
            1f - Mathf.Exp(-22f * Time.unscaledDeltaTime));
    }
}
