# DOCUMENTO DE DISEÑO DE PROYECTO (GDD): INKSIDE — BIBLIOTECA DE RELATOS VIRTUALES

> Versión 2 — corregida y ampliada a partir del GDD original ("TP creatividad.md") una vez descargados y revisados los assets reales. Los cambios respecto a la v1 están marcados con 🔄, las decisiones técnicas nuevas con 🛠️, y los puntos que siguen sin resolver con ❓.

## 1. Resumen Ejecutivo y Elección de Herramientas

### Propuesta General

Experiencia interactiva en primera persona en la que el usuario explora una biblioteca antigua y viaja a través de tres libros interactivos hacia mundos contenidos dentro de dioramas cerrados (estilo *roombox*/cubos delimitados). Cada mundo propone un encuentro con un personaje icónico (NPC) y una misión directa de búsqueda y recolección que transporta de regreso al usuario a la biblioteca.

### Selección de Tecnologías

- **Motor Gráfico — Unity 6 LTS + Universal Render Pipeline (URP)** 🔄
  Se define versión y pipeline concretos (el GDD original dejaba abierto "LTS" y "3D Core o URP"). Unity 6 LTS tiene soporte extendido, mejor rendimiento en URP y facilita el post-procesado (bloom, viñeta) que se va a usar para reforzar el clima de cada diorama.
