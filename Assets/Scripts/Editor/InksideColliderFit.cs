using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Inkside.EditorTools
{
    /// <summary>
    /// Recalcula el BoxCollider de cada interactuable sobre la geometría que tiene AHORA.
    ///
    /// Hace falta porque el builder dimensiona esos colliders una sola vez, al construir la escena. Si después
    /// se acomoda el modelo por dentro (rotar o mover un hijo del .glb, que es lo que pasó con el libro de
    /// LotR), la malla se mueve y el collider se queda donde estaba: el prompt de [E] aparece apuntando al
    /// aire y el objeto real no responde.
    ///
    /// También agranda un poco los objetos chicos: apuntarle a un libro de 17 cm desde dos metros es un
    /// ejercicio de precisión, no de juego.
    /// </summary>
    public static class InksideColliderFit
    {
        // Margen y tamaño mínimo en metros de mundo, por tipo de interactuable.
        const float ItemPadding = 1.15f;
        const float ItemMinSize = 0.35f;
        const float NpcPadding = 1.1f;
        const float NpcMinSize = 0.5f;

        [MenuItem("Inkside/Reajustar colliders de los interactuables (escena abierta)")]
        public static void FitActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            string note = Fit(scene);
            EditorUtility.DisplayDialog("inkside", note, "OK");
        }

        public static string Fit(Scene scene)
        {
            int done = 0, skipped = 0;
            var log = new System.Text.StringBuilder();

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var box in root.GetComponentsInChildren<BoxCollider>(true))
                {
                    var go = box.gameObject;

                    bool isNpc = go.GetComponent<DialogueTrigger>() != null;
                    bool isItem = go.GetComponent<SceneTeleport>() != null ||
                                  go.GetComponent<ObjectTargetReturn>() != null;
                    if (!isNpc && !isItem) continue;   // las paredes del diorama no se tocan

                    Vector3 oldCenter = box.center;
                    Vector3 oldSize = box.size;

                    Bounds local;
                    if (!TryGetLocalBounds(go, out local))
                    {
                        skipped++;
                        log.AppendLine(string.Format("  {0}: sin renderers visibles, se deja como está", go.name));
                        continue;
                    }

                    var lossy = go.transform.lossyScale;
                    if (Mathf.Abs(lossy.x) < 1e-9f || Mathf.Abs(lossy.y) < 1e-9f || Mathf.Abs(lossy.z) < 1e-9f)
                    {
                        skipped++;
                        log.AppendLine(string.Format("  {0}: escala degenerada {1}, se deja como está", go.name, lossy));
                        continue;
                    }

                    float padding = isNpc ? NpcPadding : ItemPadding;
                    float minWorld = isNpc ? NpcMinSize : ItemMinSize;
                    var minLocal = new Vector3(
                        minWorld / Mathf.Abs(lossy.x),
                        minWorld / Mathf.Abs(lossy.y),
                        minWorld / Mathf.Abs(lossy.z));

                    box.center = local.center;
                    box.size = Vector3.Max(local.size * padding, minLocal);

                    float moved = Vector3.Distance(
                        go.transform.TransformPoint(oldCenter),
                        go.transform.TransformPoint(box.center));

                    done++;
                    log.AppendLine(string.Format(
                        "  {0}: centro movido {1:0.##} m (de {2} a {3}), tamaño {4} -> {5}",
                        go.name, moved, Fmt(oldCenter), Fmt(box.center), Fmt(oldSize), Fmt(box.size)));

                    EditorUtility.SetDirty(go);
                }
            }

            Physics.SyncTransforms();
            string head = string.Format("{0}: {1} colliders reajustados{2}.",
                scene.name, done, skipped > 0 ? string.Format(", {0} salteados", skipped) : "");
            Debug.Log("[InksideColliderFit] " + head + "\n" + log);
            return head + "\n\n" + log;
        }

        /// <summary>
        /// AABB de todos los meshes hijos en el espacio LOCAL del root, que es el que espera BoxCollider.
        /// No se puede convertir un AABB de mundo dividiendo por la escala: los .glb traen una rotación de
        /// 270° en X, así que los ejes locales no coinciden con los del mundo y el tamaño quedaría permutado.
        /// </summary>
        static bool TryGetLocalBounds(GameObject go, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            var toLocal = go.transform.worldToLocalMatrix;

            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.gameObject.activeInHierarchy || !r.enabled) continue;

                var m = toLocal * r.localToWorldMatrix;
                var lb = r.localBounds;
                Vector3 min = lb.min, max = lb.max;

                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? min.x : max.x,
                        (i & 2) == 0 ? min.y : max.y,
                        (i & 4) == 0 ? min.z : max.z);
                    var p = m.MultiplyPoint3x4(corner);
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                    else bounds.Encapsulate(p);
                }
            }
            return any;
        }

        static string Fmt(Vector3 v)
        {
            return string.Format("({0:0.##}, {1:0.##}, {2:0.##})", v.x, v.y, v.z);
        }
    }
}
