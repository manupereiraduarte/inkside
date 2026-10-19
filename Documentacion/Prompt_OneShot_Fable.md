# Prompt one-shot — Construcción de "inkside" en Unity

> Este archivo es el prompt para pegar como primer mensaje a un agente nuevo (Fable) con acceso a archivos/shell sobre este mismo repositorio. Está escrito para que el agente no necesite el resto de esta conversación: contiene todo el contexto necesario. Después de que corra esto una vez, las siguientes interacciones son iteraciones puntuales de ajuste, no reconstrucciones completas.

---

## PROMPT (copiar todo lo que sigue)

Actuás como Lead Unity Developer. Vas a construir de una sola pasada el proyecto Unity de "inkside — Biblioteca de Relatos Virtuales", un entorno 3D en primera persona con 4 escenas (un hub tipo biblioteca + 3 dioramas temáticos). Tenés acceso a archivos y shell sobre este repositorio, pero **no** tenés acceso a la interfaz gráfica del Editor de Unity (no podés hacer click, arrastrar assets ni abrir ventanas). Por eso, todo lo que se pueda resolver por código se resuelve por código (scripts C# + un script de Editor que arma las escenas por API), y lo que dependa de juicio visual queda documentado como pendiente para una segunda pasada manual.

### 0. Contexto que ya existe en el repo (no lo rehagas)

- **El proyecto Unity 6 LTS (URP) ya existe y está andando** en esta misma carpeta (`ProjectSettings/`, `Packages/`, `Library/`, etc. ya están). No lo recrees ni lo toques.
- Ya están instalados: `com.unity.cloud.gltfast` (los `.glb` ya importan con preview 3D, confirmado), `com.unity.inputsystem`, `com.unity.render-pipelines.universal`, `com.unity.cinemachine`.
- Ya está importado el paquete de personaje "First & Third Person Character Controllers" en `Assets/Starter Assets/` (con espacio en el nombre — **no la reorganices**). Rutas exactas en la sección 1.
- La carpeta `Assets/` además tiene nuestra estructura de contenido (`_Mundos/`, `_Comunes/`, `Scripts/`, `_Escenas/`) con los assets 3D/audio/UI ya organizados y nombrados (ver manifiesto completo en la sección "Manifiesto de assets" más abajo). **No renombres ni muevas estos archivos.**
- El documento de diseño completo está en [`Documentacion/GDD.md`](GDD.md) — leelo primero, tiene la narrativa completa, el catálogo de assets y las decisiones de arquitectura ya tomadas. Este prompt resume y operacionaliza lo mismo, pero el GDD es la fuente de verdad si hay alguna duda de contenido/narrativa.

**Tu trabajo arranca en el paso 1** — la creación del proyecto y la instalación de paquetes/Starter Assets ya están hechas.

### 1. Prefab del jugador

**Ya se verificó leyendo el YAML de los prefabs originales (no hace falta re-investigar esto):** el sistema de jugador de este paquete son **4 objetos hermanos**, no un solo prefab combinado. Así los arma el propio asset en `Assets/Starter Assets/Sample/FirstPersonController/Playground.unity` / `NestedParent_Unpack.prefab`:

| Objeto | Ruta del prefab | Rol |
|---|---|---|
| `MainCamera` | `Assets/Starter Assets/Runtime/FirstPersonController/Prefabs/MainCamera.prefab` | Cámara real que renderiza (`CinemachineBrain`). **Poné acá `RaycastInteractor.cs`** — no es hijo del jugador. |
| `PlayerFollowCamera` | `Assets/Starter Assets/Runtime/FirstPersonController/Prefabs/PlayerFollowCamera.prefab` | Cinemachine Virtual Camera. Su `Follow` ya viene apuntando, dentro del prefab, al `PlayerCameraRoot` de `PlayerCapsule` — **no toques ese binding**. |
| `PlayerCapsule` | `Assets/Starter Assets/Runtime/FirstPersonController/Prefabs/PlayerCapsule.prefab` | `CharacterController` + `FirstPersonController.cs`. **Asignale el tag `"Player"`** (es el único con collider físico; `DialogueTrigger` depende de este tag). |
| `UI_EventSystem` | `Assets/Starter Assets/Runtime/Mobile/Prefabs/EventSystem/UI_EventSystem.prefab` | EventSystem estándar para que la UI funcione. |

Agrupá los 4 bajo un GameObject vacío llamado `Player` y guardalo como `Assets/_Comunes/Jugador/PlayerInkside.prefab` (nested prefab, igual que hace `NestedParent_Unpack.prefab` — Unity lo soporta bien). Si el anidado te da problemas al instanciarlo en las escenas, la alternativa es "Unpack Completely" sobre cada instancia (por eso el asset original lo llama "_Unpack"). No modifiques la lógica interna de `FirstPersonController.cs` ni el input.

### 2. Prefab de UI común

**Antes de armar el Canvas:** `target_cursor.png` y `panel_dialogo.png` necesitan tener el import type **"Sprite (2D and UI)"** para poder asignarse como `Sprite` de una `Image`. Por defecto Unity los importa como textura genérica — si no forzás `TextureImporter.textureType = TextureImporterType.Sprite` (y reimportás) antes de intentar cargarlos con `AssetDatabase.LoadAssetAtPath<Sprite>(...)`, el método devuelve `null` sin ningún error y la UI queda invisible.

Creá `Assets/_Comunes/UI/Prefabs/Canvas_UI.prefab` con esta jerarquía (Canvas en modo *Screen Space - Overlay*):

```
Canvas_UI
├── Reticle (Image, sprite = target_cursor.png, centrado, ~32x32px)
├── InteractionPrompt (GameObject con InteractionPromptUI.cs)
│   └── PromptText (TMP_Text, ej. "[E] Interactuar", oculto por defecto)
├── DialoguePanel (GameObject con DialoguePanelUI.cs, sprite de fondo = panel_dialogo.png, oculto por defecto)
│   ├── NPCNameText (TMP_Text)
│   └── DialogueBodyText (TMP_Text)
└── FadeImage (Image negra full-screen, CanvasGroup con alpha inicial = 1, usada por ScreenFader.cs)
```

El `FadeImage` tiene que arrancar con `CanvasGroup.alpha = 1` (pantalla en negro) — `ScreenFader.Start()` la desvanece de 1 a 0. Si arranca en 0 no hay nada que desvanecer y la escena se ve "rota" un instante al cargar.

Esta prefab se instancia igual en las 4 escenas.

### 3. Scripts C# a crear

Creá exactamente estos archivos con esta interfaz pública (la implementación interna queda a tu criterio, pero los nombres de campos serializados deben respetarse porque el script de Editor los va a asignar). **Para asignarlos desde `InksideSceneBuilder.cs`, usá `SerializedObject`/`SerializedProperty` + `ApplyModifiedProperties()` — no reflection cruda ni asignación directa del campo privado.** Es la forma correcta de que Unity registre el cambio como una modificación real de la instancia de prefab; con reflection puede que el valor "se vea" seteado en memoria pero no quede bien serializado al guardar la escena.

**`Assets/Scripts/Core/IInteractable.cs`**
```csharp
public interface IInteractable
{
    string GetInteractionPrompt();
    void Interact();
}
```

**`Assets/Scripts/Interaccion/RaycastInteractor.cs`** (se agrega a la Main Camera del jugador)
- `[SerializeField] float interactionRange = 3f`
- `[SerializeField] KeyCode interactKey = KeyCode.E`
- `[SerializeField] InteractionPromptUI promptUI`
- En `Update()`: `Physics.Raycast` desde el centro de la cámara.
  - Si el rayo golpea un collider con `IInteractable`: mostrar `promptUI.Show(GetInteractionPrompt())`, y si se presiona `interactKey` ese mismo frame, llamar `Interact()`.
  - **Si el rayo no golpea nada, o golpea algo sin `IInteractable`: llamar `promptUI.Hide()` de inmediato.** Sin esto el cartel `[E] Interactuar` queda pegado en pantalla para siempre después de la primera vez que se mira un objeto interactuable.

**`Assets/Scripts/Interaccion/SceneTeleport.cs`** (en los 3 libros de la biblioteca) — implementa `IInteractable`
- `[SerializeField] string targetSceneName`
- `[SerializeField] AudioClip pageTurnSfx`
- `[SerializeField] ScreenFader fader`
- `Interact()`: reproducir `pageTurnSfx` (vía `AudioSource.PlayClipAtPoint` o un `AudioSource` propio), llamar `fader.FadeOutThenLoad(targetSceneName)`.

**`Assets/Scripts/Interaccion/ObjectTargetReturn.cs`** (en Anillo / Rosa / Snitch) — implementa `IInteractable`
- `[SerializeField] AudioClip collectSfx`
- `[SerializeField] GameObject visualToHide`
- `[SerializeField] ScreenFader fader`
- `[SerializeField] string hubSceneName = "00_Biblioteca"`
- `Interact()`: reproducir `collectSfx`, `visualToHide.SetActive(false)`, `fader.FadeOutThenLoad(hubSceneName)`.

**`Assets/Scripts/Interaccion/DialogueTrigger.cs`** (en cada NPC; requiere un `BoxCollider` con `isTrigger = true` en el mismo GameObject o un hijo) — **no** implementa `IInteractable`, se dispara solo por proximidad
- `[SerializeField] string npcName`
- `[SerializeField, TextArea] string dialogueLine`
- `[SerializeField] DialoguePanelUI dialoguePanel`
- `OnTriggerEnter(Collider other)`: si `other.CompareTag("Player")`, `dialoguePanel.Show(npcName, dialogueLine)`.
- `OnTriggerExit(Collider other)`: si `other.CompareTag("Player")`, `dialoguePanel.Hide()`.

**`Assets/Scripts/UI/ScreenFader.cs`** (en el `FadeImage` de cada Canvas)
- `[SerializeField] CanvasGroup fadeGroup`
- `[SerializeField] float fadeDuration = 1f`
- `Start()`: corrutina que baja el alpha de `fadeGroup` de 1 a 0 (fade-in al entrar a la escena).
- `public void FadeOutThenLoad(string sceneName)`: corrutina que sube el alpha de 0 a 1 y después llama `SceneManager.LoadScene(sceneName)`.

**`Assets/Scripts/UI/InteractionPromptUI.cs`**
- `public void Show(string text)` / `public void Hide()` — activa/desactiva el GameObject y setea el texto.

**`Assets/Scripts/UI/DialoguePanelUI.cs`**
- `public void Show(string npcName, string line)` / `public void Hide()`.

No agregues sistemas que no estén pedidos acá (sin AudioManager global, sin sistema de guardado, sin más scripts de los listados) — el alcance está definido a propósito así de acotado.

### 4. Script de Editor: construcción automática de las 4 escenas

Creá `Assets/Scripts/Editor/InksideSceneBuilder.cs` con un método estático marcado `[MenuItem("Inkside/Build All Scenes")]` **y** invocable también vía `-executeMethod Inkside.EditorTools.InksideSceneBuilder.BuildAllScenes` (o el namespace/método que definas — anotalo en tu respuesta final para que se pueda invocar por CLI).

**Antes de tocar ninguna escena, una sola vez al arrancar el método:** asegurate de que los recursos esenciales de TextMeshPro estén importados. En un proyecto recién creado (como este) TMP todavía no los tiene, y sin ellos los `TMP_Text` que arma el paso 5 se ven rosa/magenta o tiran `NullReferenceException` por no tener fuente por defecto. Intentá:
```csharp
#if UNITY_EDITOR
TMPro.EditorUtilities.TMP_PackageUtilities.ImportProjectResourcesMenu();
#endif
```
Si ese método no existe en la versión de TMP que resolvió el Package Manager (la API interna cambió entre versiones), buscá el equivalente actual o, como fallback, verificá si ya existe `Resources.Load<TMPro.TMP_Settings>("TMP Settings")` (no nulo = ya están importados) y si no, dejalo documentado como paso manual pendiente (`Window > TextMeshPro > Import TMP Essential Resources`, un solo click, una sola vez).

Este método debe, para cada una de las 4 escenas:

1. Crear una nueva escena **vacía de verdad**: `EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)` — **no** uses el overload por defecto, porque crea una Main Camera y una Directional Light propias de Unity que van a duplicar/chocar con las nuestras (2 `AudioListener` activos, luces de más).
2. Armar la jerarquía estándar:
   ```
   Player          (instancia de Assets/_Comunes/Jugador/PlayerInkside.prefab)
   Environment     (instancia(s) del/los modelo(s) del mundo, ver manifiesto)
   Interactables   (libros / NPC / objetivo, con sus scripts configurados)
   Lighting        (al menos una luz direccional o puntual + un Global Volume con Bloom/Color Adjustments sutil)
   UI Canvas       (instancia de Assets/_Comunes/UI/Prefabs/Canvas_UI.prefab)
   ```
3. Cargar cada modelo con `AssetDatabase.LoadAssetAtPath<GameObject>("<ruta exacta del manifiesto>")` e instanciarlo dentro de `Environment` o `Interactables` según corresponda.
4. **Colliders — paso obligatorio, no lo saltees:** ningún modelo `.glb` importado trae collider (ni glTFast ni ningún importador de Unity los agrega solo). Sin esto el raycast de interacción no detecta nada y el jugador atraviesa las paredes.
   - **Importante:** un `.glb` importado casi nunca tiene la geometría en el GameObject raíz — glTFast suele traer un nodo padre vacío con la mesh real uno o más niveles abajo, en los hijos. Si le agregás el `MeshCollider` a la raíz sin `MeshFilter` propio, el collider queda vacío y no colisiona con nada.
   - En cada modelo de `Environment` (interior del mundo): recorré `GetComponentsInChildren<MeshFilter>()` y agregale un `MeshCollider` (`convex = false`, es geometría estática) a **cada** GameObject que tenga una mesh real — no asumas que hay uno solo ni que está en la raíz. Salteá los que ya tengan collider.
   - En cada libro/anillo/rosa/snitch de `Interactables`: agregale un `BoxCollider` (no trigger) en la raíz del objeto, dimensionado combinando **todos** los `Renderer` de sus hijos (`GetComponentsInChildren<Renderer>()`, acumulando con `Bounds.Encapsulate`) — no uses un solo `Renderer` suelto, puede haber varias sub-mallas (ej. tapa + páginas de un libro) y dejarte afuera parte del modelo. No hace falta que sea pixel-perfect, tiene que ser suficiente para que el raycast le pegue.
   - En cada NPC: el `BoxCollider` con `isTrigger = true` que ya requiere `DialogueTrigger` (sección 3) — dimensionalo con el mismo criterio de bounds combinados, un poco más grande que el modelo para que el trigger de proximidad se sienta natural.
5. Para cada libro/NPC/objetivo, agregar el script correspondiente (`SceneTeleport`, `DialogueTrigger`, `ObjectTargetReturn`) y **asignar los valores de campo exactos** que están en la tabla "Configuración por objeto" más abajo (nombre de escena destino, clip de audio vía `AssetDatabase.LoadAssetAtPath<AudioClip>`, texto de diálogo) **más las referencias cruzadas al Canvas de esta misma escena**: `RaycastInteractor.promptUI`, cada `DialogueTrigger.dialoguePanel`, y el `fader` de **cada** `SceneTeleport` y `ObjectTargetReturn` (los cuatro apuntan a los componentes correspondientes de la instancia de `Canvas_UI.prefab` que acabás de crear en esta escena — son 4 asignaciones separadas, no una sola).
6. Posicionar los objetos con transforms aproximados y razonables (ej. jugador en el origen mirando hacia el punto focal del mundo, NPC a 2-3 metros, objetivo en un punto distinto y visible). Dejá un comentario `// TODO ajustar en Fase B` en cada posición hardcodeada — no hay forma de saber la escala/pivote real de cada `.glb` sin abrirlo visualmente. La posición Y de spawn del jugador es la que más conviene revisar primero en Fase B: si queda por debajo del piso real del modelo, el `CharacterController` cae indefinidamente y da la falsa impresión de que "no funciona nada".
7. Ninguno de los NPCs ni la Snitch llevan IA de movimiento ni animación de vuelo — son props estáticos (a lo sumo un bob/rotación decorativa simple si querés, pero no es obligatorio). No implementes nada de "perseguir al jugador" ni "escapar" aunque la narrativa del GDD hable de la Snitch "revoloteando" — es solo ambientación.
8. Guardar la escena: `EditorSceneManager.MarkSceneDirty(scene)` y después `EditorSceneManager.SaveScene(scene, "Assets/_Escenas/<NombreEscena>.unity")` — pasále siempre la ruta explícita, no dependas de un guardado implícito.
9. Al final, agregar las 4 escenas a Build Settings (`EditorBuildSettings.scenes`) en este orden: `00_Biblioteca`, `01_LotR`, `02_Principito`, `03_HarryPotter`.
10. Loguear (`Debug.Log`) un resumen de qué se creó y qué quedó pendiente de ajuste manual.

Después de escribirlo, **intentá ejecutarlo vos mismo** por CLI si tenés acceso al binario de Unity. Primero verificá que no haya ninguna instancia del Editor de Unity ya abierta con este proyecto (el batch mode necesita el lock del proyecto libre, si no falla sin un error claro):
```
"<ruta_a_Unity.exe>" -batchmode -quit -projectPath "<ruta_absoluta_de_este_repo>" -executeMethod Inkside.EditorTools.InksideSceneBuilder.BuildAllScenes -logFile build_log.txt
```
Revisá `build_log.txt` después de correrlo para confirmar que no haya errores de compilación ni excepciones. Si no tenés forma de invocar Unity, dejá el script listo y documentá el comando exacto que el usuario tiene que correr (o el nombre del ítem de menú `Inkside > Build All Scenes` que tiene que clickear) para completar la construcción.

### 5. Manifiesto de assets (rutas exactas — usalas literal, no las adivines)

**Modelos:**
```
Assets/_Mundos/00_Biblioteca/Modelos/wooden_table.glb
Assets/_Mundos/00_Biblioteca/Modelos/library.glb
Assets/_Mundos/00_Biblioteca/Modelos/book_lotr.glb
Assets/_Mundos/00_Biblioteca/Modelos/book_harrypotter.glb
Assets/_Mundos/00_Biblioteca/Modelos/book_historico_animado.glb
Assets/_Mundos/01_LotR/Modelos/hobbit_house.glb
Assets/_Mundos/01_LotR/Modelos/gandalf_blanco.glb
Assets/_Mundos/01_LotR/Modelos/one_ring.glb
Assets/_Mundos/02_Principito/Modelos/fox.glb
Assets/_Mundos/02_Principito/Modelos/rose.glb
Assets/_Mundos/02_Principito/Modelos/asteroide_superficie.glb
Assets/_Mundos/02_Principito/Modelos/farol_calle.glb
Assets/_Mundos/03_HarryPotter/Modelos/dumbledores_office.glb
Assets/_Mundos/03_HarryPotter/Modelos/dumbledore.glb
Assets/_Mundos/03_HarryPotter/Modelos/golden_snitch.glb
```

**Audio:**
```
Assets/_Mundos/00_Biblioteca/Audio/Ambiente/rain_interior_loop.mp3
Assets/_Mundos/00_Biblioteca/Audio/SFX/book_page_turn.mp3
Assets/_Mundos/01_LotR/Audio/Ambiente/countryside_birds_ambient.mp3
Assets/_Mundos/01_LotR/Audio/SFX/ring_whisper.mp3
Assets/_Mundos/02_Principito/Audio/Ambiente/space_ambient_pad.mp3
Assets/_Mundos/02_Principito/Audio/SFX/glass_sparkle.mp3
Assets/_Mundos/03_HarryPotter/Audio/Ambiente/magic_mystery_ambient.mp3
Assets/_Mundos/03_HarryPotter/Audio/SFX/fluttering_wings.mp3
Assets/_Mundos/03_HarryPotter/Audio/SFX/magic_grab.mp3
Assets/_Comunes/Audio/UI/ui_interact_click.mp3
```

**UI:**
```
Assets/_Comunes/UI/Texturas/target_cursor.png
Assets/_Comunes/UI/Texturas/panel_dialogo.png
```

No hay texturas ni materiales sueltos más allá de estos dos PNG — el resto del material visual viene embebido dentro de cada `.glb`.

### 6. Configuración por objeto (valores exactos para los campos serializados)

**Escena `00_Biblioteca`** (hub, sin `DialogueTrigger`, sin `ObjectTargetReturn`):
| GameObject | Script | Campos |
|---|---|---|
| Libro LOTR (`book_lotr.glb`) | `SceneTeleport` | `targetSceneName = "01_LotR"`, `pageTurnSfx = book_page_turn.mp3` |
| Libro Harry Potter (`book_harrypotter.glb`) | `SceneTeleport` | `targetSceneName = "03_HarryPotter"`, `pageTurnSfx = book_page_turn.mp3` |
| Libro Principito (`book_historico_animado.glb`) | `SceneTeleport` | `targetSceneName = "02_Principito"`, `pageTurnSfx = book_page_turn.mp3` |

Ambiente: `AudioSource` en loop con `rain_interior_loop.mp3`, spatial blend 2D.

**Escena `01_LotR`:**
| GameObject | Script | Campos |
|---|---|---|
| `gandalf_blanco.glb` | `DialogueTrigger` | `npcName = "Gandalf"`, `dialogueLine = "Llegas a tiempo, viajero. La sombra en el Este se extiende y el destino de la Tierra Media pende de un hilo. Encuentra el Anillo Único antes de que su llamada atraiga al enemigo."` |
| `one_ring.glb` | `ObjectTargetReturn` | `collectSfx = ring_whisper.mp3`, `hubSceneName = "00_Biblioteca"` |

Ambiente: `AudioSource` en loop con `countryside_birds_ambient.mp3`, spatial blend 2D. **No agregar SFX de chimenea — es intencional.**

**Escena `02_Principito`:**
| GameObject | Script | Campos |
|---|---|---|
| `fox.glb` | `DialogueTrigger` | `npcName = "El Zorro"`, `dialogueLine = "Solo con el corazón se puede ver bien; lo esencial es invisible para los ojos. Encuentra la rosa que mi amigo dejó protegida antes de que el frío del espacio la marchite."` |
| `rose.glb` | `ObjectTargetReturn` | `collectSfx = glass_sparkle.mp3`, `hubSceneName = "00_Biblioteca"` |

Ambiente: `AudioSource` en loop con `space_ambient_pad.mp3`, spatial blend 2D. `asteroide_superficie.glb` y `farol_calle.glb` van en `Environment`, sin script. Fondo del cubo: material de estrellas/nebulosa sobre geometría local (ver GDD sección 4.5) — si no hay una textura de estrellas disponible en el repo, usar un material `URP/Lit` oscuro (azul muy oscuro/negro) con `Emission` baja como placeholder de Fase B.

**Escena `03_HarryPotter`:**
| GameObject | Script | Campos |
|---|---|---|
| `dumbledore.glb` | `DialogueTrigger` | `npcName = "Dumbledore"`, `dialogueLine = "Ah, viajero, llegas en buen momento. Un pequeño problema se nos ha escapado de las manos... o mejor dicho, de las alas. Una Snitch Dorada se liberó durante la última práctica de Quidditch y anda revoloteando libre por mi oficina. ¿Serías tan amable de atraparla antes de que rompa algo... otra vez?"` |
| `golden_snitch.glb` | `ObjectTargetReturn` | `collectSfx = magic_grab.mp3`, `hubSceneName = "00_Biblioteca"` |

Ambiente: `AudioSource` en loop con `magic_mystery_ambient.mp3`, spatial blend 2D. Además, un `AudioSource` en el propio `golden_snitch.glb` reproduciendo `fluttering_wings.mp3` en loop con spatial blend 3D (para que suene más fuerte cerca del objetivo).

Las referencias cruzadas al Canvas (`promptUI`, `dialoguePanel`, `fader`) están detalladas en la sección 4, paso 5 — son las mismas en las 4 escenas. El SFX `ui_interact_click.mp3` se reproduce opcionalmente al mostrarse el prompt de interacción (no es obligatorio, es un detalle de pulido).

### 7. Qué NO tocar / restricciones

- No renombres, muevas ni borres ningún archivo dentro de `Assets/_Mundos/` o `Assets/_Comunes/` — ya tienen nombres finales acordados.
- No reorganices la carpeta que genere el importador de Starter Assets.
- No agregues paquetes, managers o sistemas no pedidos en este prompt (nada de analytics, nada de sistemas de guardado, nada de networking).
- No inventes texturas/materiales de piso/pared que no existen en el repo — usá el fallback simple documentado (material de color plano) y dejalo marcado como pendiente de reemplazo.

### 8. Checklist final (verificalo vos mismo antes de terminar)

- [ ] El proyecto abre sin errores de compilación.
- [ ] Los 8 scripts de la sección 3 existen y compilan.
- [ ] `InksideSceneBuilder.cs` existe, tiene `[MenuItem]` y es invocable por `-executeMethod`.
- [ ] Las 4 escenas existen en `Assets/_Escenas/` y están en Build Settings en el orden correcto.
- [ ] Cada libro/NPC/objetivo de la tabla de la sección 6 tiene su script con los campos asignados, **incluyendo `promptUI`/`dialoguePanel`/`fader`** (no solo los campos "de contenido" como el texto o el audio).
- [ ] Cada modelo de `Environment` y cada `Interactable` tiene un Collider (no confiar en que el `.glb` ya lo trae).
- [ ] `target_cursor.png` y `panel_dialogo.png` quedaron con import type "Sprite (2D and UI)".
- [ ] Los recursos esenciales de TextMeshPro están importados (los textos no se ven rosa/magenta).
- [ ] Cada escena tiene un solo `AudioListener` activo (si aparece el warning de Unity de "2 audio listeners", es porque `NewScene` se llamó sin `NewSceneSetup.EmptyScene`).
- [ ] `build_log.txt` (si pudiste correr el build) no tiene excepciones.
- [ ] Documentaste en tu respuesta final: qué se automatizó, qué quedó pendiente de Fase B (ver GDD sección 5), y el comando exacto para correr `InksideSceneBuilder` si el usuario tiene que hacerlo manualmente.

---

## Notas para vos (Manu) antes de mandarlo

- El setup manual (ver [Setup_Unity.md](Setup_Unity.md)) ya terminó: proyecto creado en `C:\Users\manup\TP-CDEV\TP-inkside`, glTFast/Input System/URP instalados, y "First & Third Person Character Controllers" importado. Este prompt ya está actualizado para reflejar eso — el trabajo de Fable arranca directo en la sección 1.
- Las 3 preguntas de la v1 del GDD ya están confirmadas: `book_historico_animado.glb` es el libro de El Principito, `rose.glb` ya incluye la campana de cristal, y las texturas de piso/skybox se resuelven en la Fase B si hacen falta (no bloquean el one-shot).
- Si en algún momento cambia algo de esto, avisame y actualizamos el GDD antes de la siguiente iteración con Fable.