- **Control del Jugador: "First & Third Person Character Controllers"** (Asset Store, compatible URP) 🔄
  El paquete "Starter Assets - First Person Character Controller" original ya no existe por separado en la Asset Store — Unity lo unificó en este paquete nuevo. Usa el Input System nuevo (no el Input Manager clásico) y **Cinemachine** (`com.unity.cinemachine`, se instaló solo al importar el paquete). Ya está importado en `Assets/Starter Assets/` (con espacio en el nombre). El sistema de jugador **no es un solo prefab**, son 4 objetos hermanos que trabajan juntos (ver [sección 4.1](#41-paquetes--estado-real) para el detalle exacto de cada uno).
- **Formato de modelos 3D: `.glb` (glTF binario)** 🔄
  Todos los assets descargados vienen en `.glb`, no `.fbx` como sugería el GDD original. Unity no importa `.glb` de forma nativa: hace falta instalar el paquete **`com.unity.cloud.gltfast`** (Unity glTFast) desde el Package Manager. Sin este paquete, ningún modelo del proyecto se puede usar. Ver [sección 4.1](#41-paquetes-a-instalar).
- **Plataforma objetivo: Build de escritorio (Windows, .exe) únicamente** 🔄
  Confirmado: se entrega corriendo en local, se sube a git, y probablemente también como `.zip` para que otra persona lo ejecute en su PC. No hace falta optimizar para WebGL — esto da margen para no comprimir agresivamente los modelos actuales (30-45 MB cada uno es aceptable en escritorio).
- **Fuentes de Modelado y Texturas:** Sketchfab (`.glb`) y Meshy (generación puntual por IA).
- **Fuentes de Sonido:** Pixabay Audio y Freesound.org (`.mp3`).

## 2. Narrativa y Guion Detallado de la Experiencia

### Acto 0: La Biblioteca Central (El Hub)

Sin cambios respecto al original: el usuario aparece en una sala de estilo victoriano con una mesa central y tres libros. Sonido ambiental: lluvia + crujidos + reloj (actualmente solo se consiguió la lluvia, `rain_interior_loop.mp3`; el crujido de madera y el tic-tac del reloj no se descargaron — no bloquean el desarrollo, se pueden omitir o buscar más adelante).

Acción: apuntar a un libro + `[E]` → `book_page_turn.mp3` → fundido a negro → carga la escena del mundo.

### Acto 1: El Eco de la Tierra Media (*El Señor de los Anillos*) 🔄

**Escenario:** interior de Bolsón Cerrado (`hobbit_house.glb`). El modelo conseguido **no incluye una chimenea** como mueble independiente, así que se ajusta la puesta en escena:

> *"Junto a la ventana redonda que se asoma a los prados soleados de La Comarca se encuentra Gandalf, apoyado en su vara, contemplando el paisaje."*

**Sonido ambiental:** viento suave y ambiente de campo (`countryside_birds_ambient.mp3`, pájaros en vez de la flauta/laúd original — sustitución aceptable, mismo clima "campestre"). Se elimina el crepitar de leña (`fireplace_loop`) de la lista de audio: **no se va a usar**, es una decisión consciente y no un pendiente, porque el modelo de interior no tiene hogar/chimenea.

**Encuentro NPC (Gandalf):** el modelo conseguido es `gandalf_blanco.glb` — es Gandalf, pero en su versión "Gandalf el Blanco" porque no había un modelo 3D disponible del Gandalf gris de esta escena. Se mantiene el diálogo original sin cambios (no hace referencia a su color de túnica):

> *"Llegas a tiempo, viajero. La sombra en el Este se extiende y el destino de la Tierra Media pende de un hilo. Encuentra el Anillo Único antes de que su llamada atraiga al enemigo."*

**Misión y resolución:** sin cambios. El jugador encuentra el **Anillo Único** (`one_ring.glb`), lo activa con `[E]`, suena `ring_whisper.mp3`, destello dorado, regreso a la biblioteca.

### Acto 2: La Soledad del Asteroide B-612 (*El Principito*) 🔄

**Escenario:** cubo con fondo espacial. No se consiguió un modelo dedicado de "superficie de asteroide rocoso" — se reutiliza `asteroide_superficie.glb` (un modelo de luna) como terreno del diorama por su parecido visual. Es una decisión de producción definitiva, no un placeholder. Se agrega también `farol_calle.glb` (farol de calle antiguo), que sí estaba en el catálogo original.

**Sonido ambiental:** `space_ambient_pad.mp3` (sin cambios).

**Encuentro NPC (El Zorro):** `fox.glb`, sin cambios en el diálogo:

> *"Solo con el corazón se puede ver bien; lo esencial es invisible para los ojos. Encuentra la rosa que mi amigo dejó protegida antes de que el frío del espacio la marchite."*

**Misión y resolución:** el jugador encuentra la **Rosa** (`rose.glb` — confirmado: el modelo ya incluye la campana de cristal). Al interactuar: `glass_sparkle.mp3`, destello blanco, regreso a la biblioteca.

### Acto 3: La Oficina del Director (*Harry Potter*) 🔄🔄

**Cambio de escenario confirmado:** se abandona la ambientación de "Mazmorras de Pociones" del GDD original. El escenario pasa a ser la **Oficina de Dumbledore** (`dumbledores_office.glb`), y el NPC que da la misión es **Dumbledore** (`dumbledore.glb`) en vez del Sombrero Seleccionador. No hacen falta caldero ni frascos de poción — quedan fuera del catálogo de assets.

**Escenario (narrativa reescrita):**

> Oficina circular en lo alto de una torre. Estanterías repletas de objetos mágicos y libros antiguos cubren las paredes, los retratos de antiguos directores susurran entre sí desde sus marcos, y Dumbledore aguarda de pie junto a su escritorio.

**Sonido ambiental:** `magic_mystery_ambient.mp3` (reemplaza el original "goteras y viento frío de mazmorra").

**Encuentro NPC (Dumbledore) — diálogo nuevo:**

> *"Ah, viajero, llegas en buen momento. Un pequeño problema se nos ha escapado de las manos... o mejor dicho, de las alas. Una Snitch Dorada se liberó durante la última práctica de Quidditch y anda revoloteando libre por mi oficina. ¿Serías tan amable de atraparla antes de que rompa algo... otra vez?"*

**Misión:** el jugador explora la oficina guiado por un zumbido metálico constante (`fluttering_wings.mp3`, en bucle, idealmente como sonido 3D posicional que crece de volumen al acercarse al objetivo).

**Resolución (narrativa reescrita):**

> En un rincón alto, entre libros y objetos mágicos, flota la **Snitch Dorada** (`golden_snitch.glb`) batiendo sus alas con destellos dorados. Al acercarse y presionar `[E]`, suena un chasquido mágico (`magic_grab.mp3`), chispas llenan la pantalla y el jugador vuelve a la biblioteca.

## 3. Catálogo Real de Recursos (estado actual del repositorio)

Reemplaza al catálogo "mínimo viable" del GDD original por el inventario real de lo que ya está en `Assets/_Mundos/` y `Assets/_Comunes/`. ✅ = lo que ya está en el repo, ❌ = lo que falta.

### MUNDO 00 — Biblioteca (`Assets/_Mundos/00_Biblioteca/`)

| Asset | Archivo | Estado |
|---|---|---|
| Mesa central | `Modelos/wooden_table.glb` | ✅ |
| Estantería / sala | `Modelos/library.glb` | ✅ |
| Libro portal — LOTR | `Modelos/book_lotr.glb` | ✅ |
| Libro portal — Harry Potter | `Modelos/book_harrypotter.glb` | ✅ |
| Libro portal — El Principito | `Modelos/book_historico_animado.glb` | ✅ confirmado (genérico, sin temática de estrellas, pero es el que se usa) |
| Ambiente: lluvia | `Audio/Ambiente/rain_interior_loop.mp3` | ✅ |
| SFX: pasar página | `Audio/SFX/book_page_turn.mp3` | ✅ |
| Textura piso/pared | `Texturas/` | ❌ no conseguida — usar color/material simple como fallback |
| Crujido de madera + tic-tac de reloj | — | ❌ no conseguidos, prescindibles |

### MUNDO 01 — El Señor de los Anillos (`Assets/_Mundos/01_LotR/`)

| Asset | Archivo | Estado |
|---|---|---|
| Interior de Bolsón Cerrado | `Modelos/hobbit_house.glb` | ✅ (39 MB, versión liviana) |
| NPC: Gandalf (versión Blanco) | `Modelos/gandalf_blanco.glb` | ✅ |
| Objetivo: Anillo Único | `Modelos/one_ring.glb` | ✅ |
| SFX: susurro del anillo | `Audio/SFX/ring_whisper.mp3` | ✅ |
| Ambiente: pájaros/campo | `Audio/Ambiente/countryside_birds_ambient.mp3` | ✅ |
| Crepitar de chimenea | — | 🚫 descartado a propósito (el modelo no tiene chimenea) |

### MUNDO 02 — El Principito (`Assets/_Mundos/02_Principito/`)

| Asset | Archivo | Estado |
|---|---|---|
| NPC: El Zorro | `Modelos/fox.glb` | ✅ |
| Objetivo: la Rosa | `Modelos/rose.glb` | ✅ incluye la campana de cristal |
| Terreno: superficie de asteroide | `Modelos/asteroide_superficie.glb` | ✅ (reutilizado de un modelo de luna) |
| Farol de calle antiguo | `Modelos/farol_calle.glb` | ✅ |
| SFX: campanillas de cristal | `Audio/SFX/glass_sparkle.mp3` | ✅ |
| Ambiente: sintetizador espacial | `Audio/Ambiente/space_ambient_pad.mp3` | ✅ |
| Skybox espacial / textura de suelo | `Texturas/` | ❌ a confirmar si el material ya viene embebido en los `.glb` |

### MUNDO 03 — Harry Potter / Oficina de Dumbledore (`Assets/_Mundos/03_HarryPotter/`)

| Asset | Archivo | Estado |
|---|---|---|
| Escenario: oficina de Dumbledore | `Modelos/dumbledores_office.glb` | ✅ |
| NPC: Dumbledore | `Modelos/dumbledore.glb` | ✅ |
| Objetivo: Snitch Dorada | `Modelos/golden_snitch.glb` | ✅ |
| SFX: aleteo continuo | `Audio/SFX/fluttering_wings.mp3` | ✅ |
| SFX: atrapar la Snitch | `Audio/SFX/magic_grab.mp3` | ✅ |
| Ambiente: música mágica | `Audio/Ambiente/magic_mystery_ambient.mp3` | ✅ |
| Caldero / frascos de poción | — | 🚫 descartados (pivot a oficina, ya no aplican) |

### Recursos comunes (`Assets/_Comunes/`)

| Asset | Archivo | Estado |
|---|---|---|
| Textura del cursor/retícula | `UI/Texturas/target_cursor.png` | ✅ |
| Textura del panel de diálogo | `UI/Texturas/panel_dialogo.png` | ✅ |
| SFX de click de interfaz | `Audio/UI/ui_interact_click.mp3` | ✅ |
| Prefab del jugador (variante propia) | `Jugador/` | ❌ pendiente — se resuelve en el one-shot |
| Prefab de Canvas de UI | `UI/Prefabs/` | ❌ pendiente — se resuelve en el one-shot |

## 4. Especificaciones Técnicas y Arquitectura 🛠️ (sección ampliada)

Esta sección reemplaza y expande la arquitectura del GDD original con el nivel de detalle necesario para que un agente de IA con acceso a archivos/shell (sin acceso a la interfaz gráfica del Editor de Unity) pueda construir el proyecto de una sola pasada. Ver [Documentacion/Prompt_OneShot_Fable.md](Prompt_OneShot_Fable.md) para el prompt de construcción en sí.

### 4.1 Paquetes — estado real ✅

Ya instalados en el proyecto (`C:\Users\manup\TP-CDEV\TP-inkside`):

- `com.unity.cloud.gltfast` — importación de `.glb`/`.gltf` como assets nativos (obligatorio, sin esto no se puede usar ningún modelo). ✅ instalado, confirmado que los `.glb` muestran preview 3D.
- `com.unity.inputsystem` — venía instalado por defecto con el template 3D (URP) de Unity 6. ✅
- `com.unity.render-pipelines.universal` — URP, viene por defecto con el template. ✅
- `com.unity.cinemachine` (6.6.0) — se instaló solo al importar el paquete de personaje, porque `PlayerFollowCamera` lo requiere. ✅
- TextMeshPro (UI de texto para el prompt de interacción y el panel de diálogo) — no requiere instalación aparte en Unity 6, se activa solo la primera vez que se usa un componente TMP ("Import TMP Essentials").
- "First & Third Person Character Controllers" (reemplaza a "Starter Assets - First Person Character Controller", que ya no existe como paquete separado) — ✅ importado vía Asset Store en `Assets/Starter Assets/` (con espacio).

**El sistema de jugador son 4 objetos, no un solo prefab** (confirmado leyendo directamente el YAML de `Assets/Starter Assets/Sample/FirstPersonController/Playground.unity` y de `NestedParent_Unpack.prefab`, que es como el propio asset arma la demo):

| Objeto | Prefab de origen | Rol |
|---|---|---|
| `MainCamera` | `Runtime/FirstPersonController/Prefabs/MainCamera.prefab` | Cámara real que renderiza, con `CinemachineBrain`. **Acá va `RaycastInteractor.cs`** — no es hijo del jugador, es un objeto hermano. |
| `PlayerFollowCamera` | `Runtime/FirstPersonController/Prefabs/PlayerFollowCamera.prefab` | Cinemachine Virtual Camera. Su campo `Follow` ya viene apuntando (dentro del prefab) al `PlayerCameraRoot` de `PlayerCapsule` — no hace falta recablear nada. |
| `PlayerCapsule` | `Runtime/FirstPersonController/Prefabs/PlayerCapsule.prefab` | `CharacterController` + `FirstPersonController.cs`. **Acá va el tag `"Player"`** — es el único de los 4 con collider físico, el que realmente entra en los triggers de los NPC. |
| `UI_EventSystem` | `Runtime/Mobile/Prefabs/EventSystem/UI_EventSystem.prefab` | EventSystem estándar, necesario para que la UI/Canvas funcione. |

Para no instanciar 4 prefabs sueltos en cada una de las 4 escenas, conviene agruparlos una sola vez bajo un GameObject vacío y guardar eso como `Assets/_Comunes/Jugador/PlayerInkside.prefab` (mismo truco que usa el propio asset en `NestedParent_Unpack.prefab` — Unity soporta prefabs anidados así). Si el anidado da problemas en el Editor, la alternativa es "Unpack Completely" sobre la instancia en cada escena, tal como sugiere el nombre original (`_Unpack`).

### 4.2 Detección de interacción: sin tags ni layers extra 🔄

El GDD original proponía un tag `"Interactable"`. Se simplifica: el `RaycastInteractor` detecta el componente `IInteractable` directamente vía `GetComponent<IInteractable>()` sobre lo que golpea el rayo — evita tener que taguear manualmente cada objeto y es igual de robusto. El tag `"Player"` va en `PlayerCapsule` (ver tabla de arriba), no en la cámara.

### 4.3 Contrato de scripts (qué campos y métodos debe tener cada uno)

```
IInteractable (interfaz)
  string GetInteractionPrompt()   // texto a mostrar, ej. "[E] Tomar el Anillo"
  void Interact()                  // acción al presionar [E]

RaycastInteractor : MonoBehaviour        — en Main Camera
  [SerializeField] float interactionRange = 3f
  [SerializeField] KeyCode interactKey = KeyCode.E
  [SerializeField] InteractionPromptUI promptUI
  Update() -> Physics.Raycast; si golpea IInteractable, promptUI.Show(prompt); si se presiona interactKey, Interact()

SceneTeleport : MonoBehaviour, IInteractable     — en los 3 libros
  [SerializeField] string targetSceneName
  [SerializeField] AudioClip pageTurnSfx
  [SerializeField] ScreenFader fader
  Interact() -> reproduce SFX, fader.FadeOutThenLoad(targetSceneName)

ObjectTargetReturn : MonoBehaviour, IInteractable   — en Anillo / Rosa / Snitch
  [SerializeField] AudioClip collectSfx
  [SerializeField] GameObject visualToHide
  [SerializeField] ScreenFader fader
  [SerializeField] string hubSceneName = "00_Biblioteca"
  Interact() -> reproduce SFX, oculta visualToHide, fader.FadeOutThenLoad(hubSceneName)

DialogueTrigger : MonoBehaviour      — en cada NPC, con BoxCollider IsTrigger (NO usa IInteractable/raycast)
  [SerializeField] string npcName
  [SerializeField, TextArea] string dialogueLine
  [SerializeField] DialoguePanelUI dialoguePanel
  OnTriggerEnter(Collider other) -> si other.CompareTag("Player"): dialoguePanel.Show(npcName, dialogueLine)
  OnTriggerExit(Collider other)  -> si other.CompareTag("Player"): dialoguePanel.Hide()

ScreenFader : MonoBehaviour          — en el Canvas de cada escena (no persiste entre escenas, cada escena trae el suyo)
  [SerializeField] CanvasGroup fadeGroup
  [SerializeField] float fadeDuration = 1f
  void Start() -> fade-in automático
  public void FadeOutThenLoad(string sceneName) -> corrutina que sube alpha a 1 y luego SceneManager.LoadScene(sceneName)

InteractionPromptUI : MonoBehaviour  — UI común
  public void Show(string text)
  public void Hide()

DialoguePanelUI : MonoBehaviour      — UI común
  public void Show(string npcName, string line)
  public void Hide()
```

🔄 **Decisión sobre el diálogo de NPCs:** el diagrama del GDD original era ambiguo (mostraba tanto el raycast como el trigger de proximidad para NPCs). Se resuelve así: **el diálogo de NPC se dispara automáticamente al entrar en su radio (BoxCollider trigger), sin necesidad de apretar `[E]`** — es más cinematográfico y evita que el jugador se pierda el diálogo. El raycast + `[E]` queda reservado para libros y objetos recolectables.

### 4.4 Transición entre escenas (fundido a negro)

No existía como script en el GDD original (solo se mencionaba narrativamente "la pantalla funde a negro"). Se resuelve con `ScreenFader` (ver arriba): cada escena tiene su propio Canvas con una `Image` negra a pantalla completa controlada por un `CanvasGroup`. No hace falta un manager persistente (`DontDestroyOnLoad`) — cada escena se auto-desvanece al cargar y se desvanece a negro antes de pedir la siguiente carga.

### 4.5 Construcción de los dioramas ("roombox")

Cada mundo (excepto la Biblioteca, que puede ser una sala más abierta) es un cubo cerrado. Dos casos:

- **Con modelo de interior ya modelado** (`hobbit_house.glb`, `dumbledores_office.glb`, `library.glb`): el cubo ya viene resuelto visualmente dentro del propio modelo importado, pero **ningún importador de Unity (tampoco glTFast) agrega colliders automáticamente** — hay que agregar explícitamente un `MeshCollider` (con `convex = false`, es geometría estática) al mesh del interior después de instanciarlo, o el jugador atraviesa las paredes/el piso directamente.
- **Sin modelo de interior** (El Principito: "cubo con textura de vacío cósmico"): se arma un cubo real en Unity con las caras normales invertidas hacia adentro (o 6 planos formando una caja), con un material de estrellas/nebulosa aplicado a las caras interiores. **Importante:** esto es un material sobre geometría local de la escena, no el Skybox del proyecto (`Window > Rendering > Lighting > Environment`) — el Skybox es para exteriores a cielo abierto, acá es el interior de una caja cerrada.

En todos los casos: agregar colliders invisibles en los límites del cubo para que el jugador no pueda salir del diorama (paso explícito del roadmap original).

### 4.6 Iluminación y post-procesado

- **Iluminación: 100% realtime, sin light-baking.** Para un proyecto de este tamaño, bakear lightmaps agrega tiempo de iteración sin necesidad real de performance — se puede reconsiderar más adelante si hay caída de FPS.
- **Post-procesado:** un `Global Volume` por escena con Bloom + Color Adjustments sutil, ajustado al clima de cada mundo (cálido en Biblioteca/LOTR, frío/violeta en Principito, dorado en la oficina de Dumbledore).

### 4.7 Jerarquía estándar de cada escena

```
[Nombre de la escena]
├── Player                    (instancia de PlayerInkside.prefab: MainCamera + PlayerFollowCamera + PlayerCapsule [tag "Player"] + UI_EventSystem)
├── Environment                (modelo(s) del mundo importados)
├── Interactables               (libros / NPC / objeto objetivo, cada uno con su script)
├── Lighting                    (Directional/Point/Spot Lights + Global Volume)
└── UI Canvas                   (instancia del prefab de Canvas común: retícula, prompt, panel de diálogo, fade)
```

### 4.8 Sin sistema de guardado / progreso

No hace falta persistencia entre sesiones: es un loop simple (Biblioteca → Mundo → Biblioteca) que se reinicia cada vez que se abre el juego. No se implementa `PlayerPrefs` ni ningún sistema de save.

### 4.9 Salida sin completar la misión

No hay una "puerta trasera" para volver a la Biblioteca sin agarrar el objeto objetivo — dado el tamaño reducido de cada diorama, no hace falta; encontrar el objetivo siempre es rápido. Aceptado como simplificación de alcance.

## 5. Hoja de Ruta 🔄 (reorganizada en 2 fases pensando en construcción asistida por IA)

### Fase A — Automatizable de una pasada (agente con acceso a archivos/shell)

1. ✅ Proyecto Unity 6 LTS (URP) creado en `C:\Users\manup\TP-CDEV\TP-inkside`, repo git migrado adentro.
2. ✅ Paquetes instalados: glTFast, Input System, URP (TextMeshPro se activa solo al primer uso).
3. ✅ Importado "First & Third Person Character Controllers" en `Assets/Starter Assets/`.
4. Escribir los scripts de la [sección 4.3](#43-contrato-de-scripts-qué-campos-y-métodos-debe-tener-cada-uno).
5. Escribir un script de Editor (`InksideSceneBuilder.cs`) que arme las 4 escenas por código: instancia modelos, arma la jerarquía estándar, configura y referencia los scripts con los valores correctos (nombres de escena, clips de audio, texto de diálogo), crea el Canvas de UI común, agrega luces básicas y Global Volume, y registra las 4 escenas en Build Settings en orden.
6. Ejecutar la construcción (vía menú del Editor o `-executeMethod` en modo batch) y dejar un log de resultado.

### Fase B — Iteración manual/visual (requiere abrir el Editor y mirar)

1. Ajustar posición/escala/rotación real de cada modelo (desconocida hasta abrir cada `.glb` en el Editor).
2. Afinar iluminación y post-procesado a ojo.
3. Agregar texturas de piso/pared o de skybox espacial si hacen falta (se confirma recién al importar; si faltan, es un ajuste rápido de esta fase, no bloquea nada).
4. Recorrer el loop completo (Biblioteca → Mundo 1 → retorno → Mundo 2 → retorno → Mundo 3 → retorno) y ajustar colisiones invisibles.
5. Build final (.exe).

## 6. Preguntas abiertas / decisiones pendientes

Ninguna por ahora — las 3 dudas de la v1 de este documento (libro-portal de El Principito, campana de cristal de la Rosa, texturas de piso/skybox) ya están resueltas:

- `book_historico_animado.glb` **es** el libro-portal de El Principito (confirmado).
- `rose.glb` **incluye** la campana de cristal (confirmado).
- Las texturas de piso/pared/skybox se evalúan recién al importar; si faltan, se agregan en la Fase B de iteración — no es un bloqueante.

El foco actual está en el **Paso 0 (creación del proyecto Unity)** — ver [Documentacion/Setup_Unity.md](Setup_Unity.md) para la guía paso a paso.
