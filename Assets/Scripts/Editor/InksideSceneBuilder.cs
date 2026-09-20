using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Inkside.EditorTools
{
    /// <summary>
    /// Construye por API los prefabs comunes (jugador + canvas) y las 4 escenas de inkside.
    ///
    /// Menú:  Inkside > Build All Scenes
    /// CLI:   Unity.exe -batchmode -quit -projectPath "<repo>" -executeMethod Inkside.EditorTools.InksideSceneBuilder.BuildAllScenes -logFile build_log.txt
    ///
    /// Todo lo posicional es aproximado (no hay forma de ver los .glb desde acá): buscá los "TODO ajustar en Fase B".
    /// </summary>
    public static class InksideSceneBuilder
    {
        // ------------------------------------------------------------------ rutas (manifiesto, literales)
        const string ScenesFolder = "Assets/_Escenas";
        const string PlayerPrefabPath = "Assets/_Comunes/Jugador/PlayerInkside.prefab";
        const string CanvasPrefabPath = "Assets/_Comunes/UI/Prefabs/Canvas_UI.prefab";
        const string FloorMatPath = "Assets/_Comunes/Materiales/M_PisoFallback.mat";
        const string SpaceMatPath = "Assets/_Mundos/02_Principito/Materiales/M_FondoEspacio.mat";
        const string LightingSettingsPath = "Assets/_Escenas/LightingSettings_Inkside.lighting";

        const string MainCameraPrefab = "Assets/Starter Assets/Runtime/FirstPersonController/Prefabs/MainCamera.prefab";
        const string FollowCameraPrefab = "Assets/Starter Assets/Runtime/FirstPersonController/Prefabs/PlayerFollowCamera.prefab";
        const string PlayerCapsulePrefab = "Assets/Starter Assets/Runtime/FirstPersonController/Prefabs/PlayerCapsule.prefab";
        const string EventSystemPrefab = "Assets/Starter Assets/Runtime/Mobile/Prefabs/EventSystem/UI_EventSystem.prefab";

        const string CursorTexPath = "Assets/_Comunes/UI/Texturas/target_cursor.png";
        const string PanelTexPath = "Assets/_Comunes/UI/Texturas/panel_dialogo.png";

        const string BookPageTurnSfx = "Assets/_Mundos/00_Biblioteca/Audio/SFX/book_page_turn.mp3";

        static readonly string[] SceneOrder = { "00_Biblioteca", "01_LotR", "02_Principito", "03_HarryPotter" };

        // ------------------------------------------------------------------ specs
        enum Kind { Teleport, Npc, Target }

        class PropSpec
        {
            public string Key;            // identificador para referencias (ej. "table")
            public string ModelPath;
            public bool IsRoom;           // define el piso/centro de la escena, se deja en el origen sin escalar
            public float TargetSize;    // 0 = no se escala
            public Vector3 RotationOffset; // corrección de orientación propia del modelo (pitch/roll que el yaw no cubre)
            public Vector3 Offset;        // relativo al ancla (centro XZ del room, piso en Y)
            public float Yaw;
        }

        class InteractableSpec
        {
            public string Name;
            public string ModelPath;
            public Kind Kind;
            public float TargetSize;    // 0 = no se escala
            public Vector3 RotationOffset; // corrección de orientación propia del modelo (pitch/roll que el yaw no cubre)
            public Vector3 Offset;        // relativo al ancla; Y = piso para NPC (se apoya), centro para objetos flotantes
            public float Yaw;
            public bool Grounded = true;  // true: bounds.min.y = piso + Offset.y ; false: bounds.center = ancla + Offset
            public bool RequireLineOfSight = true; // false = puede quedar fuera de la vista inicial (hay que buscarlo)
            public string OnTopOfProp;    // Key del prop sobre el que apoyarlo (ej. "table")

            public string TargetScene;
            public string SfxPath;
            public string NpcName;
            public string Dialogue;
            public string LoopSfxPath;    // AudioSource 3D en loop sobre el propio objeto
        }

        class SceneSpec
        {
            public string Name;
            public string WorldFolder;     // Assets/_Mundos/<WorldFolder>
            public PropSpec[] Props;
            public InteractableSpec[] Items;
            public string AmbientPath;
            public bool SpaceRoombox;      // 02_Principito: fondo estelar + terreno esférico
            public string TerrainModelPath; // esfera usada como suelo curvo (El Principito: el asteroide B-612)
            public float TerrainDiameter;   // diámetro del asteroide en metros
            public float PlayExtent;        // lado de la zona jugable sobre la cima del asteroide
            public float StarBoxSize;       // lado del cubo de estrellas que rodea todo
            public Color LightColor;
            public Color AmbientColor;
            public Color Tint;
            public float Bloom;
            public float Saturation;
        }

        // ------------------------------------------------------------------ estado del reporte
        static readonly StringBuilder Report = new StringBuilder();
        static readonly List<string> Pending = new List<string>();
        static int _errors;

        // ================================================================== ENTRY POINT
        [MenuItem("Inkside/Build All Scenes")]
        public static void BuildAllScenes()
        {
            Report.Clear();
            Pending.Clear();
            _errors = 0;

            Log("=== inkside: InksideSceneBuilder — inicio ===");

            try
            {
                EnsureFolder(ScenesFolder);
                EnsureFolder("Assets/_Comunes/Materiales");
                EnsureFolder("Assets/_Comunes/Jugador");
                EnsureFolder("Assets/_Comunes/UI/Prefabs");

                // Paso 0: pre-requisitos (una sola vez)
                EnsureTmpEssentials();
                var reticleSprite = EnsureSprite(CursorTexPath);
                var panelSprite = EnsureSprite(PanelTexPath);

                // Escena temporal para armar prefabs y assets compartidos
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var playerPrefab = BuildPlayerPrefab();
                var canvasPrefab = BuildCanvasPrefab(reticleSprite, panelSprite);
                var floorMat = EnsureMaterial(FloorMatPath, new Color(0.16f, 0.13f, 0.11f), Color.black);
                var spaceMat = EnsureMaterial(SpaceMatPath, new Color(0.01f, 0.012f, 0.03f), new Color(0.02f, 0.03f, 0.09f));
                var lightingSettings = EnsureLightingSettings();

                // Paso 1..N: las 4 escenas
                foreach (var spec in GetSceneSpecs())
                {
                    try
                    {
                        BuildScene(spec, playerPrefab, canvasPrefab, floorMat, spaceMat, lightingSettings);
                    }
                    catch (Exception e)
                    {
                        _errors++;
                        Debug.LogError($"[InksideSceneBuilder] Falló la escena {spec.Name}: {e}");
                        Report.AppendLine($"  !! ERROR en {spec.Name}: {e.Message}");
                    }
                }

                // Build Settings, en orden. Sólo si todo salió bien: registrar una escena que falló y no llegó a
                // guardarse dejaría un proyecto que "parece" construido, con una entrada apuntando a un archivo viejo o inexistente.
                if (_errors > 0)
                {
                    Log($"Build Settings NO se actualiza: hubo {_errors} error(es).");
                }
                else
                {
                    EditorBuildSettings.scenes = SceneOrder
                        .Select(n => new EditorBuildSettingsScene($"{ScenesFolder}/{n}.unity", true))
                        .ToArray();
                    Log("Build Settings: " + string.Join(", ", SceneOrder));
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (Exception e)
            {
                _errors++;
                Debug.LogError($"[InksideSceneBuilder] Error fatal: {e}");
            }

            // Resumen final
            var summary = new StringBuilder();
            summary.AppendLine("=== inkside: RESUMEN DE CONSTRUCCIÓN ===");
            summary.Append(Report);
            summary.AppendLine();
            summary.AppendLine("--- Pendientes de ajuste manual (Fase B) ---");
            if (Pending.Count == 0) summary.AppendLine("  (ninguno registrado)");
            foreach (var p in Pending.Distinct()) summary.AppendLine("  - " + p);
            summary.AppendLine($"--- Errores: {_errors} ---");
            summary.AppendLine("=== inkside: InksideSceneBuilder — fin ===");

            if (_errors > 0) Debug.LogError(summary.ToString());
            else Debug.Log(summary.ToString());

            // En modo batch el código de salida es el único canal que ve quien invoca por CLI:
            // sin esto un build con errores termina en 0 y se toma por exitoso.
            if (_errors > 0 && Application.isBatchMode)
                EditorApplication.Exit(1);
        }

        // ================================================================== SPECS
        static IEnumerable<SceneSpec> GetSceneSpecs()
        {
            // TODO ajustar en Fase B: todos los Offset/Yaw/TargetSize son aproximaciones a ciegas.
            yield return new SceneSpec
            {
                Name = "00_Biblioteca",
                WorldFolder = "00_Biblioteca",
                Props = new[]
                {
                    // library.glb: sala de 4.5 x 3 x 4.8 m en metros (piso en y≈2.14 en el archivo) → sin escalar
                    new PropSpec { Key = "room", ModelPath = "Assets/_Mundos/00_Biblioteca/Modelos/library.glb", IsRoom = true },
                    // wooden_table.glb: la mesa real mide 7.15 x 2.47 x 2.68 (el plano de 68x68 del archivo se descarta) → 2.2 m de largo, ~0.76 m de alto. TODO ajustar en Fase B
                    new PropSpec { Key = "table", ModelPath = "Assets/_Mundos/00_Biblioteca/Modelos/wooden_table.glb", TargetSize = 1.7f, Offset = new Vector3(0f, 0f, 1.4f) },
                },
                Items = new[]
                {
                    // Tamaños reales leídos del glTF: book_lotr 0.04x0.19x0.14 (caja de DVD), book_historico 4.5x0.4x3.2 (abierto), book_hp 32x34x9 (parado)
                    new InteractableSpec { Name = "Libro_LOTR", ModelPath = "Assets/_Mundos/00_Biblioteca/Modelos/book_lotr.glb", Kind = Kind.Teleport, TargetSize = 0.24f, OnTopOfProp = "table", Offset = new Vector3(-0.45f, 0f, 0f), Yaw = 10f, TargetScene = "01_LotR", SfxPath = BookPageTurnSfx },
                    new InteractableSpec { Name = "Libro_Principito", ModelPath = "Assets/_Mundos/00_Biblioteca/Modelos/book_historico_animado.glb", Kind = Kind.Teleport, TargetSize = 0.34f, OnTopOfProp = "table", Offset = new Vector3(0f, 0f, 0f), Yaw = 0f, TargetScene = "02_Principito", SfxPath = BookPageTurnSfx },
                    new InteractableSpec { Name = "Libro_HarryPotter", ModelPath = "Assets/_Mundos/00_Biblioteca/Modelos/book_harrypotter.glb", Kind = Kind.Teleport, TargetSize = 0.28f, RotationOffset = new Vector3(0f, 0f, 180f), OnTopOfProp = "table", Offset = new Vector3(0.45f, 0f, 0f), Yaw = -10f, TargetScene = "03_HarryPotter", SfxPath = BookPageTurnSfx },
                },
                AmbientPath = "Assets/_Mundos/00_Biblioteca/Audio/Ambiente/rain_interior_loop.mp3",
                LightColor = new Color(1f, 0.87f, 0.7f),
                AmbientColor = new Color(0.32f, 0.27f, 0.22f),
                Tint = new Color(1f, 0.95f, 0.85f),
                Bloom = 0.35f,
                Saturation = -5f,
            };

            yield return new SceneSpec
            {
                Name = "01_LotR",
                WorldFolder = "01_LotR",
                Props = new[]
                {
                    // hobbit_house.glb: 2593 x 1207 x 2199 unidades (centímetros) → ~24 m de lado. TODO ajustar en Fase B
                    new PropSpec { Key = "room", ModelPath = "Assets/_Mundos/01_LotR/Modelos/hobbit_house.glb", IsRoom = true, TargetSize = 24f },
                },
                Items = new[]
                {
                    new InteractableSpec { Name = "Gandalf", ModelPath = "Assets/_Mundos/01_LotR/Modelos/gandalf_blanco.glb", Kind = Kind.Npc, TargetSize = 1.8f, Offset = new Vector3(0.8f, 0f, 3f), Yaw = 180f, NpcName = "Gandalf",
                        Dialogue = "Llegas a tiempo, viajero. La sombra en el Este se extiende y el destino de la Tierra Media pende de un hilo. Encuentra el Anillo Único antes de que su llamada atraiga al enemigo." },
                    new InteractableSpec { Name = "Anillo_Unico", ModelPath = "Assets/_Mundos/01_LotR/Modelos/one_ring.glb", Kind = Kind.Target, TargetSize = 0.06f, Grounded = false, Offset = new Vector3(-1.8f, 1.2f, 2.2f), SfxPath = "Assets/_Mundos/01_LotR/Audio/SFX/ring_whisper.mp3" },
                },
                AmbientPath = "Assets/_Mundos/01_LotR/Audio/Ambiente/countryside_birds_ambient.mp3",
                LightColor = new Color(1f, 0.92f, 0.78f),
                AmbientColor = new Color(0.35f, 0.32f, 0.25f),
                Tint = new Color(1f, 0.96f, 0.88f),
                Bloom = 0.4f,
                Saturation = 5f,
            };

            yield return new SceneSpec
            {
                Name = "02_Principito",
                WorldFolder = "02_Principito",
                SpaceRoombox = true,
                // El asteroide es el SUELO del diorama, no un adorno (GDD acto 2). Con 44 m de diámetro la cima
                // es una superficie caminable y su curvatura esconde el otro lado: la rosa deja de verse de entrada.
                TerrainModelPath = "Assets/_Mundos/02_Principito/Modelos/asteroide_superficie.glb",
                TerrainDiameter = 44f,
                PlayExtent = 32f,
                StarBoxSize = 120f,
                Props = new[]
                {
                    new PropSpec { Key = "lamp", ModelPath = "Assets/_Mundos/02_Principito/Modelos/farol_calle.glb", TargetSize = 3.5f, Offset = new Vector3(3.5f, 0f, 3f) },
                    // Rocas sueltas: el mismo modelo del asteroide a escala chica, para que la superficie tenga relieve.
                    new PropSpec { Key = "roca1", ModelPath = "Assets/_Mundos/02_Principito/Modelos/asteroide_superficie.glb", TargetSize = 1.6f, Offset = new Vector3(-4f, -0.5f, 5.5f) },
                    new PropSpec { Key = "roca2", ModelPath = "Assets/_Mundos/02_Principito/Modelos/asteroide_superficie.glb", TargetSize = 1f, Offset = new Vector3(5.5f, -0.3f, -3f) },
                    new PropSpec { Key = "roca3", ModelPath = "Assets/_Mundos/02_Principito/Modelos/asteroide_superficie.glb", TargetSize = 2.2f, Offset = new Vector3(-6f, -0.6f, -5f) },
                },
                Items = new[]
                {
                    new InteractableSpec { Name = "El_Zorro", ModelPath = "Assets/_Mundos/02_Principito/Modelos/fox.glb", Kind = Kind.Npc, TargetSize = 0.7f, Offset = new Vector3(1f, 0f, 3.5f), Yaw = 180f, NpcName = "El Zorro",
                        Dialogue = "Solo con el corazón se puede ver bien; lo esencial es invisible para los ojos. Encuentra la rosa que mi amigo dejó protegida antes de que el frío del espacio la marchite." },
                    // Del otro lado de la cima: hay que caminar y rodear el asteroide para encontrarla.
                    new InteractableSpec { Name = "Rosa", ModelPath = "Assets/_Mundos/02_Principito/Modelos/rose.glb", Kind = Kind.Target, TargetSize = 0.8f, RequireLineOfSight = false, Offset = new Vector3(-2f, 0f, -14f), SfxPath = "Assets/_Mundos/02_Principito/Audio/SFX/glass_sparkle.mp3" },
                },
                AmbientPath = "Assets/_Mundos/02_Principito/Audio/Ambiente/space_ambient_pad.mp3",
                LightColor = new Color(0.75f, 0.8f, 1f),
                AmbientColor = new Color(0.12f, 0.12f, 0.25f),
                Tint = new Color(0.9f, 0.9f, 1f),
                Bloom = 0.6f,
                Saturation = 0f,
            };

            yield return new SceneSpec
            {
                Name = "03_HarryPotter",
                WorldFolder = "03_HarryPotter",
                Props = new[]
                {
                    // dumbledores_office.glb: 0.14 x 0.13 x 0.09 unidades (exportado a escala 0.01) → ~14 m de lado. TODO ajustar en Fase B
                    new PropSpec { Key = "room", ModelPath = "Assets/_Mundos/03_HarryPotter/Modelos/dumbledores_office.glb", IsRoom = true, TargetSize = 14f },
                },
                Items = new[]
                {
                    new InteractableSpec { Name = "Dumbledore", ModelPath = "Assets/_Mundos/03_HarryPotter/Modelos/dumbledore.glb", Kind = Kind.Npc, TargetSize = 1.8f, Offset = new Vector3(0.8f, 0f, 3f), Yaw = 180f, NpcName = "Dumbledore",
                        Dialogue = "Ah, viajero, llegas en buen momento. Un pequeño problema se nos ha escapado de las manos... o mejor dicho, de las alas. Una Snitch Dorada se liberó durante la última práctica de Quidditch y anda revoloteando libre por mi oficina. ¿Serías tan amable de atraparla antes de que rompa algo... otra vez?" },
                    new InteractableSpec { Name = "Snitch_Dorada", ModelPath = "Assets/_Mundos/03_HarryPotter/Modelos/golden_snitch.glb", Kind = Kind.Target, TargetSize = 0.28f /* envergadura de alas, agrandada para que se pueda encontrar */, Grounded = false, Offset = new Vector3(-1.8f, 1.6f, 2.2f), SfxPath = "Assets/_Mundos/03_HarryPotter/Audio/SFX/magic_grab.mp3", LoopSfxPath = "Assets/_Mundos/03_HarryPotter/Audio/SFX/fluttering_wings.mp3" },
                },
                AmbientPath = "Assets/_Mundos/03_HarryPotter/Audio/Ambiente/magic_mystery_ambient.mp3",
                LightColor = new Color(1f, 0.85f, 0.6f),
                AmbientColor = new Color(0.3f, 0.25f, 0.15f),
                Tint = new Color(1f, 0.93f, 0.78f),
                Bloom = 0.5f,
                Saturation = 5f,
            };
        }

        // ================================================================== PRE-REQUISITOS
        static void EnsureTmpEssentials()
        {
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0)
            {
                Log("TMP Essential Resources: ya estaban importados.");
                return;
            }

            string pkg = null;
            try
            {
                string basePath = TMPro.EditorUtilities.TMP_EditorUtility.packageFullPath;
                pkg = Path.Combine(basePath, "Package Resources", "TMP Essential Resources.unitypackage");
                if (!File.Exists(pkg))
                {
                    string alt = Path.Combine(Path.GetFullPath("Packages/com.unity.ugui"), "Package Resources", "TMP Essential Resources.unitypackage");
                    if (File.Exists(alt)) pkg = alt;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[InksideSceneBuilder] No se pudo resolver la ruta del paquete TMP: " + e.Message);
            }

            if (pkg == null || !File.Exists(pkg))
            {
                Pending.Add("Importar TMP Essentials a mano: Window > TextMeshPro > Import TMP Essential Resources (no se encontró el .unitypackage).");
                Debug.LogWarning("[InksideSceneBuilder] TMP Essential Resources.unitypackage no encontrado.");
                return;
            }

            Log("TMP Essential Resources: importando desde " + pkg);
            AssetDatabase.ImportPackage(pkg, false);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0)
            {
                Log("TMP Essential Resources: importados OK.");
            }
            else
            {
                Pending.Add("Importar TMP Essentials a mano: Window > TextMeshPro > Import TMP Essential Resources (la importación por batch no dejó TMP Settings).");
                Debug.LogWarning("[InksideSceneBuilder] Tras importar el paquete no se encontró TMP_Settings.");
            }
        }

        static Sprite EnsureSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                _errors++;
                Debug.LogError($"[InksideSceneBuilder] No existe la textura {path}");
                return null;
            }

            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
                Log($"Textura {Path.GetFileName(path)}: import type cambiado a Sprite (2D and UI).");
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                _errors++;
                Debug.LogError($"[InksideSceneBuilder] No se pudo cargar el Sprite de {path} (¿import type?).");
            }
            return sprite;
        }

        static Material EnsureMaterial(string path, Color baseColor, Color emission)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.2f);
            if (emission.maxColorComponent > 0f && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateAsset(mat, path);
            Log($"Material placeholder creado: {path}  // TODO reemplazar en Fase B");
            Pending.Add($"Reemplazar el material placeholder {path} por una textura real.");
            return mat;
        }

        static LightingSettings EnsureLightingSettings()
        {
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingSettingsPath);
            if (ls != null) return ls;
            ls = new LightingSettings
            {
                name = "LightingSettings_Inkside",
                bakedGI = false,
                realtimeGI = false,
                autoGenerate = false,
            };
            AssetDatabase.CreateAsset(ls, LightingSettingsPath);
            Log("LightingSettings 100% realtime creado: " + LightingSettingsPath);
            return ls;
        }

        // ================================================================== PREFAB DEL JUGADOR
        static GameObject BuildPlayerPrefab()
        {
            var root = new GameObject("Player");

            var mainCamera = InstantiatePrefabChild(MainCameraPrefab, root.transform, "MainCamera");
            var followCamera = InstantiatePrefabChild(FollowCameraPrefab, root.transform, "PlayerFollowCamera");
            var capsule = InstantiatePrefabChild(PlayerCapsulePrefab, root.transform, "PlayerCapsule");
            var eventSystem = InstantiatePrefabChild(EventSystemPrefab, root.transform, "UI_EventSystem");

            // Mismas posiciones relativas que NestedParent_Unpack.prefab (cámaras 1.375 m sobre el capsule).
            if (capsule != null) capsule.transform.localPosition = Vector3.zero;
            if (mainCamera != null) mainCamera.transform.localPosition = new Vector3(0f, 1.375f, 0f);
            if (followCamera != null) followCamera.transform.localPosition = new Vector3(0f, 1.375f, 0f);
            if (eventSystem != null) eventSystem.transform.localPosition = Vector3.zero;

            if (capsule != null && capsule.tag != "Player") capsule.tag = "Player";

            if (mainCamera != null)
            {
                // RaycastInteractor en la cámara real (hermana del jugador, no hija)
                if (mainCamera.GetComponent<RaycastInteractor>() == null)
                    mainCamera.AddComponent<RaycastInteractor>();

                // Post-procesado ON para que el Global Volume (Bloom/Color) se vea
                var camData = mainCamera.GetComponent<UniversalAdditionalCameraData>();
                if (camData != null) SetBool(camData, "m_RenderPostProcessing", true);
            }

            // El binding Follow → PlayerCameraRoot vive como override en el asset original (NestedParent_Unpack); lo replicamos.
            if (followCamera != null && capsule != null)
            {
                var cineCam = followCamera.GetComponent<CinemachineCamera>();
                var camRoot = FindDeepChild(capsule.transform, "PlayerCameraRoot");
                if (cineCam != null && camRoot != null)
                    SetRef(cineCam, "Target.TrackingTarget", camRoot);
                else
                    Debug.LogWarning("[InksideSceneBuilder] No se pudo enlazar PlayerFollowCamera → PlayerCameraRoot.");
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath, out bool ok);
            Object.DestroyImmediate(root);
            if (!ok || prefab == null)
            {
                _errors++;
                Debug.LogError("[InksideSceneBuilder] No se pudo guardar " + PlayerPrefabPath);
            }
            else Log("Prefab creado: " + PlayerPrefabPath);
            return prefab;
        }

        static GameObject InstantiatePrefabChild(string path, Transform parent, string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
            {
                _errors++;
                Debug.LogError("[InksideSceneBuilder] Prefab no encontrado: " + path);
                return null;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.transform.SetParent(parent, false);
            go.name = name;
            return go;
        }

        // ================================================================== PREFAB DEL CANVAS
        static GameObject BuildCanvasPrefab(Sprite reticleSprite, Sprite panelSprite)
        {
            var canvasGO = new GameObject("Canvas_UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            TMP_FontAsset font = null;
            try { font = TMP_Settings.defaultFontAsset; } catch { /* sin TMP essentials */ }
            if (font == null) Pending.Add("Los TMP_Text del Canvas no tienen fuente asignada (faltan TMP Essentials); importarlos y reconstruir.");

            // --- Reticle
            var reticle = NewRect("Reticle", canvasGO.transform);
            var reticleImg = reticle.gameObject.AddComponent<Image>();
            reticleImg.sprite = reticleSprite;
            reticleImg.raycastTarget = false;
            reticleImg.preserveAspect = true;
            if (reticleSprite == null) reticleImg.color = new Color(1f, 1f, 1f, 0.8f);
            reticle.anchorMin = reticle.anchorMax = new Vector2(0.5f, 0.5f);
            reticle.pivot = new Vector2(0.5f, 0.5f);
            reticle.anchoredPosition = Vector2.zero;
            reticle.sizeDelta = new Vector2(32f, 32f);

            // --- InteractionPrompt
            var prompt = NewRect("InteractionPrompt", canvasGO.transform);
            prompt.anchorMin = prompt.anchorMax = new Vector2(0.5f, 0.5f);
            prompt.pivot = new Vector2(0.5f, 1f);
            prompt.anchoredPosition = new Vector2(0f, -40f);
            prompt.sizeDelta = new Vector2(700f, 60f);
            var promptUI = prompt.gameObject.AddComponent<InteractionPromptUI>();

            var promptText = NewRect("PromptText", prompt);
            Stretch(promptText);
            var promptTmp = promptText.gameObject.AddComponent<TextMeshProUGUI>();
            SetupText(promptTmp, font, "[E] Interactuar", 30f, TextAlignmentOptions.Center, Color.white);
            promptText.gameObject.SetActive(false); // oculto por defecto
            SetRef(promptUI, "promptText", promptTmp);

            // --- DialoguePanel
            // Sin el sprite panel_dialogo.png: mide 547x365 y el panel lo estiraba a ~1459x292, deformándolo
            // 2.7x a lo ancho. Queda una banda negra translúcida generada por código, que no se deforma nunca,
            // con el texto en blanco encima.
            var panel = NewRect("DialoguePanel", canvasGO.transform);
            panel.anchorMin = new Vector2(0.15f, 0.06f);
            panel.anchorMax = new Vector2(0.85f, 0.26f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            var panelImg = panel.gameObject.AddComponent<Image>();
            panelImg.sprite = null;
            panelImg.raycastTarget = false;
            panelImg.color = new Color(0f, 0f, 0f, 0.5f);
            var panelUI = panel.gameObject.AddComponent<DialoguePanelUI>();

            var nameText = NewRect("NPCNameText", panel);
            nameText.anchorMin = new Vector2(0.04f, 0.66f);
            nameText.anchorMax = new Vector2(0.96f, 0.95f);
            nameText.offsetMin = nameText.offsetMax = Vector2.zero;
            var nameTmp = nameText.gameObject.AddComponent<TextMeshProUGUI>();
            SetupText(nameTmp, font, "Nombre", 30f, TextAlignmentOptions.Left, new Color(1f, 0.85f, 0.5f));
            nameTmp.fontStyle = FontStyles.Bold;

            var bodyText = NewRect("DialogueBodyText", panel);
            bodyText.anchorMin = new Vector2(0.04f, 0.06f);
            bodyText.anchorMax = new Vector2(0.96f, 0.64f);
            bodyText.offsetMin = bodyText.offsetMax = Vector2.zero;
            var bodyTmp = bodyText.gameObject.AddComponent<TextMeshProUGUI>();
            SetupText(bodyTmp, font, "Línea de diálogo", 24f, TextAlignmentOptions.TopLeft, Color.white);
            bodyTmp.textWrappingMode = TextWrappingModes.Normal;
            bodyTmp.enableAutoSizing = true;
            bodyTmp.fontSizeMin = 16f;
            bodyTmp.fontSizeMax = 24f;

            SetRef(panelUI, "npcNameText", nameTmp);
            SetRef(panelUI, "dialogueBodyText", bodyTmp);
            panel.gameObject.SetActive(false); // oculto por defecto

            // --- FadeImage (último hijo: se dibuja encima de todo)
            var fade = NewRect("FadeImage", canvasGO.transform);
            Stretch(fade);
            var fadeImg = fade.gameObject.AddComponent<Image>();
            fadeImg.color = Color.black;
            fadeImg.raycastTarget = false;
            var fadeGroup = fade.gameObject.AddComponent<CanvasGroup>();
            fadeGroup.alpha = 1f;               // arranca en negro; ScreenFader.Start() lo desvanece
            fadeGroup.blocksRaycasts = false;
            fadeGroup.interactable = false;
            var fader = fade.gameObject.AddComponent<ScreenFader>();
            SetRef(fader, "fadeGroup", fadeGroup);
            SetFloat(fader, "fadeDuration", 1f);

            // En batch no hay pase de layout del Canvas, así que el RectTransform raíz se guardaría con escala 0.
            // Unity la recalcula en runtime para Screen Space - Overlay, pero se deja un valor sano por las dudas.
            canvasGO.transform.localScale = Vector3.one;

            var prefab = PrefabUtility.SaveAsPrefabAsset(canvasGO, CanvasPrefabPath, out bool ok);
            Object.DestroyImmediate(canvasGO);
            if (!ok || prefab == null)
            {
                _errors++;
                Debug.LogError("[InksideSceneBuilder] No se pudo guardar " + CanvasPrefabPath);
            }
            else Log("Prefab creado: " + CanvasPrefabPath);
            return prefab;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void SetupText(TextMeshProUGUI tmp, TMP_FontAsset font, string text, float size, TextAlignmentOptions align, Color color)
        {
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = color;
            tmp.raycastTarget = false;
        }

        // ================================================================== ESCENAS
        static void BuildScene(SceneSpec spec, GameObject playerPrefab, GameObject canvasPrefab, Material floorMat, Material spaceMat, LightingSettings lightingSettings)
        {
            Report.AppendLine();
            Report.AppendLine($"[{spec.Name}]");

            // 1. Escena vacía de verdad (sin cámara ni luz por defecto → un solo AudioListener)
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var envRoot = new GameObject("Environment");
            var interRoot = new GameObject("Interactables");
            var lightRoot = new GameObject("Lighting");

            // 2. Environment: rooms primero (definen piso y centro)
            var placedProps = new Dictionary<string, GameObject>();
            bool hasRoom = false;
            Bounds roomBounds = new Bounds(Vector3.zero, Vector3.zero);

            foreach (var prop in spec.Props.Where(p => p.IsRoom))
            {
                var go = InstantiateModel(prop.ModelPath, envRoot.transform);
                if (go == null) continue;
                go.transform.position = Vector3.zero; // TODO ajustar en Fase B
                FitSize(go, prop.TargetSize);          // normaliza unidades (cm → m, etc.) según el tamaño real leído del .glb
                int colliders = AddMeshColliders(go);
                if (TryGetBounds(go, out var b))
                {
                    roomBounds = hasRoom ? Encapsulate(roomBounds, b) : b;
                    hasRoom = true;
                    Report.AppendLine($"  Environment {go.name}: bounds center={F(b.center)} size={F(b.size)} — {colliders} MeshCollider(s)");
                }
                else Report.AppendLine($"  Environment {go.name}: sin Renderers (!)");
                placedProps[prop.Key] = go;
            }

            // Terreno esférico: el asteroide no es un adorno, es el suelo del diorama (GDD acto 2).
            // Se lo coloca con su cima en y=0, así el jugador camina sobre una superficie curva y el horizonte
            // esconde lo que está del otro lado.
            if (!string.IsNullOrEmpty(spec.TerrainModelPath))
            {
                var terrain = InstantiateModel(spec.TerrainModelPath, envRoot.transform);
                if (terrain != null)
                {
                    terrain.name = "Asteroide_Terreno";
                    FitSize(terrain, spec.TerrainDiameter);
                    if (TryGetBounds(terrain, out var tb))
                        terrain.transform.position += new Vector3(-tb.center.x, -tb.max.y, -tb.center.z);
                    int tc = AddMeshColliders(terrain);
                    TryGetBounds(terrain, out var tb2);
                    Report.AppendLine($"  Terreno {terrain.name}: diámetro {F(tb2.size)} cima en y={tb2.max.y:0.00} — {tc} MeshCollider(s)");
                }
            }

            // Ancla: centro XZ del room y su piso (bounds.min.y). Principito: cima del asteroide en y=0.
            float floorY;
            Vector3 anchor;
            Vector3 envSize;
            if (spec.SpaceRoombox)
            {
                envSize = new Vector3(spec.PlayExtent, 12f, spec.PlayExtent);
                floorY = 0f;
                anchor = Vector3.zero;
                BuildStarBox(envRoot.transform, spec.StarBoxSize, spaceMat);
                Report.AppendLine($"  Fondo estelar de {spec.StarBoxSize:0} m con material {spaceMat.name}, zona jugable {spec.PlayExtent:0} m");
            }
            else if (hasRoom)
            {
                envSize = roomBounds.size;
                floorY = roomBounds.min.y;
                anchor = new Vector3(roomBounds.center.x, floorY, roomBounds.center.z);
            }
            else
            {
                envSize = new Vector3(20f, 6f, 20f);
                floorY = 0f;
                anchor = Vector3.zero;
                Report.AppendLine("  (!) Sin modelo de room: ancla en el origen.");
            }
            Report.AppendLine($"  Ancla (centro XZ, piso Y) = {F(anchor)}  // TODO ajustar en Fase B");

            // 3. Player — el spawn NO se asume en el centro del modelo: se busca por física un punto con piso
            //    debajo y sin geometría encima (el centro de los bounds suele caer dentro de un mueble o una pared).
            //    El piso de respaldo se crea DESPUÉS de esta búsqueda a propósito: si ya estuviera puesto, el
            //    buscador podría elegirlo y dejar al jugador parado debajo del modelo en vez de adentro.
            Physics.SyncTransforms();
            Vector3 spawnRoot;
            float groundY;
            if (!TryFindSpawn(new Bounds(new Vector3(anchor.x, floorY + envSize.y * 0.5f, anchor.z), envSize), floorY, out spawnRoot, out groundY))
            {
                spawnRoot = anchor + new Vector3(0f, 0.15f, 0f);
                groundY = floorY;
                Report.AppendLine("  (!) No se encontró un spawn libre por física: se usa el centro del ancla.");
                Pending.Add($"{spec.Name}: no se encontró spawn libre automáticamente, revisar la posición del Player a mano.");
            }
            float facingYaw = FindOpenYaw(spawnRoot);
            var facing = Quaternion.Euler(0f, facingYaw, 0f);

            var playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            playerInstance.name = "Player";
            playerInstance.transform.position = spawnRoot;           // TODO ajustar en Fase B
            playerInstance.transform.rotation = facing;              // mira hacia el lado más despejado
            Physics.SyncTransforms();
            Report.AppendLine($"  Player spawn={F(spawnRoot)} piso={groundY:0.##} mirando yaw={facingYaw:0}° (búsqueda por física)");

            // En el asteroide no hace falta piso de respaldo (el suelo es la esfera), pero sí un límite lateral
            // para que el jugador no baje por la curvatura hasta la pendiente imposible y se caiga al vacío.
            if (spec.SpaceRoombox)
            {
                BuildBoundaryWalls(envRoot.transform, new Vector3(anchor.x, groundY - 2f, anchor.z), spec.PlayExtent, 20f);
                Physics.SyncTransforms();
            }

            // Piso de respaldo (material plano) + paredes invisibles: red de seguridad para que el jugador no
            // caiga al vacío ni salga del diorama si el modelo no trae piso o paredes con collider.
            if (!spec.SpaceRoombox)
            {
                // Ceñido al modelo real: un mínimo fijo dejaba un plano de 20 m y paredes a 10 m del centro
                // en una sala de 4,5 m, o sea muy por fuera del diorama.
                float extent = Mathf.Max(envSize.x, envSize.z) * 1.05f + 0.5f;
                float fallbackY = Mathf.Min(floorY, groundY) - 0.01f;
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "Fallback_Floor"; // TODO ajustar en Fase B: quitar si el modelo ya trae piso con collider
                floor.transform.SetParent(envRoot.transform, false);
                floor.transform.position = new Vector3(anchor.x, fallbackY, anchor.z);
                floor.transform.localScale = new Vector3(extent / 10f, 1f, extent / 10f);
                floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;
                BuildBoundaryWalls(envRoot.transform, new Vector3(anchor.x, fallbackY, anchor.z), extent, Mathf.Max(envSize.y, 4f) + 4f);
                Pending.Add($"{spec.Name}: revisar Fallback_Floor / Bounds_Walls (placeholder) contra el piso y las paredes reales del modelo.");
                Physics.SyncTransforms();
            }

            // Origen de colocación: todo lo demás se ubica RELATIVO al jugador y a su dirección de mirada,
            // así los offsets "+Z = adelante" del spec caen siempre en el campo visual inicial.
            var placeOrigin = new Vector3(spawnRoot.x, groundY, spawnRoot.z);
            Vector3 eye = spawnRoot + new Vector3(0f, 1.375f, 0f);
            var playerRoot = playerInstance.transform;

            // Props no-room (mesa, farol, asteroide), apoyados en el piso y esquivando geometría
            foreach (var prop in spec.Props.Where(p => !p.IsRoom))
            {
                var go = InstantiateModel(prop.ModelPath, envRoot.transform);
                if (go == null) continue;
                FitSize(go, prop.TargetSize);
                ApplyRotationOffset(go, prop.RotationOffset);
                string note = PlaceClear(go, placeOrigin, facing, facingYaw, prop.Offset, prop.Yaw, true, eye, false, playerRoot);
                int colliders = AddMeshColliders(go);
                if (TryGetBounds(go, out var b))
                    Report.AppendLine($"  Prop {go.name}: pos={F(go.transform.position)} scale={go.transform.localScale.x:0.###} size={F(b.size)} — {colliders} MeshCollider(s) [{note}]");
                placedProps[prop.Key] = go;
                Physics.SyncTransforms();
            }

            // Ambiente 2D en loop
            var ambientClip = LoadClip(spec.AmbientPath);
            if (ambientClip != null)
            {
                var src = envRoot.AddComponent<AudioSource>();
                src.clip = ambientClip;
                src.loop = true;
                src.playOnAwake = true;
                src.spatialBlend = 0f;
                src.volume = 0.6f;
            }

            // 4. UI Canvas
            var canvasInstance = (GameObject)PrefabUtility.InstantiatePrefab(canvasPrefab);
            canvasInstance.name = "UI Canvas";
            var fader = canvasInstance.GetComponentInChildren<ScreenFader>(true);
            var promptUI = canvasInstance.GetComponentInChildren<InteractionPromptUI>(true);
            var dialoguePanel = canvasInstance.GetComponentInChildren<DialoguePanelUI>(true);
            if (fader == null || promptUI == null || dialoguePanel == null)
            {
                _errors++;
                Debug.LogError($"[InksideSceneBuilder] {spec.Name}: la instancia del Canvas no tiene todos los componentes de UI.");
            }

            var interactor = playerInstance.GetComponentInChildren<RaycastInteractor>(true);
            if (interactor != null) SetRef(interactor, "promptUI", promptUI);
            else Debug.LogWarning($"[InksideSceneBuilder] {spec.Name}: el Player no tiene RaycastInteractor.");

            // 5. Interactables
            foreach (var item in spec.Items)
            {
                var go = InstantiateModel(item.ModelPath, interRoot.transform);
                if (go == null) continue;
                go.name = item.Name;

                Vector3 sizeBefore = TryGetBounds(go, out var b0) ? b0.size : Vector3.zero;
                FitSize(go, item.TargetSize);
                ApplyRotationOffset(go, item.RotationOffset);

                string placeNote;
                if (!string.IsNullOrEmpty(item.OnTopOfProp))
                {
                    // Sobre un soporte conocido (la mesa): se apoya en su cara superior, repartido según la mirada del jugador.
                    Vector3 target;
                    if (placedProps.TryGetValue(item.OnTopOfProp, out var support) && TryGetBounds(support, out var sb))
                    {
                        var side = facing * new Vector3(item.Offset.x, 0f, item.Offset.z);
                        target = new Vector3(sb.center.x + side.x, sb.max.y + item.Offset.y, sb.center.z + side.z);
                        placeNote = $"sobre {item.OnTopOfProp}";
                    }
                    else
                    {
                        target = placeOrigin + facing * item.Offset + new Vector3(0f, 0.8f, 0f); // TODO ajustar en Fase B: no se encontró el soporte
                        placeNote = "sin soporte (!)";
                    }
                    PlaceGrounded(go, target, facingYaw + item.Yaw);
                }
                else
                {
                    // Relativo al jugador: busca un lugar libre y con línea de visión desde el ojo.
                    placeNote = PlaceClear(go, placeOrigin, facing, facingYaw, item.Offset, item.Yaw, item.Grounded, eye, item.RequireLineOfSight, playerRoot);
                }

                bool isNpc = item.Kind == Kind.Npc;
                var box = AddBoxCollider(go, isNpc, isNpc ? 1.6f : 1.05f, isNpc ? 1.5f : 0.25f);

                switch (item.Kind)
                {
                    case Kind.Teleport:
                    {
                        var tp = go.AddComponent<SceneTeleport>();
                        SetString(tp, "targetSceneName", item.TargetScene);
                        SetRef(tp, "pageTurnSfx", LoadClip(item.SfxPath));
                        SetRef(tp, "fader", fader);
                        break;
                    }
                    case Kind.Target:
                    {
                        var tr = go.AddComponent<ObjectTargetReturn>();
                        SetRef(tr, "collectSfx", LoadClip(item.SfxPath));
                        SetRef(tr, "visualToHide", go);
                        SetRef(tr, "fader", fader);
                        SetString(tr, "hubSceneName", "00_Biblioteca");
                        if (!string.IsNullOrEmpty(item.LoopSfxPath))
                        {
                            var loop = LoadClip(item.LoopSfxPath);
                            if (loop != null)
                            {
                                // El bucle va en un objeto hermano, no en el propio objetivo: al recogerlo,
                                // ObjectTargetReturn desactiva su visual y eso cortaría el sonido de golpe.
                                var audioHost = new GameObject(go.name + "_LoopAudio");
                                audioHost.transform.SetParent(go.transform.parent, false);
                                audioHost.transform.position = go.transform.position;
                                var src = audioHost.AddComponent<AudioSource>();
                                src.clip = loop;
                                src.loop = true;
                                src.playOnAwake = true;
                                src.spatialBlend = 1f;
                                src.rolloffMode = AudioRolloffMode.Linear;
                                src.minDistance = 1f;
                                src.maxDistance = 20f;
                                src.volume = 1f;

                                // Un destello propio: un objeto de 28 cm en una oficina de 14 m en penumbra
                                // es imposible de ver sin ayuda visual.
                                var glowGO = new GameObject(go.name + "_Glow");
                                glowGO.transform.SetParent(go.transform.parent, false);
                                glowGO.transform.position = b1Center(go);
                                var glow = glowGO.AddComponent<Light>();
                                glow.type = LightType.Point;
                                glow.color = new Color(1f, 0.85f, 0.4f);
                                glow.intensity = 3f;
                                glow.range = 4f;
                                glow.shadows = LightShadows.None;
                            }
                        }
                        break;
                    }
                    case Kind.Npc:
                    {
                        var dt = go.AddComponent<DialogueTrigger>();
                        SetString(dt, "npcName", item.NpcName);
                        SetString(dt, "dialogueLine", item.Dialogue);
                        SetRef(dt, "dialoguePanel", dialoguePanel);
                        break;
                    }
                }

                TryGetBounds(go, out var b1);
                Report.AppendLine($"  {item.Kind} {go.name}: pos={F(go.transform.position)} dist={Vector3.Distance(eye, b1.center):0.0}m scale={go.transform.localScale.x:0.####} size(orig)={F(sizeBefore)} size(final)={F(b1.size)} collider={F(box.size)}{(box.isTrigger ? " trigger" : "")} [{placeNote}]");
                Physics.SyncTransforms();
            }

            // 6. Lighting + Global Volume
            BuildLighting(spec, lightRoot.transform, anchor, envSize, lightingSettings);

            // Orden en jerarquía
            playerInstance.transform.SetSiblingIndex(0);
            envRoot.transform.SetSiblingIndex(1);
            interRoot.transform.SetSiblingIndex(2);
            lightRoot.transform.SetSiblingIndex(3);
            canvasInstance.transform.SetSiblingIndex(4);

            // 7. Guardar
            string scenePath = $"{ScenesFolder}/{spec.Name}.unity";
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                _errors++;
                Debug.LogError("[InksideSceneBuilder] No se pudo guardar " + scenePath);
            }
            else Report.AppendLine($"  Guardada: {scenePath}");

            int listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            if (listeners != 1) Debug.LogWarning($"[InksideSceneBuilder] {spec.Name}: hay {listeners} AudioListener(s), se esperaba 1.");
            Report.AppendLine($"  AudioListeners: {listeners}");

            Pending.Add($"{spec.Name}: verificar posiciones/escala reales de cada modelo y la Y de spawn del jugador.");
        }

        /// <summary>
        /// Cubo de estrellas que rodea el diorama. No lleva piso a la altura de juego: el suelo es el asteroide.
        /// El piso del cubo queda muy abajo, sólo como red de seguridad.
        /// </summary>
        static void BuildStarBox(Transform parent, float size, Material mat)
        {
            var box = new GameObject("Fondo_Estelar");
            box.transform.SetParent(parent, false);
            float t = 1f;
            float h = size / 2f;
            void Wall(string name, Vector3 center, Vector3 scale)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                c.name = name;
                c.transform.SetParent(box.transform, false);
                c.transform.localPosition = center;
                c.transform.localScale = scale;
                c.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
            // TODO ajustar en Fase B: reemplazar el material por una textura real de estrellas/nebulosa (GDD 4.5)
            Wall("Techo", new Vector3(0f, h, 0f), new Vector3(size, t, size));
            Wall("Piso_Lejano", new Vector3(0f, -h, 0f), new Vector3(size, t, size));
            Wall("Pared_N", new Vector3(0f, 0f, h), new Vector3(size, size, t));
            Wall("Pared_S", new Vector3(0f, 0f, -h), new Vector3(size, size, t));
            Wall("Pared_E", new Vector3(h, 0f, 0f), new Vector3(t, size, size));
            Wall("Pared_O", new Vector3(-h, 0f, 0f), new Vector3(t, size, size));
        }


        static void BuildBoundaryWalls(Transform parent, Vector3 floorCenter, float extent, float height)
        {
            var root = new GameObject("Bounds_Walls"); // colliders invisibles en los límites del diorama (GDD 4.5)
            root.transform.SetParent(parent, false);
            root.transform.position = floorCenter;
            float half = extent / 2f;
            void Wall(string name, Vector3 center, Vector3 size)
            {
                var w = new GameObject(name);
                w.transform.SetParent(root.transform, false);
                w.transform.localPosition = center;
                var bc = w.AddComponent<BoxCollider>();
                bc.size = size;
            }
            Wall("Wall_N", new Vector3(0f, height / 2f, half), new Vector3(extent, height, 0.5f));
            Wall("Wall_S", new Vector3(0f, height / 2f, -half), new Vector3(extent, height, 0.5f));
            Wall("Wall_E", new Vector3(half, height / 2f, 0f), new Vector3(0.5f, height, extent));
            Wall("Wall_W", new Vector3(-half, height / 2f, 0f), new Vector3(0.5f, height, extent));
        }

        static void BuildLighting(SceneSpec spec, Transform parent, Vector3 anchor, Vector3 envSize, LightingSettings lightingSettings)
        {
            var sunGO = new GameObject("Directional Light");
            sunGO.transform.SetParent(parent, false);
            sunGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var sun = sunGO.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = spec.LightColor;
            sun.intensity = spec.SpaceRoombox ? 0.6f : 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.lightmapBakeType = LightmapBakeType.Realtime;

            var pointGO = new GameObject("Point Light (foco)");
            pointGO.transform.SetParent(parent, false);
            pointGO.transform.position = anchor + new Vector3(0f, Mathf.Clamp(envSize.y * 0.6f, 2f, 4f), 2f); // TODO ajustar en Fase B
            var point = pointGO.AddComponent<Light>();
            point.type = LightType.Point;
            point.color = spec.LightColor;
            // La direccional queda bloqueada por el techo en los interiores cerrados, así que la point light es
            // la que realmente ilumina: su intensidad y alcance tienen que crecer con el tamaño de la sala.
            float room = Mathf.Max(envSize.x, envSize.z);
            point.intensity = Mathf.Lerp(2.5f, 8f, Mathf.InverseLerp(5f, 24f, room));
            point.range = Mathf.Max(10f, room * 1.1f);
            point.shadows = LightShadows.None;
            point.lightmapBakeType = LightmapBakeType.Realtime;

            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = spec.AmbientColor;
            RenderSettings.fog = false;
            if (lightingSettings != null) Lightmapping.lightingSettings = lightingSettings;

            // Global Volume con Bloom + Color Adjustments sutil
            string profilePath = $"Assets/_Mundos/{spec.WorldFolder}/Materiales/VP_{spec.Name}.asset";
            var profile = CreateVolumeProfile(profilePath, spec.Bloom, spec.Tint, spec.Saturation);
            var volGO = new GameObject("Global Volume");
            volGO.transform.SetParent(parent, false);
            var volume = volGO.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.weight = 1f;
            volume.priority = 0f;
            volume.sharedProfile = profile;
            Report.AppendLine($"  Lighting: Directional + Point (intensidad {point.intensity:0.0}, rango {point.range:0.#}), ambient flat, Global Volume → {profilePath}");
            Pending.Add($"{spec.Name}: verificar la iluminación interior a ojo (la luz direccional no entra en un volumen cerrado).");
        }

        static VolumeProfile CreateVolumeProfile(string path, float bloomIntensity, Color tint, float saturation)
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));

            // Se reutiliza el asset existente vaciándolo, en vez de borrarlo y recrearlo: borrar cambia el GUID
            // en cada build, lo que rompe cualquier referencia externa y ensucia el diff de git.
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            else
            {
                foreach (var component in profile.components.ToArray())
                {
                    profile.components.Remove(component);
                    Object.DestroyImmediate(component, true);
                }
            }

            var bloom = profile.Add<Bloom>(false);
            bloom.intensity.Override(bloomIntensity);
            bloom.threshold.Override(0.9f);
            bloom.scatter.Override(0.7f);
            AssetDatabase.AddObjectToAsset(bloom, profile);

            var color = profile.Add<ColorAdjustments>(false);
            color.colorFilter.Override(tint);
            color.saturation.Override(saturation);
            color.postExposure.Override(0.15f);
            AssetDatabase.AddObjectToAsset(color, profile);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            return profile;
        }

        // ================================================================== MODELOS / COLLIDERS / BOUNDS
        static GameObject LoadModel(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
            {
                // Los .meta pueden haber quedado con DefaultImporter de antes de instalar glTFast: forzar reimport.
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            var importer = AssetImporter.GetAtPath(path);
            string importerName = importer != null ? importer.GetType().Name : "(sin importer)";
            if (asset == null)
            {
                _errors++;
                Debug.LogError($"[InksideSceneBuilder] No se pudo cargar el modelo {path} (importer: {importerName}). ¿glTFast instalado / .glb reimportado?");
            }
            else if (importerName != "GltfImporter")
            {
                Debug.LogWarning($"[InksideSceneBuilder] {path} se importó con {importerName}, no con glTFast.");
            }
            return asset;
        }

        static GameObject InstantiateModel(string path, Transform parent)
        {
            var asset = LoadModel(path);
            if (asset == null)
            {
                var placeholder = GameObject.CreatePrimitive(PrimitiveType.Cube);
                placeholder.name = "MISSING_" + Path.GetFileNameWithoutExtension(path);
                placeholder.transform.SetParent(parent, false);
                Pending.Add($"Modelo no cargado: {path} (se dejó un cubo placeholder).");
                return placeholder;
            }

            GameObject go = null;
            try { go = PrefabUtility.InstantiatePrefab(asset) as GameObject; }
            catch (Exception e) { Debug.LogWarning($"[InksideSceneBuilder] InstantiatePrefab falló para {path}: {e.Message}; se usa Instantiate."); }
            if (go == null) go = Object.Instantiate(asset);

            go.name = Path.GetFileNameWithoutExtension(path);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            StripOversizedFlatMeshes(go);
            // OJO: NO resetear localRotation. glTFast deja en el root la rotación del nodo raíz del glTF
            // (Sketchfab exporta Z-up → Y-up como una rotación de 270° en X en ese nodo); si se pisa, el modelo queda acostado.
            return go;
        }

        static int AddMeshColliders(GameObject go)
        {
            int added = 0;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                if (!mf.gameObject.activeInHierarchy) continue;
                if (mf.GetComponent<Collider>() != null) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = false;
                added++;
            }
            if (added == 0) Debug.LogWarning($"[InksideSceneBuilder] {go.name}: no se agregó ningún MeshCollider (¿sin MeshFilters?).");
            return added;
        }

        static BoxCollider AddBoxCollider(GameObject go, bool trigger, float sizeMultiplier, float minWorldSize)
        {
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = trigger;

            var lossy = go.transform.lossyScale;
            if (Mathf.Abs(lossy.x) < 1e-12f || Mathf.Abs(lossy.y) < 1e-12f || Mathf.Abs(lossy.z) < 1e-12f)
            {
                // Escala degenerada: cualquier conversión mundo↔local daría basura. Es un error, no un caso a parchear.
                _errors++;
                Debug.LogError($"[InksideSceneBuilder] {go.name} tiene escala degenerada {lossy}; no se puede dimensionar el BoxCollider.", go);
                return box;
            }
            var scale = new Vector3(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y), Mathf.Abs(lossy.z));

            // Los bounds se calculan directamente en el espacio LOCAL del root. Convertir un AABB de mundo
            // dividiendo por la escala sería incorrecto: los modelos traen una rotación de 270° en X del glTF,
            // así que los ejes locales no coinciden con los del mundo y el tamaño quedaría permutado entre ejes.
            if (TryGetLocalBounds(go, out var lb))
            {
                var minLocal = new Vector3(minWorldSize / scale.x, minWorldSize / scale.y, minWorldSize / scale.z);
                box.center = lb.center;
                box.size = Vector3.Max(lb.size * sizeMultiplier, minLocal);
            }
            else
            {
                box.center = Vector3.zero;
                box.size = new Vector3(minWorldSize / scale.x, minWorldSize / scale.y, minWorldSize / scale.z);
            }
            return box;
        }

        /// <summary>
        /// AABB de todos los meshes hijos expresado en el espacio local del root (el que espera BoxCollider).
        /// </summary>
        static bool TryGetLocalBounds(GameObject go, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            var toLocal = go.transform.worldToLocalMatrix;

            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.gameObject.activeInHierarchy) continue;
                if (!TryGetRendererBounds(r, toLocal, out var b)) continue;
                if (!any) { bounds = b; any = true; }
                else bounds.Encapsulate(b);
            }
            return any;
        }

        /// <summary>
        /// AABB mundial combinando TODOS los renderers hijos. Se calcula a mano desde los bounds locales de cada mesh
        /// y su localToWorldMatrix (equivale a Renderer.bounds, pero no depende de que el renderer haya sido actualizado).
        /// </summary>
        static bool TryGetBounds(GameObject go, out Bounds bounds)
        {
            bounds = new Bounds(go.transform.position, Vector3.zero);
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.gameObject.activeInHierarchy) continue;
                if (!TryGetRendererBounds(r, Matrix4x4.identity, out var b)) continue;
                if (!any) { bounds = b; any = true; }
                else bounds.Encapsulate(b);
            }
            return any;
        }

        /// <summary>
        /// Bounds de un renderer transformados al espacio que indica <paramref name="toSpace"/>
        /// (identidad = mundo; worldToLocalMatrix de un root = espacio local de ese root).
        /// </summary>
        static bool TryGetRendererBounds(Renderer r, Matrix4x4 toSpace, out Bounds bounds)
        {
            bounds = default;
            if (!TryGetMeshBounds(r, out var local)) return false;

            var m = toSpace * r.transform.localToWorldMatrix;
            var min = local.min;
            var max = local.max;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                var p = m.MultiplyPoint3x4(corner);
                if (i == 0) bounds = new Bounds(p, Vector3.zero);
                else bounds.Encapsulate(p);
            }
            return true;
        }

        /// <summary>Bounds del mesh de un renderer, en su espacio local. false si no tiene mesh utilizable.</summary>
        static bool TryGetMeshBounds(Renderer r, out Bounds local)
        {
            local = default;
            if (r is SkinnedMeshRenderer smr)
            {
                if (smr.sharedMesh == null) return false;
                local = smr.sharedMesh.bounds;
                return true;
            }
            if (r is MeshRenderer)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) return false;
                local = mf.sharedMesh.bounds;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Algunos .glb traen un plano auxiliar de suelo/sombra que no forma parte del objeto pero domina sus bounds
        /// y arruina el escalado. Caso real: wooden_table.glb incluye un cuadrilátero de 4 vértices de 68x68 y espesor 0,
        /// mientras la mesa de verdad mide 7.15 x 2.47 x 2.68; escalar por el plano dejaba la mesa del tamaño de una caja de fósforos.
        /// Se desactivan sólo los planos perfectamente chatos, de muy pocos vértices y desproporcionados respecto del resto.
        /// </summary>
        static int StripOversizedFlatMeshes(GameObject go)
        {
            var flats = new List<(GameObject target, float size, string name)>();
            float solidMax = 0f;
            var toLocal = go.transform.worldToLocalMatrix;

            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                // El tamaño se mide en el espacio del root, no en el de la malla: glTFast deja la escala en los
                // nodos. El plano de wooden_table.glb es una malla de 2x2 en un nodo escalado x34, así que
                // medido en espacio de malla parecería más chico que la mesa.
                if (!TryGetRendererBounds(r, toLocal, out var b)) continue;
                var mf = r.GetComponent<MeshFilter>();
                int vertexCount = mf != null && mf.sharedMesh != null ? mf.sharedMesh.vertexCount : int.MaxValue;

                var size = b.size;
                float max = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                float min = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
                bool isFlat = min <= max * 1e-3f;

                if (isFlat && vertexCount <= 64) flats.Add((r.gameObject, max, r.name));
                else solidMax = Mathf.Max(solidMax, max);
            }

            int stripped = 0;
            foreach (var (target, size, name) in flats)
            {
                if (solidMax <= 0f || size <= solidMax * 3f) continue; // proporcionado: es parte del modelo
                target.SetActive(false);
                stripped++;
                Report.AppendLine($"    (plano auxiliar descartado en {go.name}: '{name}', {size:0.#} vs {solidMax:0.#} del modelo real)");
            }
            return stripped;
        }

        static Bounds Encapsulate(Bounds a, Bounds b)
        {
            a.Encapsulate(b.min);
            a.Encapsulate(b.max);
            return a;
        }

        /// <summary>
        /// Corrige la orientación propia del modelo, en su espacio local, antes de cualquier yaw de colocación.
        /// Hace falta porque cada .glb viene con la orientación que le dejó su autor y el yaw solo gira en Y:
        /// book_harrypotter.glb, por ejemplo, viene dado vuelta y necesita 180° en su Z.
        /// </summary>
        static Vector3 b1Center(GameObject go) => TryGetBounds(go, out var b) ? b.center : go.transform.position;

        static void ApplyRotationOffset(GameObject go, Vector3 eulerOffset)
        {
            if (eulerOffset == Vector3.zero) return;
            go.transform.rotation = go.transform.rotation * Quaternion.Euler(eulerOffset);
        }

        /// <summary>
        /// Escala uniformemente el root para que su dimensión MÁXIMA (bounds) sea targetSize. 0 = no escalar.
        /// Se usa la dimensión máxima y no la altura porque no sabemos con certeza qué eje es "arriba" en cada .glb.
        /// </summary>
        static void FitSize(GameObject go, float targetSize)
        {
            if (targetSize <= 0f) return;
            if (!TryGetBounds(go, out var b)) return;
            float current = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (current < 1e-5f) return;
            go.transform.localScale *= targetSize / current;
        }

        // ================================================================== COLOCACIÓN POR FÍSICA
        // Medidas del CharacterController de PlayerCapsule.prefab (height 2, radius 0.5, center (0, 0.93, 0)).
        const float CcHeight = 2f, CcRadius = 0.5f, CcCenterY = 0.93f;

        /// <summary>Espacio libre mínimo debajo de un objetivo flotante para que no parezca apoyado en algo.</summary>
        const float MinFloatClearance = 1.2f;

        /// <summary>
        /// Busca una posición de spawn con piso debajo y sin geometría atravesando la cápsula del jugador.
        /// Recorre anillos desde el centro hacia afuera y devuelve el primer punto libre (el más cercano al centro).
        /// </summary>
        static bool TryFindSpawn(Bounds room, float floorY, out Vector3 rootPos, out float groundY)
        {
            rootPos = Vector3.zero;
            groundY = floorY;
            float maxRadius = Mathf.Max(1f, Mathf.Min(room.size.x, room.size.z) * 0.45f);

            // Alturas de sondeo, de la más baja a la más alta. La primera (media altura, tope 3 m) es la que sirve
            // en interiores normales: salir desde arriba de todo haría que el rayo se quedara con el techo.
            // Las siguientes cubren el caso en que el piso jugable esté muy por encima del punto más bajo del modelo.
            float[] probeHeights =
            {
                Mathf.Min(room.size.y * 0.5f, 3f),
                room.size.y * 0.5f,
                room.size.y * 0.75f,
                room.size.y * 0.95f,
            };

            // Primera pasada exigiendo espacio abierto alrededor; segunda sin ese requisito, para no quedarnos
            // sin spawn en salas muy angostas.
            for (int pass = 0; pass < 2; pass++)
            {
                bool requireOpenSides = pass == 0;
                foreach (var probeHeight in probeHeights)
                {
                    float rayTop = floorY + probeHeight;
                    float rayLength = probeHeight + 3f;

                    for (float radius = 0f; radius <= maxRadius; radius += Mathf.Max(0.6f, maxRadius / 8f))
                    {
                        int steps = radius < 0.01f ? 1 : 12;
                        for (int i = 0; i < steps; i++)
                        {
                            float angle = i * (360f / steps);
                            var offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
                            var probe = new Vector3(room.center.x + offset.x, rayTop, room.center.z + offset.z);

                            var hits = Physics.RaycastAll(probe, Vector3.down, rayLength, ~0, QueryTriggerInteraction.Ignore);
                            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                            foreach (var hit in hits)
                            {
                                if (Vector3.Dot(hit.normal, Vector3.up) < 0.7f) continue; // paredes y techos no son piso
                                var candidate = new Vector3(probe.x, hit.point.y + 0.12f, probe.z);
                                if (!IsCapsuleFree(candidate, requireOpenSides)) continue;

                                rootPos = candidate;
                                groundY = hit.point.y;
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        static bool IsCapsuleFree(Vector3 rootPos, bool requireOpenSides)
        {
            var center = rootPos + new Vector3(0f, CcCenterY, 0f);
            float half = CcHeight / 2f - CcRadius;
            if (Physics.OverlapCapsule(center + Vector3.up * half, center - Vector3.up * half, CcRadius * 0.9f, ~0, QueryTriggerInteraction.Ignore).Length > 0)
                return false;

            if (!requireOpenSides) return true;

            // Un overlap contra MeshCollider cóncavo sólo detecta intersección con triángulos, no contención:
            // un punto encerrado dentro de un mueble o del espesor de un muro da "libre". Se exige además que
            // haya espacio abierto en varias direcciones.
            int openSides = 0;
            for (int i = 0; i < 8; i++)
            {
                var dir = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                if (!Physics.Raycast(center, dir, 2f, ~0, QueryTriggerInteraction.Ignore)) openSides++;
            }
            return openSides >= 3;
        }

        /// <summary>Devuelve el yaw con más espacio libre delante, para que el jugador no arranque mirando una pared.</summary>
        static float FindOpenYaw(Vector3 rootPos)
        {
            var eye = rootPos + new Vector3(0f, 1.375f, 0f);
            float bestClearance = -1f, bestYaw = 0f;
            for (int i = 0; i < 24; i++)
            {
                float yaw = i * 15f;
                var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                float clearance = Physics.Raycast(eye, dir, out var hit, 40f, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : 40f;
                if (clearance > bestClearance) { bestClearance = clearance; bestYaw = yaw; }
            }
            return bestYaw;
        }

        /// <summary>
        /// Coloca el objeto en origin + facing*offset; si ahí queda embebido en geometría (o tapado, cuando se pide
        /// línea de visión), prueba variantes girando el offset alrededor del jugador y acercándolo, y se queda con la primera que sirva.
        /// Devuelve una nota para el log.
        /// </summary>
        static string PlaceClear(GameObject go, Vector3 origin, Quaternion facing, float facingYaw, Vector3 offset, float itemYaw,
            bool grounded, Vector3 eye, bool needLineOfSight, Transform playerRoot)
        {
            float[] angles = { 0f, 25f, -25f, 50f, -50f, 80f, -80f, 120f, -120f, 180f };
            float[] scales = { 1f, 0.8f, 0.65f, 1.2f };
            // El yaw se compone sobre la rotación que trae el modelo del glTF, así que hay que partir siempre
            // de la original en cada intento; si no, las rotaciones se acumulan intento tras intento.
            var baseRotation = go.transform.rotation;

            foreach (var scale in scales)
            {
                foreach (var angle in angles)
                {
                    var moved = Quaternion.Euler(0f, angle, 0f) * new Vector3(offset.x * scale, offset.y, offset.z * scale);
                    var target = origin + facing * moved;
                    float yaw = facingYaw + angle + itemYaw; // el objeto acompaña el giro para seguir encarando al jugador

                    go.transform.rotation = baseRotation;
                    if (grounded) PlaceGrounded(go, target, yaw);
                    else PlaceCentered(go, target, yaw);
                    if (!TryGetBounds(go, out var b)) return "sin renderers";

                    // En una pendiente, la base de un objeto apoyado se entierra en el terreno y la consulta de
                    // obstrucción lo daba por bloqueado. Se recorta el 30% inferior: el contacto con el suelo no
                    // cuenta como obstáculo, pero una pared o un mueble sí.
                    var queryBounds = b;
                    if (grounded)
                    {
                        float trim = b.size.y * 0.3f;
                        queryBounds = new Bounds(
                            new Vector3(b.center.x, b.center.y + trim * 0.5f, b.center.z),
                            new Vector3(b.size.x, Mathf.Max(b.size.y - trim, 0.05f), b.size.z));
                    }
                    if (Blocked(queryBounds, go.transform, playerRoot)) continue;
                    if (needLineOfSight && Occluded(eye, b.center, go.transform, playerRoot)) continue;

                    // Un objetivo que "flota" tiene que flotar de verdad: si hay algo justo debajo queda
                    // apoyado sobre un mueble o la copa de un árbol, que fue lo que pasó con el Anillo.
                    if (!grounded && RestsOnSomething(go, b)) continue;

                    bool moved2 = Mathf.Abs(angle) > 0.01f || Mathf.Abs(scale - 1f) > 0.01f;
                    return moved2 ? $"reubicado {angle:0}° x{scale:0.##}" : "posición prevista";
                }
            }

            // Ninguna variante sirvió: se deja la original y queda anotado para Fase B.
            var fallback = origin + facing * offset;
            go.transform.rotation = baseRotation;
            if (grounded) PlaceGrounded(go, fallback, facingYaw + itemYaw);
            else PlaceCentered(go, fallback, facingYaw + itemYaw);
            Pending.Add($"'{go.name}': no se encontró una ubicación libre/visible automáticamente, reubicarlo a mano.");
            return "!! sin lugar libre";
        }

        /// <summary>true si hay una superficie justo debajo del objeto, o sea que no está flotando sino apoyado.</summary>
        static bool RestsOnSomething(GameObject go, Bounds b)
        {
            var from = new Vector3(b.center.x, b.min.y - 0.02f, b.center.z);
            foreach (var hit in Physics.RaycastAll(from, Vector3.down, MinFloatClearance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(go.transform)) continue;
                return true;
            }
            return false;
        }

        static bool Blocked(Bounds b, Transform self, Transform playerRoot)
        {
            // Caja al 70% para tolerar el contacto normal con el piso o el mueble de apoyo.
            foreach (var col in Physics.OverlapBox(b.center, b.extents * 0.7f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            {
                if (col.transform.IsChildOf(self)) continue;
                if (playerRoot != null && col.transform.IsChildOf(playerRoot)) continue;
                return true;
            }
            return false;
        }

        static bool Occluded(Vector3 eye, Vector3 target, Transform self, Transform playerRoot)
        {
            var dir = target - eye;
            float dist = dir.magnitude;
            if (dist < 0.01f) return false;
            foreach (var hit in Physics.RaycastAll(eye, dir / dist, dist - 0.05f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(self)) continue;
                if (playerRoot != null && hit.transform.IsChildOf(playerRoot)) continue;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Apoya el objeto sobre el piso que tiene REALMENTE debajo, en su propio XZ, y centra su XZ en target.
        /// No alcanza con clavar todo a una única altura de piso: los modelos de interior tienen alfombras,
        /// escalones y pisos irregulares, y un objeto lejos del spawn queda hundido o flotando.
        /// target.y se usa como altura de referencia y como respaldo si no hay nada debajo.
        /// </summary>
        static void PlaceGrounded(GameObject go, Vector3 target, float yaw)
        {
            // El yaw se COMPONE con la rotación original del root (que glTFast trae del glTF), no la reemplaza.
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * go.transform.rotation;
            go.transform.position = target;
            if (!TryGetBounds(go, out var b)) return;

            // Primero el XZ, porque el piso se busca en la vertical definitiva del objeto.
            go.transform.position += new Vector3(target.x - b.center.x, 0f, target.z - b.center.z);
            if (!TryGetBounds(go, out b)) return;

            float floorHere = FindSurfaceUnder(go, b, target.y);
            go.transform.position += new Vector3(0f, floorHere - b.min.y, 0f);
        }

        /// <summary>
        /// Altura de la superficie horizontal más alta que hay debajo del objeto (alfombra, escalón, mesa),
        /// buscada en su propio XZ. Si no encuentra nada razonable devuelve fallbackY.
        /// </summary>
        static float FindSurfaceUnder(GameObject go, Bounds b, float fallbackY, float maxDrop = 8f)
        {
            var from = new Vector3(b.center.x, b.max.y + 1f, b.center.z);
            float length = (b.max.y + 1f) - (fallbackY - maxDrop);

            float best = float.NegativeInfinity;
            foreach (var hit in Physics.RaycastAll(from, Vector3.down, length, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(go.transform)) continue;
                if (Vector3.Dot(hit.normal, Vector3.up) < 0.7f) continue;   // paredes y techos no son piso
                if (hit.point.y > b.max.y) continue;                        // por encima del objeto: no lo sostiene
                if (hit.point.y < fallbackY - maxDrop) continue;            // demasiado abajo: es otro nivel
                if (hit.point.y > best) best = hit.point.y;
            }
            return float.IsNegativeInfinity(best) ? fallbackY : best;
        }

        /// <summary>Coloca el objeto con el centro de sus bounds en target.</summary>
        static void PlaceCentered(GameObject go, Vector3 target, float yaw)
        {
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * go.transform.rotation;
            go.transform.position = target;
            if (TryGetBounds(go, out var b))
                go.transform.position += target - b.center;
        }

        static AudioClip LoadClip(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                _errors++;
                Debug.LogError("[InksideSceneBuilder] AudioClip no encontrado: " + path);
            }
            return clip;
        }

        // ================================================================== SERIALIZED PROPERTY HELPERS
        static SerializedProperty Prop(Object target, string name)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(name);
            if (p == null)
            {
                _errors++;
                Debug.LogError($"[InksideSceneBuilder] {target.GetType().Name} no tiene la propiedad serializada '{name}'.");
            }
            return p;
        }

        static void SetRef(Object target, string name, Object value)
        {
            var p = Prop(target, name);
            if (p == null) return;
            p.objectReferenceValue = value;
            p.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            if (value == null) Debug.LogWarning($"[InksideSceneBuilder] {target.name}.{name} quedó en null.");
        }

        static void SetString(Object target, string name, string value)
        {
            var p = Prop(target, name);
            if (p == null) return;
            p.stringValue = value;
            p.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetFloat(Object target, string name, float value)
        {
            var p = Prop(target, name);
            if (p == null) return;
            p.floatValue = value;
            p.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetBool(Object target, string name, bool value)
        {
            var p = Prop(target, name);
            if (p == null) return;
            p.boolValue = value;
            p.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        // ================================================================== UTILS
        static Transform FindDeepChild(Transform parent, string name)
        {
            foreach (var t in parent.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        static void EnsureFolder(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || AssetDatabase.IsValidFolder(assetPath)) return;
            string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            string leaf = Path.GetFileName(assetPath);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static string F(Vector3 v) => $"({v.x:0.##}, {v.y:0.##}, {v.z:0.##})";

        static void Log(string msg)
        {
            Report.AppendLine(msg);
            Debug.Log("[InksideSceneBuilder] " + msg);
        }
    }
}
