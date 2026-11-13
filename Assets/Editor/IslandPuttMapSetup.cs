using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menú IslandPutt → Montar mapa del desierto.
/// Mete el mapa en la escena abierta y le pone colisiones, materiales físicos,
/// hoyo, salida, pelota y cámara. Se puede repetir cuantas veces quieras (Ctrl+Z lo deshace).
/// Los objetos del mapa se reconocen por el principio de su nombre (lo pone el export de Blender).
/// </summary>
public static class IslandPuttMapSetup
{
    const string MapPath = "Assets/Maps/IslandDesert_Mapa.glb";
    const string PhysicsFolder = "Assets/Physics";
    const string MapInstanceName = "IslandDesert_Mapa";
    const string OldMapName = "mapadesiertoModificadores";

    enum Kind { Cesped, Pared, Arena, Obstaculo, SinColision }

    // prefijo de nombre → tipo
    static readonly (string prefix, Kind kind)[] Rules =
    {
        ("Circuito_Cesped", Kind.Cesped),
        ("Circuito_Paredes", Kind.Pared),
        ("Terreno_Isla", Kind.Arena),
        ("Piramide", Kind.Arena),
        ("Madera", Kind.Pared),
        ("Roca", Kind.Obstaculo),
        ("Palmera_Tronco", Kind.Obstaculo),
        ("Cactus", Kind.Obstaculo),
        ("Palmera_Hojas", Kind.SinColision),
        ("Bandera", Kind.SinColision),
        ("Detalle", Kind.SinColision),
    };

    [MenuItem("IslandPutt/Montar mapa del desierto")]
    public static void MontarDesdeMenu() => Montar(false);

