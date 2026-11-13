using UnityEngine;

/// <summary>
/// Detecta cuando la pelota entra en el hoyo.
/// Va en el objeto "Hoyo" (su posición es el centro del agujero, a ras del césped).
/// El trigger se crea solo si no existe.
/// </summary>
public class GolfHole : MonoBehaviour
{
    [Tooltip("Radio del agujero en metros")]
    public float radioHoyo = 1.6f;
    [Tooltip("Si la pelota pasa más rápido que esto, no entra (rebota por encima)")]
    public float velocidadMaxima = 7f;

    void Awake()
    {
        var sc = GetComponent<SphereCollider>();
        if (sc == null) sc = gameObject.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = radioHoyo;
        sc.center = new Vector3(0f, -radioHoyo * 0.5f, 0f);
    }

    void OnTriggerStay(Collider other)
    {
        var rb = other.attachedRigidbody;
        if (rb == null) return;
        var ball = rb.GetComponent<MiniGolfShotController>();
        if (ball == null || ball.Embocada) return;

        Vector3 p = rb.position;
        Vector2 flat = new Vector2(p.x - transform.position.x, p.z - transform.position.z);

        // Dentro del agujero: centrada, por debajo del césped y no demasiado rápida
        bool dentro = flat.magnitude < radioHoyo * 0.85f &&
                      p.y < transform.position.y + ball.Radio * 0.6f &&
                      rb.linearVelocity.magnitude < velocidadMaxima;

        if (dentro) ball.Embocar(transform);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, radioHoyo);
    }
}
