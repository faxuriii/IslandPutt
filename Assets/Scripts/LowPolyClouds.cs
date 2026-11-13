using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Nubes low poly alrededor y por debajo de la isla (para que parezca que flota).
/// Se generan al darle a Play alrededor del objeto "centro" (el mapa).
/// </summary>
public class LowPolyClouds : MonoBehaviour
{
    [Tooltip("Objeto alrededor del cual van las nubes (el mapa). Si está vacío, este objeto")]
    public Transform centro;
    public int semilla = 7;

    [Header("Nubes alrededor")]
    public int nubesAlrededor = 26;
    [Header("Mar de nubes por debajo")]
    public int nubesDebajo = 40;

    [Header("Movimiento")]
    [Tooltip("Grados por segundo que gira el anillo de nubes")]
    public float velocidadGiro = 0.6f;
    public float balanceo = 1.2f;

    public Color colorNube = new Color(1f, 1f, 1f);
    public Color colorSombra = new Color(0.80f, 0.87f, 0.97f);

    readonly List<(Transform t, float fase, Vector3 basePos)> nubes = new List<(Transform, float, Vector3)>();
    Transform anillo;
    Vector3 c;
    float R;

    void Start()
    {
        Random.InitState(semilla);
        Bounds b = CalcularBounds();
        c = b.center;
        R = Mathf.Max(b.extents.x, b.extents.z);
        float alto = b.extents.y;

        var mat = CrearMaterial();
        anillo = new GameObject("Nubes").transform;
        anillo.SetParent(transform, false);
        anillo.position = c;

        // Anillo de nubes alrededor de la isla, unas por encima y otras por debajo
        for (int i = 0; i < nubesAlrededor; i++)
        {
            float ang = (i / (float)nubesAlrededor) * 360f + Random.Range(-6f, 6f);
            float dist = R * Random.Range(1.5f, 3.6f);
            float y = Random.Range(-alto * 1.6f, alto * 0.9f);
            Vector3 p = Quaternion.Euler(0, ang, 0) * Vector3.forward * dist + Vector3.up * y;
            CrearNube(p, R * Random.Range(0.12f, 0.26f), mat, 0.55f);
        }

        // Mar de nubes muy por debajo: grandes y planas
        for (int i = 0; i < nubesDebajo; i++)
        {
            Vector2 r2 = Random.insideUnitCircle * R * 5f;
            float y = b.min.y - c.y - R * Random.Range(0.9f, 1.4f);
            Vector3 p = new Vector3(r2.x, y, r2.y);
            CrearNube(p, R * Random.Range(0.35f, 0.7f), mat, 0.35f);
        }
    }

    void Update()
    {
        if (anillo == null) return;
        anillo.Rotate(0f, velocidadGiro * Time.deltaTime, 0f, Space.World);
        float t = Time.time;
        foreach (var n in nubes)
            n.t.localPosition = n.basePos + Vector3.up * Mathf.Sin(t * 0.3f + n.fase) * balanceo;
    }

    Bounds CalcularBounds()
    {
        Transform root = centro != null ? centro : transform;
        var rends = root.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return new Bounds(root.position, Vector3.one * 100f);
        Bounds b = rends[0].bounds;
        foreach (var r in rends) b.Encapsulate(r.bounds);
        return b;
    }

    Material CrearMaterial()
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
        var m = new Material(sh);
        m.SetColor("_BaseColor", colorNube);
        m.SetFloat("_Smoothness", 0f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", colorSombra * 0.35f); // las caras en sombra no quedan grises
        m.enableInstancing = true;
        return m;
    }

    void CrearNube(Vector3 localPos, float tam, Material mat, float aplastado)
    {
        var nube = new GameObject("Nube").transform;
        nube.SetParent(anillo, false);
        nube.localPosition = localPos;
        nube.localRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

        int bolas = Random.Range(3, 7);
        for (int i = 0; i < bolas; i++)
        {
            var go = new GameObject("Bola");
            go.transform.SetParent(nube, false);
            float s = tam * Random.Range(0.45f, 1f) * (i == 0 ? 1.2f : 1f);
            go.transform.localPosition = new Vector3((i - bolas * 0.5f) * tam * 0.55f + Random.Range(-0.15f, 0.15f) * tam,
                                                     Random.Range(-0.1f, 0.25f) * tam,
                                                     Random.Range(-0.3f, 0.3f) * tam);
            go.transform.localScale = new Vector3(s, s * aplastado * Random.Range(0.9f, 1.3f), s * Random.Range(0.8f, 1.1f));
            go.AddComponent<MeshFilter>().sharedMesh = MallaBola();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }
        nubes.Add((nube, Random.Range(0f, 10f), localPos));
    }

    // ---------- Malla low poly (icosaedro deformado, caras planas) ----------
    static readonly List<Mesh> cache = new List<Mesh>();

    static Mesh MallaBola()
    {
        if (cache.Count < 6)
        {
            var m = CrearIco(Random.Range(0, 100000));
            cache.Add(m);
            return m;
        }
        return cache[Random.Range(0, cache.Count)];
    }

    static Mesh CrearIco(int seed)
    {
        var rnd = new System.Random(seed);
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        var v = new List<Vector3> {
            new(-1,t,0), new(1,t,0), new(-1,-t,0), new(1,-t,0),
            new(0,-1,t), new(0,1,t), new(0,-1,-t), new(0,1,-t),
            new(t,0,-1), new(t,0,1), new(-t,0,-1), new(-t,0,1) };
        int[] f = { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                    3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };

        // Subdividir una vez
        var tris = new List<int>();
        var mid = new Dictionary<long, int>();
        int Mid(int a, int b)
        {
            long k = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (mid.TryGetValue(k, out int idx)) return idx;
            v.Add((v[a] + v[b]) * 0.5f);
            mid[k] = v.Count - 1;
            return v.Count - 1;
        }
        for (int i = 0; i < f.Length; i += 3)
        {
            int a = f[i], b = f[i + 1], c = f[i + 2];
            int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
            tris.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
        }
        // Normalizar y deformar un poco (aspecto irregular)
        for (int i = 0; i < v.Count; i++)
            v[i] = v[i].normalized * (0.5f * (0.85f + (float)rnd.NextDouble() * 0.3f));

        // Vértices sin compartir -> caras planas (low poly)
        var verts = new Vector3[tris.Count];
        var idxs = new int[tris.Count];
        for (int i = 0; i < tris.Count; i++) { verts[i] = v[tris[i]]; idxs[i] = i; }
        var mesh = new Mesh { name = "NubeLowPoly" };
        mesh.vertices = verts;
        mesh.triangles = idxs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