    /// <summary>¿La escena abierta ya tiene el mapa montado con colisiones?</summary>
    public static bool EstaMontado()
    {
        var scene = EditorSceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name != MapInstanceName) continue;
            var cesped = FindDeep(root.transform, "Circuito_Cesped");
            return cesped != null && cesped.GetComponentInChildren<MeshCollider>() != null;
        }
        return false;
    }

    public static void Montar(bool silencioso)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapPath);
        if (prefab == null)
        {
            Debug.LogError("[IslandPutt] No encuentro " + MapPath);
            return;
        }

        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[IslandPutt] Sal del modo Play antes de montar el mapa.");
            return;
        }

        // Recargar siempre al darle a Play: evita los errores 'MissingReferenceException' del Inspector
        EditorSettings.enterPlayModeOptionsEnabled = false;

        // Física más precisa para una pelota rápida
        Physics.defaultContactOffset = 0.02f;
        Physics.defaultSolverIterations = 12;
        Physics.defaultSolverVelocityIterations = 4;

        var scene = EditorSceneManager.GetActiveScene();
        Undo.SetCurrentGroupName("Montar mapa IslandPutt");
        int undoGroup = Undo.GetCurrentGroup();

        // --- Materiales físicos
        var matCesped = GetOrCreateMaterial("Cesped", 0.5f, 0.5f, 0f, PhysicsMaterialCombine.Average, PhysicsMaterialCombine.Minimum);
        var matPared = GetOrCreateMaterial("Pared", 0.05f, 0.05f, 0.75f, PhysicsMaterialCombine.Minimum, PhysicsMaterialCombine.Maximum);
        var matArena = GetOrCreateMaterial("Arena", 1f, 1f, 0f, PhysicsMaterialCombine.Maximum, PhysicsMaterialCombine.Minimum);
        var matObst = GetOrCreateMaterial("Obstaculo", 0.3f, 0.3f, 0.5f, PhysicsMaterialCombine.Average, PhysicsMaterialCombine.Maximum);

        // --- Quitar mapas anteriores (el viejo solo se desactiva)
        Vector3 mapPos = Vector3.zero;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == OldMapName)
            {
                mapPos = root.transform.position;
                if (root.activeSelf)
                {
                    Undo.RecordObject(root, "Desactivar mapa viejo");
                    root.SetActive(false);
                }
            }
        }
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == MapInstanceName)
            {
                mapPos = root.transform.position;
                Undo.DestroyObjectImmediate(root);
            }
        }

        // --- Instanciar el mapa nuevo
        var map = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        Undo.RegisterCreatedObjectUndo(map, "Crear mapa");
        map.name = MapInstanceName;
        map.transform.SetPositionAndRotation(mapPos, Quaternion.identity);
        map.transform.localScale = Vector3.one;

        // --- Colisiones
        var counts = new Dictionary<Kind, int>();
        foreach (var mf in map.GetComponentsInChildren<MeshFilter>(true))
        {
            Kind kind = Classify(mf.transform);
            counts[kind] = counts.TryGetValue(kind, out int c) ? c + 1 : 1;

            foreach (var old in mf.GetComponents<Collider>())
                Undo.DestroyObjectImmediate(old);

            GameObjectUtility.SetStaticEditorFlags(mf.gameObject,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI |
                StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.OccludeeStatic);

            if (kind == Kind.SinColision || mf.sharedMesh == null) continue;

            var mc = Undo.AddComponent<MeshCollider>(mf.gameObject);
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = false;
            mc.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation |
                                MeshColliderCookingOptions.EnableMeshCleaning |
                                MeshColliderCookingOptions.WeldColocatedVertices |
                                MeshColliderCookingOptions.UseFastMidphase;
            mc.sharedMaterial = kind switch
            {
                Kind.Cesped => matCesped,
                Kind.Pared => matPared,
                Kind.Arena => matArena,
                _ => matObst
            };
        }

        // --- Hoyo
        var hoyo = FindDeep(map.transform, "Hoyo");
        if (hoyo != null && hoyo.GetComponent<GolfHole>() == null)
            Undo.AddComponent<GolfHole>(hoyo.gameObject);
        if (hoyo != null)
        {
            var sc = hoyo.GetComponent<SphereCollider>();
            if (sc == null) sc = Undo.AddComponent<SphereCollider>(hoyo.gameObject);
            var gh = hoyo.GetComponent<GolfHole>();
            sc.isTrigger = true;
            sc.radius = gh.radioHoyo;
            sc.center = new Vector3(0f, -gh.radioHoyo * 0.5f, 0f);
        }

        // --- Pelota
        var salida = FindDeep(map.transform, "Salida");
        var ball = FindBall(scene);
        string ballInfo = "No he encontrado la pelota (un objeto con Rigidbody y SphereCollider).";
        if (ball != null)
        {
            var ctrl = ball.GetComponent<MiniGolfShotController>();
            if (ctrl == null) ctrl = Undo.AddComponent<MiniGolfShotController>(ball);
            Undo.RecordObject(ctrl, "Configurar pelota");
            ctrl.salida = salida;

            var cesped = FindDeep(map.transform, "Circuito_Cesped");
            var rend = cesped != null ? cesped.GetComponentInChildren<Renderer>() : null;
            if (rend != null) ctrl.alturaMuerte = rend.bounds.min.y - 4f;

            var rb = ball.GetComponent<Rigidbody>();
            Undo.RecordObject(rb, "Configurar pelota");
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var sphere = ball.GetComponent<SphereCollider>();
            float r = sphere != null ? sphere.radius * MaxAbs(ball.transform.lossyScale) : 0.5f;
            if (salida != null)
            {
                Undo.RecordObject(ball.transform, "Mover pelota");
                ball.transform.position = salida.position + Vector3.up * (r * 1.05f);
            }
            ballInfo = "Pelota: " + ball.name + " colocada en la salida.";

            // Cámara
            var cam = Object.FindAnyObjectByType<CameraFollow>();
            if (cam != null)
            {
                Undo.RecordObject(cam, "Cámara");
                cam.target = ball.transform;
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!silencioso) Selection.activeGameObject = map;
        else Selection.activeGameObject = null; // evita errores del Inspector al salir de Play

        string resumen =
            $"Mapa montado.\n\n" +
            $"Césped: {Get(counts, Kind.Cesped)}  Paredes/madera: {Get(counts, Kind.Pared)}\n" +
            $"Arena (isla, pirámide): {Get(counts, Kind.Arena)}\n" +
            $"Obstáculos (rocas, palmeras, cactus): {Get(counts, Kind.Obstaculo)}\n" +
            $"Sin colisión (hojas, bandera): {Get(counts, Kind.SinColision)}\n\n" +
            (hoyo != null ? "Hoyo configurado.\n" : "¡No encuentro el objeto Hoyo!\n") +
            ballInfo + "\n\nGuarda la escena (Ctrl+S) y dale a Play.";
        Debug.Log("[IslandPutt] " + resumen);
        if (!silencioso) EditorUtility.DisplayDialog("IslandPutt", resumen, "OK");
        else EditorSceneManager.SaveScene(scene);
    }

    // ======================= CIELO =======================
    const string SkyMatPath = "Assets/Materials/CieloLowPoly.mat";
    const string SkyRootName = "Cielo";

    public static bool CieloMontado()
    {
        var scene = EditorSceneManager.GetActiveScene();
        bool tieneNubes = scene.GetRootGameObjects().Any(r => r.name == SkyRootName && r.GetComponent<LowPolyClouds>() != null);
        return tieneNubes && RenderSettings.skybox != null && RenderSettings.skybox.name == "CieloLowPoly";
    }

    [MenuItem("IslandPutt/Montar cielo")]
    public static void MontarCieloDesdeMenu() => MontarCielo(false);

    public static bool MontarCielo(bool silencioso)
    {
        var sh = Shader.Find("IslandPutt/CieloLowPoly");
        if (sh == null)
        {
            Debug.LogWarning("[IslandPutt] El shader del cielo aún no está importado; se reintentará.");
            return false;
        }
        var scene = EditorSceneManager.GetActiveScene();

        // Material del cielo
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(SkyMatPath);
        if (mat == null)
        {
            mat = new Material(sh) { name = "CieloLowPoly" };
            AssetDatabase.CreateAsset(mat, SkyMatPath);
        }

        // Sol en la dirección de la luz principal, y luz algo más cálida
        var sun = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>())
                       .FirstOrDefault(l => l.type == LightType.Directional);
        if (sun != null)
        {
            Undo.RecordObject(sun, "Sol");
            sun.color = new Color(1f, 0.95f, 0.84f);
            sun.shadows = LightShadows.Soft;
            mat.SetVector("_DirSol", -sun.transform.forward);
            RenderSettings.sun = sun;
        }
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        // Tamaño del mapa (para niebla y nubes)
        var mapRoot = scene.GetRootGameObjects().FirstOrDefault(r => r.name == MapInstanceName);
        Bounds b = new Bounds(Vector3.zero, Vector3.one * 120f);
        if (mapRoot != null)
        {
            var rends = mapRoot.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0) { b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds); }
        }
        float R = Mathf.Max(b.extents.x, b.extents.z);

        // Ambiente, cielo y niebla de horizonte
        RenderSettings.skybox = mat;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.78f, 0.98f);
        RenderSettings.ambientEquatorColor = new Color(0.78f, 0.85f, 0.93f);
        RenderSettings.ambientGroundColor = new Color(0.48f, 0.44f, 0.40f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = mat.GetColor("_ColorHorizonte");
        RenderSettings.fogStartDistance = R * 2.5f;
        RenderSettings.fogEndDistance = R * 9f;
        DynamicGI.UpdateEnvironment();

        // La cámara tiene que ver lejos (nubes)
        var cam = Camera.main;
        if (cam != null)
        {
            Undo.RecordObject(cam, "Cámara");
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, R * 12f);
        }

        // Nubes
        foreach (var r in scene.GetRootGameObjects())
            if (r.name == SkyRootName) Undo.DestroyObjectImmediate(r);
        var cielo = new GameObject(SkyRootName);
        Undo.RegisterCreatedObjectUndo(cielo, "Cielo");
        var clouds = cielo.AddComponent<LowPolyClouds>();
        clouds.centro = mapRoot != null ? mapRoot.transform : null;

        EditorSceneManager.MarkSceneDirty(scene);
        if (silencioso) EditorSceneManager.SaveScene(scene);
        Debug.Log("[IslandPutt] Cielo montado.");
        if (!silencioso)
            EditorUtility.DisplayDialog("IslandPutt", "Cielo low poly y nubes montados.\nLas nubes aparecen al darle a Play.", "OK");
        return true;
    }

    static Kind Classify(Transform t)
    {
        for (var p = t; p != null; p = p.parent)
            foreach (var (prefix, kind) in Rules)
                if (p.name.StartsWith(prefix)) return kind;
        return Kind.Obstaculo; // cualquier cosa desconocida choca
    }

    static PhysicsMaterial GetOrCreateMaterial(string name, float dyn, float stat, float bounce,
                                               PhysicsMaterialCombine friction, PhysicsMaterialCombine bounceCombine)
    {
        if (!AssetDatabase.IsValidFolder(PhysicsFolder))
            AssetDatabase.CreateFolder("Assets", "Physics");
        string path = $"{PhysicsFolder}/{name}.physicsMaterial";
        var m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if (m == null)
        {
            m = new PhysicsMaterial(name);
            AssetDatabase.CreateAsset(m, path);
        }
        m.dynamicFriction = dyn;
        m.staticFriction = stat;
        m.bounciness = bounce;
        m.frictionCombine = friction;
        m.bounceCombine = bounceCombine;
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }

    static GameObject FindBall(UnityEngine.SceneManagement.Scene scene)
    {
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Rigidbody>(true));
        var withCtrl = all.FirstOrDefault(rb => rb.GetComponent<MiniGolfShotController>() != null);
        if (withCtrl != null) return withCtrl.gameObject;
        var sphere = all.FirstOrDefault(rb => rb.GetComponent<SphereCollider>() != null);
        return sphere != null ? sphere.gameObject : null;
    }

    static Transform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    static float MaxAbs(Vector3 v) => Mathf.Max(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    static int Get(Dictionary<Kind, int> d, Kind k) => d.TryGetValue(k, out int v) ? v : 0;
}
