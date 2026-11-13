using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Entrada con rebote.</summary>
public class Aparecer : MonoBehaviour
{
    public float retraso;
    float t;
    void Update()
    {
        t += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01((t - retraso) / 0.5f);
        float s = k <= 0f ? 0f : (k >= 1f ? 1f : Mathf.Pow(2f, -10f * k) * Mathf.Sin((k * 10f - 0.75f) * (2f * Mathf.PI / 3f)) + 1f);
        transform.localScale = Vector3.one * s;
        if (k >= 1f) Destroy(this);
    }
}
