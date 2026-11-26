using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class Aparecer : MonoBehaviour
{
    CanvasGroup grupo;
    float t;
    void Awake() => grupo = GetComponent<CanvasGroup>();
    void OnEnable() { t = 0; grupo.alpha = 0; }
    void Update()
    {
        if (t >= 1f) return;
        t = Mathf.Min(1f, t + Time.unscaledDeltaTime / .2f);
        grupo.alpha = Mathf.SmoothStep(0, 1, t);
    }
}
