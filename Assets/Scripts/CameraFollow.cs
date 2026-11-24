using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Cámara orbital suave alrededor de la pelota.
///  - Clic derecho + ratón: girar
///  - A/D: girar · W/S: subir/bajar
///  - Rueda: zoom
///  - F: vista libre (WASD volar, Q/E bajar/subir, Shift rápido). F otra vez: volver a la pelota
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public static bool VistaLibreActiva { get; private set; }
    static CameraFollow instancia;

    /// <summary>Sacudida de cámara (0-1). La usa el HUD al golpear, rebotar y meter el hoyo.</summary>
    public static void Shake(float fuerza)
    {
        if (instancia == null) return;
        instancia.shake = Mathf.Max(instancia.shake, fuerza);
        instancia.fovKick = Mathf.Max(instancia.fovKick, fuerza * 6f);
    }

    [Header("Objetivo")]
    public Transform target;
    public float targetHeight = 0.5f;

    [Header("Giro")]
    [Tooltip("Grados por píxel con el clic derecho")]
    public float sensibilidad = 0.18f;
    [Tooltip("Grados por segundo con A/D y W/S")]
    public float velocidadTeclas = 100f;
    [Tooltip("Suavidad del giro (más alto = responde más rápido)")]
    public float respuestaGiro = 14f;
    public float inclinacionInicial = 28f;
    public float inclinacionMin = 8f;
    public float inclinacionMax = 80f;
    public bool invertirY = false;

    [Header("Zoom")]
    public float distanciaInicial = 15f;
    public float distanciaMin = 4f;
    public float distanciaMax = 55f;
    [Tooltip("Cuánto se acerca por paso de rueda (0.15 = 15%)")]
    public float pasoZoom = 0.15f;
    public float respuestaZoom = 10f;

    [Header("Seguimiento")]
    [Tooltip("Segundos de retraso al seguir la pelota (0 = pegada)")]
    public float retrasoSeguir = 0.06f;

    [Header("Paredes")]
    public bool evitarParedes = true;
    public float radioColision = 0.3f;

    [Header("Vista libre (F)")]
    public float velocidadVuelo = 30f;
    public float multiplicadorRapido = 3f;

    float yaw, pitch, yawT, pitchT;
    float dist, distT, alturaOrbita;
    readonly RaycastHit[] impactos = new RaycastHit[16];
    Vector3 pivot, pivotVel;
    bool libre;
    float shake, fovKick, fovBase = 60f;
    Camera cam;

    void Awake() { instancia = this; cam = GetComponent<Camera>(); if (cam != null) fovBase = cam.fieldOfView; }
    void OnDisable() { VistaLibreActiva = false; }

    void Start()
    {
        yaw = yawT = transform.eulerAngles.y;
        pitch = pitchT = inclinacionInicial;
        dist = distT = distanciaInicial;
        if (target != null) pivot = target.position + Vector3.up * targetHeight;
    }

    void LateUpdate()
    {
        if (Time.timeScale == 0f) return;
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;
        float dt = Time.unscaledDeltaTime;

        if (kb.fKey.wasPressedThisFrame) SetLibre(!libre);

        // ---- Entrada de giro
        if (mouse.rightButton.isPressed)
        {
            Vector2 d = mouse.delta.ReadValue();
            yawT += d.x * sensibilidad;
            pitchT += (invertirY ? d.y : -d.y) * sensibilidad;
        }

        if (libre) { VistaLibre(kb, mouse, dt); Efectos(dt); return; }

        float h = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        float v = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
        yawT -= h * velocidadTeclas * dt;
        pitchT += v * velocidadTeclas * 0.6f * dt;
        pitchT = Mathf.Clamp(pitchT, inclinacionMin, inclinacionMax);

        // ---- Zoom: la rueda en Windows da 120 por paso y en Mac valores pequeños; se normaliza
        float s = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(s) > 20f) s /= 120f;
        if (Mathf.Abs(s) > 0.001f)
            distT = Mathf.Clamp(distT * Mathf.Pow(1f - pasoZoom, Mathf.Clamp(s, -3f, 3f)), distanciaMin, distanciaMax);

        // ---- Suavizado
        float kGiro = 1f - Mathf.Exp(-respuestaGiro * dt);
        yaw = Mathf.LerpAngle(yaw, yawT, kGiro);
        pitch = Mathf.Lerp(pitch, pitchT, kGiro);
        dist = Mathf.Lerp(dist, distT, 1f - Mathf.Exp(-respuestaZoom * dt));

        if (target == null) return;

        Vector3 objetivo = target.position + Vector3.up * targetHeight;
        if ((objetivo - pivot).sqrMagnitude > 30f * 30f || retrasoSeguir <= 0f) { pivot = objetivo; pivotVel = Vector3.zero; }
        else pivot = Vector3.SmoothDamp(pivot, objetivo, ref pivotVel, retrasoSeguir, Mathf.Infinity, dt);

        // Conserva la distancia elegida y eleva la órbita al encontrar una pared.
        // Al despejarse la vista, recupera suavemente la inclinación del jugador.
        float angulo = pitch;
        float alcance = dist;
        if (evitarParedes)
        {
            alcance = AlcanceVisible(angulo);
            while (alcance < dist && angulo < 89f)
            {
                angulo = Mathf.Min(angulo + 5f, 89f);
                alcance = AlcanceVisible(angulo);
            }
        }
        float descenso = Mathf.MoveTowards(alturaOrbita, angulo, 75f * dt);
        if (evitarParedes && descenso > angulo && AlcanceVisible(descenso) >= dist)
        {
            angulo = descenso;
            alcance = dist;
        }
        alturaOrbita = angulo;
        var rot = Quaternion.Euler(angulo, yaw, 0f);
        transform.SetPositionAndRotation(pivot + rot * Vector3.back * alcance, rot);
        Efectos(dt);
    }

    float AlcanceVisible(float angulo)
    {
        Vector3 direccion = Quaternion.Euler(angulo, yaw, 0f) * Vector3.back;
        int count = Physics.SphereCastNonAlloc(pivot, radioColision, direccion, impactos,
            dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float alcance = dist;
        for (int i = 0; i < count; i++)
        {
            var hit = impactos[i];
            if (hit.collider.transform.IsChildOf(target)) continue;
            alcance = Mathf.Min(alcance, Mathf.Max(0.01f, hit.distance - 0.05f));
        }
        return alcance;
    }

    void Efectos(float dt)
    {
        if (shake > 0.001f)
        {
            transform.position += (Vector3)(Random.insideUnitCircle * shake * 0.35f) + Random.insideUnitSphere * shake * 0.1f;
            shake *= Mathf.Exp(-7f * dt);
        }
        if (cam != null)
        {
            fovKick *= Mathf.Exp(-6f * dt);
            cam.fieldOfView = fovBase + fovKick;
        }
    }

    void VistaLibre(Keyboard kb, Mouse mouse, float dt)
    {
        pitchT = Mathf.Clamp(pitchT, -89f, 89f);
        float k = 1f - Mathf.Exp(-respuestaGiro * dt);
        yaw = Mathf.LerpAngle(yaw, yawT, k);
        pitch = Mathf.Lerp(pitch, pitchT, k);
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 move = Vector3.zero;
        if (kb.wKey.isPressed) move += Vector3.forward;
        if (kb.sKey.isPressed) move += Vector3.back;
        if (kb.aKey.isPressed) move += Vector3.left;
        if (kb.dKey.isPressed) move += Vector3.right;
        Vector3 world = rot * move;
        if (kb.eKey.isPressed) world += Vector3.up;
        if (kb.qKey.isPressed) world += Vector3.down;

        float s = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(s) > 20f) s /= 120f;
        velocidadVuelo = Mathf.Clamp(velocidadVuelo * Mathf.Pow(1.15f, Mathf.Clamp(s, -3f, 3f)), 3f, 300f);

        float vel = velocidadVuelo * (kb.leftShiftKey.isPressed ? multiplicadorRapido : 1f);
        transform.SetPositionAndRotation(transform.position + world.normalized * vel * dt, rot);
    }

    void SetLibre(bool on)
    {
        libre = on;
        VistaLibreActiva = on;
        if (on || target == null) return;

        // Al volver: mirar a la pelota desde donde estabas, sin saltos
        Vector3 piv = target.position + Vector3.up * targetHeight;
        Vector3 dir = transform.position - piv;
        if (dir.sqrMagnitude < 0.01f) return;
        Vector3 e = Quaternion.LookRotation(-dir).eulerAngles;
        yaw = yawT = e.y;
        pitch = pitchT = Mathf.Clamp(e.x > 180f ? e.x - 360f : e.x, inclinacionMin, inclinacionMax);
        dist = distT = Mathf.Clamp(dir.magnitude, distanciaMin, distanciaMax);
        pivot = piv;
        pivotVel = Vector3.zero;
    }
}
