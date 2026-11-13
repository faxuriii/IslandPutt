using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Red de seguridad al darle a Play: cualquier objeto visible de la escena que no tenga
/// colisión recibe un MeshCollider, para que nada sea atravesable.
/// Se excluyen las hojas de palmera, la bandera, los detalles y la propia pelota.
/// </summary>
public static class ColliderSafetyNet
{
    static readonly string[] SinColision = { "Palmera_Hojas", "Bandera", "Detalle", "AimArrow", "Cielo", "Nube", "GolfUI" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AddMissingColliders()
    {
        int added = 0;
        var filters = SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<MeshFilter>(false));
        foreach (var mf in filters)
        {
            if (mf.sharedMesh == null) continue;
            if (mf.GetComponent<Collider>() != null) continue;
            if (mf.GetComponentInParent<Rigidbody>() != null) continue; // la pelota u objetos dinámicos
            if (Excluido(mf.transform)) continue;

            var mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            added++;
        }
        if (added > 0) Debug.Log($"[IslandPutt] Añadidas {added} colisiones que faltaban.");
    }

    static bool Excluido(Transform t)
    {
        for (var p = t; p != null; p = p.parent)
            foreach (var n in SinColision)
                if (p.name.StartsWith(n)) return true;
        return false;
    }
}
