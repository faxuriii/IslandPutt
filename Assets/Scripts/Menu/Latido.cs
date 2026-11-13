using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Latido suave (el "?" del mapa bloqueado).</summary>
public class Latido : MonoBehaviour
{
    void Update() => transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 3f) * 0.08f);
}
