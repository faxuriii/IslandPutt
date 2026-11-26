using UnityEngine;

// The imported course contains separate meshes for the green, rails and cup.
[DefaultExecutionOrder(-500)]
public class SnowCourse : MonoBehaviour
{
    public PhysicsMaterial green, wall;
    public MiniGolfShotController ball;

    readonly System.Collections.Generic.List<Collider> circuito = new System.Collections.Generic.List<Collider>();
    Vector3 centroHoyo;
    float radioHoyo;
    Material materialNieve;
    Texture2D copo;

    public bool SobreCircuito(Vector3 posicion, float radio)
    {
        // La abertura del hoyo también forma parte del campo.
        Vector3 delta = posicion - centroHoyo;
        if (delta.x * delta.x + delta.z * delta.z <= radioHoyo * radioHoyo && delta.y >= -radioHoyo)
            return true;
        foreach (var collider in circuito)
        {
            var bounds = collider.bounds;
            var origen = new Vector3(posicion.x, bounds.max.y + 1f, posicion.z);
            if (collider.Raycast(new Ray(origen, Vector3.down), out var hit, bounds.size.y + 2f) &&
                posicion.y >= hit.point.y - radio * 2f) return true;
        }
        return false;
    }

    void CrearNevada()
    {
        var nube = new GameObject("Nevada");
        nube.transform.SetParent(transform, false);
        nube.transform.position = ball.transform.position + Vector3.up * 12f;
        var ps = nube.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.startLifetime = 7f;
        main.startSize = new ParticleSystem.MinMaxCurve(.07f, .19f);
        main.maxParticles = 1700;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(46, 12, 46);
        var emission = ps.emission;
        emission.rateOverTime = 180f;
        var viento = ps.velocityOverLifetime;
        viento.enabled = true;
        viento.space = ParticleSystemSimulationSpace.World;
        viento.x = .5f;
        viento.y = -2.8f;
        viento.z = .22f;
        copo = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                copo.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01((7.5f - Vector2.Distance(new Vector2(x,y), new Vector2(7.5f,7.5f))) / 2f)));
        copo.Apply();
        materialNieve = new Material(Shader.Find("Sprites/Default"));
        materialNieve.mainTexture = copo;
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = materialNieve;
        // El emisor sigue la posición, sin heredar el giro de la pelota.
        nube.transform.rotation = Quaternion.identity;
        emisor = nube.transform;
        ps.Play();
    }

    Transform emisor;
    void LateUpdate()
    {
        if (emisor != null) emisor.SetPositionAndRotation(ball.transform.position + Vector3.up * 12f, Quaternion.identity);
    }

    void OnDestroy()
    {
        if (materialNieve != null) Destroy(materialNieve);
        if (copo != null) Destroy(copo);
    }

    void Awake()
    {
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            Transform course = null;
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
                if (node.Find("Salida") != null && node.Find("Hoyo") != null) { course = node; break; }
            if (course == null) continue;
            var clouds = FindAnyObjectByType<LowPolyClouds>();
            if (clouds != null) clouds.centro = course;
            ball.salida = course.Find("Salida");
            var cup = course.Find("Hoyo");
            var trigger = new GameObject("Hoyo jugable");
            trigger.transform.position = cup.position;
            centroHoyo = cup.position;
            radioHoyo = trigger.AddComponent<GolfHole>().radioHoyo;
            foreach (var mesh in course.GetComponentsInChildren<MeshFilter>())
            {
                if (mesh.name.StartsWith("Bandera")) continue;
                var collider = mesh.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh.sharedMesh;
                if (mesh.name.StartsWith("Cesped") || mesh.name.StartsWith("Paredes") || mesh.name.StartsWith("Copa"))
                    circuito.Add(collider);
                collider.sharedMaterial = mesh.name.StartsWith("Cesped") ? green : wall;
            }
            ball.circuitoNieve = this;
            CrearNevada();
            return;
        }
        Debug.LogError("Falta la raíz IslaNieve en la escena de nieve.", this);
    }
}
