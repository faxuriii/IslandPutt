using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Control de tiro estilo minigolf.
///  - Apuntar: mueve el ratón (la raya apunta hacia el cursor) o usa las flechas ← →.
///  - Cargar:  mantén clic izquierdo y arrastra el ratón hacia atrás (como Golf It).
///             Con Espacio la barra sube y baja sola.
///  - Tirar:   suelta el botón en el momento que quieras.
///  - Cancelar: clic derecho o Escape mientras cargas.
///  - Reiniciar el hoyo: R.
/// Pon este script en la PELOTA (necesita Rigidbody + SphereCollider).
/// La flecha y la barra de potencia se crean automáticamente al darle a Play.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class MiniGolfShotController : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Cámara usada para apuntar con el ratón. Si está vacío usa Camera.main")]
    public Camera cam;

    [Header("Apuntado")]
    [Tooltip("Grados por segundo al girar con A/D o flechas")]
    public float keyboardAimSpeed = 90f;

    [Header("Fuerza del tiro (estilo Golf It)")]
    [Tooltip("Velocidad de la pelota al 100% de fuerza (m/s)")]
    public float velocidadTiroMax = 70f;
    [Tooltip("Velocidad con la fuerza mínima (m/s)")]
    public float velocidadTiroMin = 3f;
    [Tooltip(">1 = más control en tiros suaves (la fuerza crece más despacio al principio)")]
    public float curvaFuerza = 1.3f;
    [Tooltip("Píxeles que hay que arrastrar el ratón hacia atrás para llegar al 100%")]
    public float pixelesFuerzaMax = 250f;
    [Tooltip("Con Espacio: segundos en llenar la barra")]
    public float segundosBarraLlena = 1.2f;
    [Tooltip("Por debajo de esta fuerza (0-1) no se dispara")]
    public float fuerzaMinimaTiro = 0.02f;

    [Header("Rodar y frenar")]
    [Tooltip("Frenado constante sobre césped (m/s²). Más alto = se para antes")]
    public float deceleracionCesped = 5f;
    [Tooltip("Frenado sobre arena (m/s²)")]
    public float deceleracionArena = 12f;
    [Tooltip("Frenado extra al final del recorrido para que se pare limpia (m/s²)")]
    public float frenadoFinal = 2.5f;
    [Tooltip("Por debajo de esta velocidad empieza el frenado final (m/s)")]
    public float velocidadFrenadoFinal = 3f;
    [Tooltip("Por debajo de esta velocidad la pelota se para (m/s)")]
    public float velocidadParada = 0.35f;
    [Tooltip("En cuestas más inclinadas que esto la pelota sigue rodando (grados)")]
    public float pendienteMaxParada = 9f;
    [Tooltip("Resistencia del aire")]
    public float arrastreAire = 0.02f;
    [Tooltip("Masa de la pelota (kg)")]
    public float masa = 5f;
    [Tooltip("Gravedad extra: >1 la pelota pesa más, cae rápido y se pega al suelo")]
    public float multiplicadorGravedad = 2f;
    [Tooltip("En cuestas, cuánto del frenado se aplica (bajo = baja rodando por las rampas)")]
    public float frenadoEnCuesta = 0.15f;

    [Header("Recorrido")]
    [Tooltip("Punto de salida del hoyo. La pelota empieza aquí y vuelve aquí al reiniciar (R)")]
    public Transform salida;
    [Tooltip("Si la pelota cae por debajo de esta altura (Y mundo) vuelve a donde estaba")]
    public float alturaMuerte = -100f;
    [Tooltip("Golpes previstos para este hoyo (para BIRDIE, PAR, BOGEY...)")]
    public int par = 3;
    [Tooltip("Segundos que se muestra el mensaje de hoyo antes de reiniciar")]
    public float tiempoTrasHoyo = 4f;

    [Header("Flecha")]
    public float flechaLargoMin = 3f;   // en radios de pelota
    public float flechaLargoMax = 24f;     // en radios de pelota
    public float flechaAncho = 1.6f;      // en radios de pelota
    public Gradient powerGradient;
    [Tooltip("Opcional. Si está vacío se crea un material Unlit de URP")]
    public Material arrowMaterial;

    [Header("UI")]
    [Tooltip("Opcional. Fuente TMP (p. ej. Bungee-Regular SDF)")]
    public TMP_FontAsset uiFont;

    // Cada vez que cambien los valores por defecto se sube este número y se aplican
    // automáticamente a la pelota (Unity, si no, conserva los números viejos del Inspector).
    const int VersionAjustes = 4;
    [SerializeField, HideInInspector] int versionAjustes;

    void AplicarAjustesSiHaceFalta()
    {
        if (versionAjustes >= VersionAjustes) return;
        velocidadTiroMax = 70f;
        velocidadTiroMin = 3f;
        masa = 5f;
        multiplicadorGravedad = 2f;
        frenadoEnCuesta = 0.15f;
        flechaLargoMin = 3f;
        flechaLargoMax = 24f;
        flechaAncho = 1.6f;
        curvaFuerza = 1.3f;
        pixelesFuerzaMax = 250f;
        segundosBarraLlena = 1.2f;
        deceleracionCesped = 5f;
        deceleracionArena = 12f;
        frenadoFinal = 2.5f;
        velocidadFrenadoFinal = 3f;
        velocidadParada = 0.35f;
        pendienteMaxParada = 9f;
        arrastreAire = 0.02f;
        versionAjustes = VersionAjustes;
    }

    void OnValidate() => AplicarAjustesSiHaceFalta();

    // --- estado ---
    Rigidbody rb;
    float radius = 0.5f;
    float aimYaw;               // ángulo en grados (mundo)
    float power;                // 0..1
    float chargeTime;
    bool isCharging;
    bool isMoving;
    float slowTimer;
    float shotTimer;
    float stuckTimer;
    float dragPixels;
    bool chargingWithMouse;
    int strokes;
    Vector3 lastShotPosition;
    float lastShotYaw;
    bool canUndoShot;
    Collider ownCollider;

    public float Radio => radius;
    public int Golpes => strokes;
    public bool Embocada { get; private set; }

    // flecha
    Transform arrow;
    LineRenderer line;

    // UI
    GolfHUD hud;
    TrailRenderer trail;

    void Reset()
    {
        powerGradient = DefaultGradient();
    }

    DesertPauseMenu pauseMenu;

    void Awake()
    {
        AplicarAjustesSiHaceFalta();
        rb = GetComponent<Rigidbody>();
        if (cam == null) cam = Camera.main;
        if (powerGradient == null || powerGradient.colorKeys.Length < 2 ||
            powerGradient.Evaluate(0f) == powerGradient.Evaluate(1f))
            powerGradient = DefaultGradient();

        var col = GetComponent<Collider>();
        ownCollider = col;
        if (col != null) radius = Mathf.Max(0.01f, col.bounds.extents.y);

        rb.mass = masa;
        rb.linearDamping = arrastreAire;
        rb.angularDamping = 0.05f;
        rb.maxAngularVelocity = 100f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.maxLinearVelocity = velocidadTiroMax * 2f;

        lastShotPosition = transform.position;

        if (cam != null)
        {
            Vector3 f = cam.transform.forward; f.y = 0f;
            if (f.sqrMagnitude > 0.001f) aimYaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        }

        BuildArrow();
        CrearEstela();
        hud = gameObject.AddComponent<GolfHUD>();
        hud.Init(uiFont, powerGradient, par);

        if (gameObject.scene.name == "IslandDesert")
        {
            var pauseRoot = new GameObject("IslandDesert Pause Menu", typeof(RectTransform));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(pauseRoot, gameObject.scene);
            pauseRoot.transform.SetParent(transform, false);
            pauseMenu = pauseRoot.AddComponent<DesertPauseMenu>();
            pauseMenu.Init(this, hud);
        }
    }

    void Start()
    {
        if (salida != null) ColocarEnSalida();
    }

    public void ColocarEnSalida()
    {
        Vector3 p = salida != null ? salida.position + Vector3.up * (radius * 1.05f) : lastShotPosition;
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.position = p;
        transform.position = p;
        lastShotPosition = p;
        canUndoShot = false;
        isMoving = false;
        isCharging = false;
        power = 0f;
    }

    /// <summary>Lo llama GolfHole cuando la pelota entra en el hoyo.</summary>
    public void Embocar(Transform hoyo)
    {
        if (Embocada) return;
        Embocada = true;
        isMoving = false;
        isCharging = false;
        power = 0f;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        if (trail != null) trail.emitting = false;
        StartCoroutine(SecuenciaHoyo(hoyo));
    }

    System.Collections.IEnumerator SecuenciaHoyo(Transform hoyo)
    {
        // La pelota se centra y cae dentro del agujero
        Vector3 desde = transform.position;
        Vector3 hasta = hoyo.position + Vector3.down * radius * 0.6f;
        float t = 0f;
        while (t < 0.35f)
        {
            t += Time.deltaTime;
            float k = t / 0.35f;
            transform.position = Vector3.Lerp(desde, hasta, k * k);
            yield return null;
        }
        transform.position = hasta;
        string resultado = hud.OnHole(strokes, hoyo.position, radius);

        yield return new WaitForSeconds(tiempoTrasHoyo);
        if (pauseMenu != null) pauseMenu.ShowResults(resultado, strokes, par);
        else ReiniciarHoyo();
    }

    public void ReiniciarHoyo()
    {
        StopAllCoroutines();
        Embocada = false;
        strokes = 0;
        if (hud != null) hud.OnReset();
        if (trail != null) { trail.emitting = false; trail.Clear(); }
        ColocarEnSalida();
    }

    public void CancelChargeForPause()
    {
        if (!isCharging) return;
        isCharging = false;
        power = 0f;
        hud.CancelCharge();
    }

    void Update()
    {
        if (pauseMenu != null && pauseMenu.CapturesGameplay) return;
        if (!Embocada && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            ReiniciarHoyo();

        if (Embocada)
        {
            arrow.gameObject.SetActive(false);
            UpdateUI();
            return;
        }

        if (canUndoShot && Keyboard.current?.zKey.wasPressedThisFrame == true)
        {
            ResetToLastShot(0f);
            aimYaw = lastShotYaw;
            canUndoShot = false;
            strokes--;
            hud.CancelCharge();
            hud.SetGolpes(strokes, true);
            UpdateArrow();
            return;
        }

        // ¿Se ha caído del mapa?
        if (transform.position.y < alturaMuerte)
            ResetToLastShot(radius * 0.2f);

        if (isMoving)
        {
            arrow.gameObject.SetActive(false);
            UpdateUI();
            return;
        }

        // En vista libre no se puede tirar
        if (CameraFollow.VistaLibreActiva)
        {
            isCharging = false;
            power = 0f;
            arrow.gameObject.SetActive(false);
            UpdateUI();
            return;
        }

        arrow.gameObject.SetActive(true);
        HandleAim();
        HandlePower();
        UpdateArrow();
        UpdateUI();
    }

    void FixedUpdate()
    {
        // Si la pelota empieza a rodar sola (p. ej. quedó en una cuesta), se trata como un tiro en curso
        if (!isMoving && !Embocada && !isCharging && !rb.isKinematic && rb.linearVelocity.magnitude > 1.5f)
        {
            isMoving = true;
            slowTimer = 0f; shotTimer = 0f; stuckTimer = 0f;
        }
        if (!isMoving || Embocada) return;

        float dt = Time.fixedDeltaTime;
        shotTimer += dt;
        if (trail != null) trail.emitting = rb.linearVelocity.magnitude > 6f;
        rb.linearDamping = arrastreAire;

        // Pelota "pesada": más gravedad -> cae rápido, no flota en los saltos y baja las rampas con decisión
        if (multiplicadorGravedad > 1f)
            rb.AddForce(Physics.gravity * (multiplicadorGravedad - 1f), ForceMode.Acceleration);

        bool grounded = IsGrounded(out RaycastHit ground);
        if (!grounded) { slowTimer = 0f; return; }

        bool enArena = ground.collider.sharedMaterial != null &&
                       ground.collider.sharedMaterial.name.Contains("Arena");
        float pendiente = Vector3.Angle(ground.normal, Vector3.up);
        bool llano = pendiente < pendienteMaxParada;

        // Velocidad sobre el plano del suelo
        Vector3 v = rb.linearVelocity;
        Vector3 vPlano = Vector3.ProjectOnPlane(v, ground.normal);
        float speed = vPlano.magnitude;

        // Frenado constante tipo minigolf (no exponencial): la pelota pierde velocidad
        // a ritmo fijo y se para de forma natural. Al final frena un poco más.
        float decel = enArena ? deceleracionArena : deceleracionCesped;
        if (!llano) decel *= frenadoEnCuesta;   // en las rampas manda la gravedad: la pelota baja
        if (llano && speed < velocidadFrenadoFinal)
            decel += frenadoFinal * (1f - speed / velocidadFrenadoFinal);

        if (speed > 0.0001f)
        {
            float dv = Mathf.Min(speed, decel * dt);
            rb.AddForce(-vPlano / speed * dv, ForceMode.VelocityChange);
            // reducir también el giro en la misma proporción, para que el roce no la vuelva a empujar
            rb.angularVelocity *= (speed - dv) / speed;
            speed -= dv;
        }

        // Parada: lenta, en terreno casi llano
        if (speed < velocidadParada && llano)
        {
            slowTimer += dt;
            if (slowTimer >= 0.15f) StopBall();
        }
        else slowTimer = 0f;

        // Anti-atasco: si lleva un rato casi quieta (encajada en una esquina de una rampa, etc.)
        // se da por parada para que puedas volver a tirar
        if (rb.linearVelocity.magnitude < 0.4f) stuckTimer += dt; else stuckTimer = 0f;
        if (stuckTimer > 1.5f) StopBall();

        // Seguridad: un tiro nunca dura más de 25 s
        if (shotTimer > 25f && speed < 2f) StopBall();
    }

    // ---------------- Apuntado ----------------
    void HandleAim()
    {
        if (isCharging) return; // mientras cargas, la dirección queda fija
        var kb = Keyboard.current;
        var mouse = Mouse.current;

        // Teclado: flechas izquierda/derecha (WASD es para la cámara, como en Golf It)
        if (kb != null)
        {
            float dir = 0f;
            if (kb.leftArrowKey.isPressed) dir -= 1f;
            if (kb.rightArrowKey.isPressed) dir += 1f;
            aimYaw += dir * keyboardAimSpeed * Time.deltaTime;
        }

        // Ratón: la flecha apunta al cursor (solo si se mueve y no se está girando la cámara)
        if (mouse != null && cam != null && !mouse.rightButton.isPressed &&
            mouse.delta.ReadValue().sqrMagnitude > 0.01f)
        {
            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            Plane plane = new Plane(Vector3.up, transform.position);
            if (plane.Raycast(ray, out float enter))
            {
                Vector3 to = ray.GetPoint(enter) - transform.position;
                to.y = 0f;
                if (to.magnitude > radius * 0.5f)
                    aimYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            }
        }
    }

    Vector3 AimDirection => Quaternion.Euler(0f, aimYaw, 0f) * Vector3.forward;

    // ---------------- Potencia ----------------
    void HandlePower()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;

        bool mousePressed = mouse != null && mouse.leftButton.wasPressedThisFrame &&
            (pauseMenu == null || !pauseMenu.PointerOverPauseButton());
        bool pressed  = mousePressed || (kb != null && kb.spaceKey.wasPressedThisFrame);
        bool held     = (mouse != null && mouse.leftButton.isPressed)           || (kb != null && kb.spaceKey.isPressed);
        bool released = (mouse != null && mouse.leftButton.wasReleasedThisFrame) || (kb != null && kb.spaceKey.wasReleasedThisFrame);
        bool cancel   = (mouse != null && mouse.rightButton.wasPressedThisFrame) || (kb != null && kb.escapeKey.wasPressedThisFrame);

        if (!isCharging && pressed)
        {
            isCharging = true;
            chargeTime = 0f;
            power = 0f;
            dragPixels = 0f;
            chargingWithMouse = mousePressed;
        }

        if (!isCharging) return;

        if (cancel)
        {
            isCharging = false;
            power = 0f;
            return;
        }

        if (held)
        {
            if (chargingWithMouse)
            {
                // Estilo Golf It: arrastra el ratón hacia atrás (abajo) para dar fuerza,
                // hacia delante para quitarla
                float escala = Screen.height / 1080f;
                dragPixels -= mouse.delta.ReadValue().y;
                power = Mathf.Clamp01(dragPixels / (pixelesFuerzaMax * escala));
            }
            else
            {
                // Con Espacio: la barra sube y baja sola
                chargeTime += Time.deltaTime;
                power = Mathf.PingPong(chargeTime / segundosBarraLlena, 1f);
            }
        }

        if (released || !held)
        {
            bool releasingOverPause = chargingWithMouse && pauseMenu != null && pauseMenu.PointerOverPauseButton();
            if (!releasingOverPause && power >= fuerzaMinimaTiro) Shoot(power);
            isCharging = false;
            power = 0f;
        }
    }

    void Shoot(float p)
    {
        lastShotPosition = rb.position;
        lastShotYaw = aimYaw;
        canUndoShot = true;
        strokes++;

        rb.WakeUp();
        float speed = Mathf.Lerp(velocidadTiroMin, velocidadTiroMax, Mathf.Pow(p, curvaFuerza));
        rb.AddForce(AimDirection * speed, ForceMode.VelocityChange);

        isMoving = true;
        slowTimer = 0f;
        shotTimer = 0f;
        stuckTimer = 0f;
        if (hud != null) hud.OnShot(p, strokes, transform.position, radius);
    }

    void StopBall()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.Sleep();
        if (trail != null) trail.emitting = false;
        isMoving = false;
        slowTimer = 0f;
        stuckTimer = 0f;
    }

    void ResetToLastShot(float lift)
    {
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.position = lastShotPosition + Vector3.up * lift;
        transform.position = rb.position;
        if (trail != null) { trail.emitting = false; trail.Clear(); }
        isMoving = false;
        isCharging = false;
        power = 0f;
    }

    bool IsGrounded(out RaycastHit hit)
    {
        return Physics.Raycast(transform.position, Vector3.down, out hit, radius * 1.3f,
                               ~0, QueryTriggerInteraction.Ignore);
    }

    // ---------------- Raya de apuntado (estilo Golf It) ----------------
    const int PuntosRaya = 16;

    void BuildArrow()
    {
        var go = new GameObject("AimArrow");
        arrow = go.transform;

        line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = PuntosRaya;
        line.numCapVertices = 8;          // extremos redondeados
        line.numCornerVertices = 4;
        line.alignment = LineAlignment.View; // siempre de cara a la cámara: se ve desde cualquier ángulo
        line.textureMode = LineTextureMode.Stretch;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        if (arrowMaterial == null)
        {
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            arrowMaterial = new Material(sh);
        }
        line.sharedMaterial = arrowMaterial;
    }

    void UpdateArrow()
    {
        Vector3 dir = AimDirection;
        float len = Mathf.Lerp(flechaLargoMin, flechaLargoMax, power) * radius;
        float altura = radius * 0.3f;  // un poco por encima del suelo
        Vector3 start = transform.position + dir * (radius * 1.15f);
        float baseY = transform.position.y - radius + altura;

        // La raya sigue el terreno (rampas, escalones) para que nunca quede enterrada
        for (int i = 0; i < PuntosRaya; i++)
        {
            float t = i / (float)(PuntosRaya - 1);
            Vector3 p = start + dir * (len * t);
            float y = baseY;
            if (Physics.Raycast(new Vector3(p.x, transform.position.y + radius * 3f, p.z), Vector3.down,
                                out RaycastHit hit, radius * 8f, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider != ownCollider)
                y = hit.point.y + altura;
            p.y = y;
            line.SetPosition(i, p);
        }

        line.widthMultiplier = flechaAncho * radius * 0.35f;

        // Color: verde en la bola, color de la fuerza en la punta
        Color c0 = powerGradient.Evaluate(0f);
        Color c1 = powerGradient.Evaluate(Mathf.Max(power, 0.05f));
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(c0, 0f), new GradientColorKey(c1, 1f) },
                  new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.95f, 1f) });
        line.colorGradient = g;
    }

    static Gradient DefaultGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.15f, 0.85f, 0.2f), 0f),
                new GradientColorKey(new Color(1f, 0.9f, 0.1f), 0.5f),
                new GradientColorKey(new Color(1f, 0.15f, 0.1f), 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }

    // ---------------- UI / efectos ----------------
    void UpdateUI()
    {
        if (hud != null) hud.SetPower(power, isCharging);
    }

    void CrearEstela()
    {
        trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.35f;
        trail.minVertexDistance = radius * 0.2f;
        trail.widthMultiplier = radius * 1.4f;
        trail.widthCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.8f, 0.9f, 1f), 1f) },
                  new[] { new GradientAlphaKey(0.55f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = g;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        Shader sh = Shader.Find("Sprites/Default");
        if (sh != null) trail.sharedMaterial = new Material(sh);
        trail.emitting = false;
    }

    void OnCollisionEnter(Collision c)
    {
        if (hud == null || c.contactCount == 0) return;
        // Solo choques "de lado" (paredes, rocas...), no el suelo
        Vector3 n = c.GetContact(0).normal;
        float fuerza = Vector3.Dot(c.relativeVelocity, n);
        if (Mathf.Abs(n.y) < 0.6f && fuerza > 2f) hud.OnBounce(fuerza);
    }

    void OnDestroy()
    {
        if (arrow != null) Destroy(arrow.gameObject);
    }
}
