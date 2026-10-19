using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Inkside.EditorTools
{
    /// <summary>
    /// Ajusta el tamaño del jugador escena por escena, cada uno relativo a un personaje u objeto de su mundo
    /// (un hobbit mide la mitad que Gandalf, en el asteroide hay que sacarle metro y medio al zorro, etc.).
    ///
    /// La altura NO se escribe a mano: se mide el objeto de referencia dentro de la escena abierta y se calcula.
    /// Así, si mañana se reescala a Gandalf, el jugador lo sigue.
    ///
    /// Importante: el tamaño se cambia en el CharacterController y en la altura de la cámara, NUNCA con la
    /// herramienta Scale sobre el Player. PhysX no soporta escala no uniforme en un CharacterController (la
    /// cápsula de colisión queda indefinida y te caés por el piso), y la velocidad del FirstPersonController
    /// está en metros de mundo, así que escalar el transform da un personaje chico que se mueve como un cohete.
    /// </summary>
    public static class InksidePlayerSizer
    {
        /// <summary>Altura del jugador con el que se afinaron las velocidades del FirstPersonController.</summary>
        const float ReferenceHeight = 1.7f;

        /// <summary>Proporción ojo/altura del PlayerCapsule original de Starter Assets (1,375 / 2).</summary>
        const float EyeRatio = 0.6875f;

        class SizeRule
        {
            public string Scene;
            public string ReferenceObject; // objeto de la escena que se mide para sacar la altura
            public float Factor = 1f;      // altura = alturaDeLaReferencia * Factor + Offset
            public float Offset;
            public float Radius;
            public string Why;
        }

        static readonly SizeRule[] Rules =
        {
            new SizeRule
            {
                Scene = "00_Biblioteca", ReferenceObject = "wooden_table", Offset = 1f, Radius = 0.22f,
                Why = "1 m más alto que la mesa",
            },
            new SizeRule
            {
                Scene = "01_LotR", ReferenceObject = "Gandalf", Factor = 0.5f, Radius = 0.18f,
                Why = "hobbit: la mitad de la altura de Gandalf, y flaco",
            },
            new SizeRule
            {
                Scene = "02_Principito", ReferenceObject = "El_Zorro", Offset = 1.5f, Radius = 0.5f,
                Why = "1,5 m más alto que el zorro",
            },
            new SizeRule
            {
                Scene = "03_HarryPotter", ReferenceObject = "Dumbledore", Offset = -0.15f, Radius = 0.15f,
                Why = "un poco más bajo que Dumbledore y lo más flaco posible, para pasar por las escaleras",
            },
        };

        [MenuItem("Inkside/Ajustar tamaño del jugador (escena abierta)")]
        public static void ApplyActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            string note = Apply(scene);
            EditorUtility.DisplayDialog("inkside", note, "OK");
        }

        /// <summary>
        /// Aplica la regla de tamaño de esta escena al Player que haya en ella. Devuelve una línea de informe.
        /// Se respeta dónde está parado el jugador: la posición de mundo de la cápsula no se mueve.
        /// </summary>
        public static string Apply(Scene scene)
        {
            SizeRule rule = null;
            foreach (var r in Rules)
                if (r.Scene == scene.name) { rule = r; break; }
            if (rule == null) return string.Format("'{0}' no tiene regla de tamaño de jugador.", scene.name);

            GameObject player = null;
            var byName = new Dictionary<string, Transform>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "Player") player = root;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (!byName.ContainsKey(t.name)) byName[t.name] = t;
            }
            if (player == null) return string.Format("(!) '{0}' no tiene un objeto 'Player'.", scene.name);

            Transform reference;
            if (!byName.TryGetValue(rule.ReferenceObject, out reference))
                return string.Format("(!) '{0}': no se encontró el objeto de referencia '{1}'.",
                    scene.name, rule.ReferenceObject);

            float refHeight;
            if (!TryMeasureHeight(reference.gameObject, out refHeight))
                return string.Format("(!) '{0}': '{1}' no tiene renderers visibles, no se puede medir.",
                    scene.name, rule.ReferenceObject);

            float height = refHeight * rule.Factor + rule.Offset;
            if (height < 0.3f)
                return string.Format("(!) '{0}': la regla da una altura de {1:0.##} m, demasiado baja. Se cancela.",
                    scene.name, height);

            // PhysX exige height >= 2*radius; si no, la cápsula degenera en esfera.
            float radius = Mathf.Min(rule.Radius, height / 2f - 0.02f);
            float eye = height * EyeRatio;
            float centerY = height / 2f;   // base de la cápsula justo en el origen del transform

            var capsule = FindChild(player.transform, "PlayerCapsule");
            if (capsule == null) return string.Format("(!) '{0}': el Player no tiene 'PlayerCapsule'.", scene.name);

            // Dónde está realmente parado el jugador hoy, con los overrides manuales incluidos.
            Vector3 standing = capsule.position;

            // 1. Limpiar el escalado/desplazamiento manual: la escala va a 1 y los hijos vuelven a su sitio.
            player.transform.localScale = Vector3.one;
            ResetChild(capsule, Vector3.zero);
            ResetChild(FindChild(player.transform, "MainCamera"), new Vector3(0f, eye, 0f));
            ResetChild(FindChild(player.transform, "PlayerFollowCamera"), new Vector3(0f, eye, 0f));
            ResetChild(FindChild(player.transform, "UI_EventSystem"), Vector3.zero);

            // 2. Devolver al jugador al mismo punto del mundo donde estaba, ya sin los offsets de los hijos.
            player.transform.position += standing - capsule.position;

            // 3. La altura de ojo en primera persona la da PlayerCameraRoot: es el Tracking Target de la
            //    CinemachineCamera, así que mover sólo las cámaras no cambia nada de lo que se ve.
            var camRoot = FindChild(player.transform, "PlayerCameraRoot");
            if (camRoot != null) camRoot.localPosition = new Vector3(0f, eye, 0f);
            else Debug.LogWarning("[InksidePlayerSizer] No se encontró PlayerCameraRoot.", player);

            // 4. La cápsula de colisión.
            var cc = capsule.GetComponent<CharacterController>();
            if (cc == null) return string.Format("(!) '{0}': PlayerCapsule sin CharacterController.", scene.name);
            cc.height = height;
            cc.radius = radius;
            cc.center = new Vector3(0f, centerY, 0f);
            cc.stepOffset = Mathf.Min(0.25f, height * 0.25f);

            // 5. El FirstPersonController vive en el assembly de Starter Assets: se busca por nombre de tipo
            //    para no tener que referenciarlo desde un script de editor.
            //    GroundedOffset se deduce del radio para que la esfera del chequeo de piso asome siempre 5 cm
            //    por debajo de los pies: con el valor fijo original, un radio chico la dejaba sin tocar el suelo.
            float speedFactor = height / ReferenceHeight;
            foreach (var mb in capsule.GetComponents<MonoBehaviour>())
            {
                if (mb == null || mb.GetType().Name != "FirstPersonController") continue;
                SetFloat(mb, "GroundedRadius", radius);
                SetFloat(mb, "GroundedOffset", 0.05f - radius);
                SetFloat(mb, "MoveSpeed", 4f * speedFactor);
                SetFloat(mb, "SprintSpeed", 6f * speedFactor);
                SetFloat(mb, "JumpHeight", 1.2f * speedFactor);
                break;
            }

            // 6. La cápsula visible es la primitiva de Unity: 2 m de alto y 1 m de diámetro con escala 1.
            var visual = FindChild(capsule, "Capsule");
            if (visual != null)
            {
                visual.localPosition = new Vector3(0f, centerY, 0f);
                visual.localRotation = Quaternion.identity;
                visual.localScale = new Vector3(radius / 0.5f, height / 2f, radius / 0.5f);
            }

            EditorUtility.SetDirty(player);
            Physics.SyncTransforms();

            return string.Format(
                "{0}: jugador de {1:0.##} m de alto x {2:0.##} m de radio, ojo a {3:0.##} m " +
                "({4}; '{5}' mide {6:0.##} m). Velocidad x{7:0.##}. Pies en {8}.",
                scene.name, height, radius, eye, rule.Why, rule.ReferenceObject, refHeight, speedFactor,
                Fmt(capsule.position));
        }

        [MenuItem("Inkside/Apoyar el jugador en el piso (escena abierta)")]
        public static void GroundActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            string note = Ground(scene);
            EditorUtility.DisplayDialog("inkside", note, "OK");
        }

        /// <summary>
        /// Baja (o sube) al jugador hasta el piso, sin moverlo de su XZ. Para saber CUÁL es el piso se usa la
        /// base del objeto de referencia de la escena: Gandalf, el zorro y Dumbledore están parados donde el
        /// jugador tiene que estar parado. De todas las superficies que haya bajo el jugador se elige la más
        /// cercana a esa altura, no la más alta: así no se lo sube al techo del modelo ni se lo deja en el piso
        /// de respaldo que queda por debajo de todo.
        /// </summary>
        public static string Ground(Scene scene)
        {
            SizeRule rule = null;
            foreach (var r in Rules)
                if (r.Scene == scene.name) { rule = r; break; }
            if (rule == null) return string.Format("'{0}' no tiene regla de tamaño de jugador.", scene.name);

            GameObject player = null;
            Transform reference = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "Player") player = root;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == rule.ReferenceObject && reference == null) reference = t;
            }
            if (player == null) return string.Format("(!) '{0}' no tiene un objeto 'Player'.", scene.name);
            if (reference == null)
                return string.Format("(!) '{0}': no se encontró '{1}'.", scene.name, rule.ReferenceObject);

            float referenceFloor;
            if (!TryMeasureBase(reference.gameObject, out referenceFloor))
                return string.Format("(!) '{0}': '{1}' no tiene renderers visibles.", scene.name, rule.ReferenceObject);

            var capsule = FindChild(player.transform, "PlayerCapsule");
            if (capsule == null) return string.Format("(!) '{0}': el Player no tiene 'PlayerCapsule'.", scene.name);
            var cc = capsule.GetComponent<CharacterController>();
            if (cc == null) return string.Format("(!) '{0}': PlayerCapsule sin CharacterController.", scene.name);

            Physics.SyncTransforms();

            Vector3 feet = capsule.position;
            var origin = new Vector3(feet.x, referenceFloor + 20f, feet.z);
            var hits = Physics.RaycastAll(origin, Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore);

            bool found = false;
            float best = 0f;
            foreach (var hit in hits)
            {
                if (Vector3.Dot(hit.normal, Vector3.up) < 0.7f) continue; // paredes y techos no son piso
                if (hit.collider.transform.IsChildOf(player.transform)) continue;
                if (!found || Mathf.Abs(hit.point.y - referenceFloor) < Mathf.Abs(best - referenceFloor))
                {
                    best = hit.point.y;
                    found = true;
                }
            }

            if (!found)
                return string.Format(
                    "(!) '{0}': no se encontró ninguna superficie bajo el jugador (XZ {1:0.##}, {2:0.##}). " +
                    "Movelo a mano sobre el piso.", scene.name, feet.x, feet.z);

            float targetFeet = best + 0.05f;   // 5 cm de aire: el CharacterController lo apoya al arrancar
            float delta = targetFeet - feet.y;
            player.transform.position += new Vector3(0f, delta, 0f);
            EditorUtility.SetDirty(player);
            Physics.SyncTransforms();

            return string.Format(
                "{0}: jugador {1} {2:0.##} m. Pies de {3:0.##} a {4:0.##} (piso en {5:0.##}, " +
                "referencia '{6}' apoyada en {7:0.##}).",
                scene.name, delta >= 0f ? "subido" : "bajado", Mathf.Abs(delta),
                feet.y, targetFeet, best, rule.ReferenceObject, referenceFloor);
        }

        /// <summary>Y más baja del AABB de los renderers activos: donde el objeto apoya.</summary>
        static bool TryMeasureBase(GameObject go, out float baseY)
        {
            baseY = 0f;
            Bounds bounds = default(Bounds);
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.gameObject.activeInHierarchy || !r.enabled) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            if (!any) return false;
            baseY = bounds.min.y;
            return true;
        }

        /// <summary>Altura en metros de mundo del AABB de todos los renderers activos del objeto.</summary>
        static bool TryMeasureHeight(GameObject go, out float height)
        {
            height = 0f;
            Bounds bounds = default(Bounds);
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.gameObject.activeInHierarchy || !r.enabled) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            if (!any) return false;
            height = bounds.size.y;
            return true;
        }

        static void ResetChild(Transform t, Vector3 localPos)
        {
            if (t == null) return;
            t.localPosition = localPos;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }

        static Transform FindChild(Transform parent, string name)
        {
            foreach (var t in parent.GetComponentsInChildren<Transform>(true))
                if (t.name == name && t != parent) return t;
            return null;
        }

        static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogWarning(string.Format("[InksidePlayerSizer] '{0}' no tiene el campo '{1}'.",
                    target.GetType().Name, field), target);
                return;
            }
            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static string Fmt(Vector3 v)
        {
            return string.Format("({0:0.##}, {1:0.##}, {2:0.##})", v.x, v.y, v.z);
        }
    }
}
