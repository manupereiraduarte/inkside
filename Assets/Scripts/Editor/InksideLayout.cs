using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Inkside.EditorTools
{
    /// <summary>
    /// Persiste el acomodado manual de las escenas (Fase B del GDD) para que un rebuild no lo pise.
    /// Guarda a JSON la posición, rotación, escala y estado activo de lo que se tocó a mano, y lo vuelve a
    /// aplicar después. InksideSceneBuilder llama a TryApply al final de cada escena, antes de guardarla.
    ///
    /// Sólo se guarda el DELTA, no la escena entera: los objetos raíz (props, interactuables, luces, el Player,
    /// el Canvas) más, dentro de cada instancia de prefab, únicamente los hijos que tengan overrides propios
    /// (es el caso de la mesa y del libro de LotR, que se ajustaron por dentro). Así el archivo queda chico y
    /// legible en vez de volcar los cientos de nodos que traen los .glb.
    /// </summary>
    public static class InksideLayout
    {
        const string LayoutFolder = "Assets/_Escenas/Layout";

        [Serializable]
        public class Entry
        {
            public string path;        // ruta en la jerarquía, con #n para desambiguar hermanos homónimos
            public Vector3 pos;        // localPosition
            public Quaternion rot;     // localRotation (cuaternión, para no perder nada en el ida y vuelta)
            public Vector3 scale;      // localScale
            public bool active = true;
        }

        [Serializable]
        public class Layout
        {
            public string scene;
            public string savedAt;
            public List<Entry> entries = new List<Entry>();
        }

        // ============================================================== MENÚ

        [MenuItem("Inkside/Guardar layout de la escena abierta")]
        public static void SaveActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("inkside", "No hay una escena guardada abierta.", "OK");
                return;
            }

            int n = Save(scene);
            EditorUtility.DisplayDialog("inkside",
                string.Format("Layout de '{0}' guardado: {1} objetos en\n{2}", scene.name, n, PathFor(scene.name)),
                "OK");
        }

        [MenuItem("Inkside/Aplicar layout guardado a la escena abierta")]
        public static void ApplyActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            string note = TryApply(scene.name, scene);
            EditorUtility.DisplayDialog("inkside", note, "OK");
        }

        // ============================================================== GUARDAR

        public static int Save(Scene scene)
        {
            var layout = new Layout
            {
                scene = scene.name,
                savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            };

            var seen = new HashSet<Transform>();
            foreach (var root in scene.GetRootGameObjects())
            {
                // Todo objeto raíz de escena se guarda siempre, y también sus hijos directos: son los
                // contenedores (Environment, Interactables, Lighting) y lo que cuelga de ellos.
                Add(layout, seen, root.transform);
                foreach (Transform child in root.transform) Add(layout, seen, child);

                // De los prefabs, los hijos retocados a mano. PrefabUtility dice exactamente cuáles son,
                // así no hay que comparar a ojo contra el asset original.
                foreach (var instanceRoot in InstanceRootsIn(root.transform))
                {
                    var mods = PrefabUtility.GetPropertyModifications(instanceRoot.gameObject);
                    if (mods == null) continue;
                    foreach (var mod in mods)
                    {
                        if (mod == null || mod.target == null) continue;
                        var t = mod.target as Transform;
                        if (t == null)
                        {
                            var go = mod.target as GameObject;
                            if (go != null && mod.propertyPath == "m_IsActive") t = go.transform;
                        }
                        if (t != null && t.gameObject.scene == scene) Add(layout, seen, t);
                    }
                }
            }

            Directory.CreateDirectory(LayoutFolder);
            string path = PathFor(scene.name);
            File.WriteAllText(path, JsonUtility.ToJson(layout, true));
            AssetDatabase.ImportAsset(path);
            Debug.Log(string.Format("[InksideLayout] '{0}': guardados {1} objetos en {2}",
                scene.name, layout.entries.Count, path));
            return layout.entries.Count;
        }

        static void Add(Layout layout, HashSet<Transform> seen, Transform t)
        {
            if (t == null || !seen.Add(t)) return;
            layout.entries.Add(new Entry
            {
                path = PathOf(t),
                pos = t.localPosition,
                rot = t.localRotation,
                scale = t.localScale,
                active = t.gameObject.activeSelf,
            });
        }

        /// <summary>Raíces de instancia de prefab que haya debajo de (o en) este transform.</summary>
        static IEnumerable<Transform> InstanceRootsIn(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))
                    yield return t;
        }

        // ============================================================== APLICAR

        /// <summary>
        /// Aplica el layout guardado de la escena, si existe. Devuelve una línea para el informe del builder.
        /// Que no haya archivo no es un error: significa que esa escena todavía no se acomodó a mano.
        /// </summary>
        public static string TryApply(string sceneName, Scene scene)
        {
            string path = PathFor(sceneName);
            if (!File.Exists(path)) return "sin layout guardado (nada que reaplicar)";

            Layout layout;
            try { layout = JsonUtility.FromJson<Layout>(File.ReadAllText(path)); }
            catch (Exception e) { return string.Format("(!) no se pudo leer {0}: {1}", path, e.Message); }

            if (layout == null || layout.entries == null || layout.entries.Count == 0)
                return string.Format("(!) {0} está vacío", path);

            var byPath = new Dictionary<string, Transform>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    byPath[PathOf(t)] = t;

            // De padres a hijos: aplicar primero el padre deja bien la posición de mundo de los hijos.
            int applied = 0;
            var missing = new List<string>();
            foreach (var e in layout.entries.OrderBy(x => x.path.Count(c => c == '/')))
            {
                Transform t;
                if (!byPath.TryGetValue(e.path, out t)) { missing.Add(e.path); continue; }
                t.localPosition = e.pos;
                t.localRotation = e.rot;
                t.localScale = e.scale;
                if (t.gameObject.activeSelf != e.active) t.gameObject.SetActive(e.active);
                applied++;
            }
            Physics.SyncTransforms();

            string note = string.Format("layout reaplicado ({0}): {1}/{2} objetos",
                layout.savedAt, applied, layout.entries.Count);
            if (missing.Count > 0)
            {
                note += string.Format("; {0} sin encontrar", missing.Count);
                Debug.LogWarning(string.Format(
                    "[InksideLayout] '{0}': no se encontraron estos objetos del layout (¿cambió un nombre o el orden de creación?):\n  {1}",
                    sceneName, string.Join("\n  ", missing.Take(20).ToArray())));
            }
            return note;
        }

        // ============================================================== UTILS

        public static string PathFor(string sceneName)
        {
            return string.Format("{0}/{1}.json", LayoutFolder, sceneName);
        }

        /// <summary>
        /// Ruta estable de un transform en la jerarquía. A los hermanos con el mismo nombre se les agrega #n
        /// (hay tres 'asteroide_superficie' en El Principito), así que el orden de creación del builder tiene
        /// que coincidir con el que había cuando se guardó el layout.
        /// </summary>
        public static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (var cur = t; cur != null; cur = cur.parent)
            {
                int idx = 0;
                if (cur.parent != null)
                {
                    for (int i = 0; i < cur.parent.childCount; i++)
                    {
                        var c = cur.parent.GetChild(i);
                        if (c == cur) break;
                        if (c.name == cur.name) idx++;
                    }
                }
                else
                {
                    foreach (var r in cur.gameObject.scene.GetRootGameObjects())
                    {
                        if (r.transform == cur) break;
                        if (r.name == cur.name) idx++;
                    }
                }
                parts.Add(idx == 0 ? cur.name : string.Format("{0}#{1}", cur.name, idx));
            }
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }
    }
}
