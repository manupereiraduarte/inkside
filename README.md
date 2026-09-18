# inkside — Biblioteca de Relatos Virtuales

Experiencia interactiva en primera persona desarrollada en Unity para un trabajo práctico universitario. El usuario explora una **biblioteca antigua (hub central)** y viaja, a través de tres libros mágicos, hacia **dioramas cerrados** (estilo *roombox*) ambientados en tres obras literarias. En cada mundo se encuentra con un personaje icónico (NPC) que le encarga una misión breve de búsqueda y recolección, tras la cual regresa automáticamente a la biblioteca.

| Mundo | Obra | NPC (GDD original) | NPC (asset real) | Objetivo a recolectar |
|---|---|---|---|---|
| 00 | Biblioteca (Hub) | — | — | Elegir un libro para viajar |
| 01 | El Señor de los Anillos | Gandalf | ✅ Gandalf, versión "Mago Blanco" (`gandalf_blanco`) | El Anillo Único |
| 02 | El Principito | El Zorro | ✅ El Zorro | La Rosa bajo campana de cristal |
| 03 | Harry Potter | El Sombrero Seleccionador | ⚠️ Albus Dumbledore | La Snitch Dorada |

> Ver [Cambios respecto al GDD original](#cambios-respecto-al-gdd-original) — no se consiguieron todos los assets planificados y hubo alguna sustitución de escenario/NPC.

## Especificaciones técnicas

- **Motor:** Unity (versión LTS), plantilla 3D Core o URP.
- **Control del jugador:** paquete oficial *Starter Assets — First Person Character Controller* (movimiento, cámara y colisiones). Se importa desde el Package Manager / Asset Store; Unity crea su propia carpeta raíz (p. ej. `Assets/StarterAssets/`) que **no debe reorganizarse manualmente** para poder actualizarla sin conflictos.
- **Plataforma objetivo:** Build de escritorio (.exe) o WebGL, según lo defina la cátedra.
- **Fuentes de assets:** Sketchfab / Meshy (modelos `.glb`), Pixabay Audio / Freesound.org (sonido `.mp3`).

## Convenciones de nombres para assets

Todos los archivos de assets (modelos, texturas, audio, materiales) se nombran en **minúsculas**, sin espacios ni caracteres especiales, usando `_` como separador. Las carpetas del proyecto mantienen mayúscula inicial en español para mayor legibilidad en el Project window.

Los nombres originales de descarga (slugs largos de Sketchfab/Pixabay/Freesound, con espacios o guiones) se renombraron al copiarlos al repo para cumplir esta convención — ver la tabla de cada mundo más abajo para el mapeo nombre original → nombre en el repo.

| Correcto | Incorrecto |
|---|---|
| `one_ring.glb` | `One Ring (1).glb` |
| `ring_whisper.mp3` | `dragon-studio-creepy-whisper-472369.mp3` |
| `wood_floor_tileable.png` | `Suelo Madera.png` |

## Estructura del proyecto

La organización es **por mundo/feature** en lugar de por tipo de archivo: cada diorama es una unidad autocontenida con sus propios modelos, texturas, materiales, prefabs y audio. Esto evita mezclar assets de escenas distintas, facilita que cada integrante del equipo trabaje en "su" mundo sin pisar carpetas ajenas, y deja el proyecto listo para paquetizar por escena (Addressables/AssetBundles) si hiciera falta más adelante. Lo que sí se comparte entre todos los mundos (jugador, UI, sonidos de interfaz) vive aparte, en `_Comunes/`.

```
Assets/
├── _Escenas/                          # Las 4 escenas .unity (Build Settings → Scenes in Build)
│                                       #   00_Biblioteca, 01_LotR, 02_Principito, 03_HarryPotter
│
├── _Mundos/
│   ├── 00_Biblioteca/                  # HUB — mesa, estantería, los 3 libros portal
│   │   ├── Modelos/                    # wooden_table, library, book_lotr, book_harrypotter, book_historico_animado
│   │   ├── Texturas/                   # (pendiente)
│   │   ├── Materiales/                 # (pendiente)
│   │   ├── Prefabs/                    # (pendiente) libro_lotr / libro_principito / libro_harrypotter con SceneTeleport
│   │   └── Audio/
│   │       ├── Ambiente/               # rain_interior_loop
│   │       └── SFX/                    # book_page_turn
│   │
│   ├── 01_LotR/                        # El Señor de los Anillos — interior de Bolsón Cerrado
│   │   ├── Modelos/                    # hobbit_house, gandalf_blanco (NPC), one_ring
│   │   ├── Texturas/                   # (pendiente)
│   │   ├── Materiales/                 # (pendiente)
│   │   ├── Prefabs/                    # (pendiente)
│   │   └── Audio/
│   │       ├── Ambiente/               # countryside_birds_ambient (sin fireplace_loop: el modelo no tiene chimenea)
│   │       └── SFX/                    # ring_whisper
│   │
│   ├── 02_Principito/                  # Asteroide B-612
│   │   ├── Modelos/                    # fox, rose, asteroide_superficie, farol_calle
│   │   ├── Texturas/                   # (pendiente) skybox espacial / suelo del asteroide
│   │   ├── Materiales/                 # (pendiente)
│   │   ├── Prefabs/                    # (pendiente)
│   │   └── Audio/
│   │       ├── Ambiente/               # space_ambient_pad
│   │       └── SFX/                    # glass_sparkle
│   │
│   └── 03_HarryPotter/                 # ahora "Oficina de Dumbledore" (ver cambios) en vez de mazmorras
│       ├── Modelos/                    # dumbledores_office, dumbledore (NPC sustituto), golden_snitch
│       ├── Texturas/                   # (pendiente)
│       ├── Materiales/                 # (pendiente)
│       ├── Prefabs/                    # (pendiente)
│       └── Audio/
│           ├── Ambiente/               # magic_mystery_ambient
│           └── SFX/                    # fluttering_wings, magic_grab
│
├── _Comunes/
│   ├── Jugador/                         # (pendiente) prefab/variante propia sobre el First Person Controller
│   ├── Modelos/                         # (pendiente) props genéricos reutilizables entre dioramas
│   ├── Materiales/                      # (pendiente)
│   ├── UI/
│   │   ├── Texturas/                    # panel_dialogo, target_cursor
│   │   └── Prefabs/                     # (pendiente) Canvas, panel de diálogo, prompt "[E] Interactuar"
│   └── Audio/
│       └── UI/                          # ui_interact_click
│
└── Scripts/                             # Organizado por responsabilidad (la lógica se reutiliza en los 4 mundos)
    ├── Core/                            # IInteractable (interfaz), lógica central/estado del juego
    ├── Interaccion/                     # RaycastInteractor, SceneTeleport, ObjectTargetReturn, DialogueTrigger
    ├── UI/                              # Controladores del panel de diálogo y del prompt de interacción
    └── Editor/                          # Herramientas y extensiones del editor de Unity
```

### Arquitectura de interacción (referencia, sin implementación)

```
JUGADOR (Character Controller)
  └── Main Camera + Audio Listener
      └── RaycastInteractor.cs — detecta objetos con tag "Interactable" (rango ~2.5-3 m)
            ├── Libro / objetivo → muestra prompt UI "[E] Interactuar" → SceneTeleport.cs / ObjectTargetReturn.cs
            └── NPC → activa panel de diálogo → DialogueTrigger.cs
```

- **`RaycastInteractor.cs`** — en la cámara del jugador; detecta objetos `IInteractable` y escucha la tecla `[E]`.
- **`SceneTeleport.cs`** — en los 3 libros de la biblioteca; carga la escena del mundo correspondiente.
- **`ObjectTargetReturn.cs`** — en el Anillo, la Rosa y la Snitch; reproduce el SFX de recolección y vuelve a `00_Biblioteca`.
- **`DialogueTrigger.cs`** — en el NPC de cada mundo; muestra su diálogo al entrar en el trigger.

### Guía rápida: ¿dónde coloco mi archivo?

| Tipo de archivo | Carpeta destino |
|---|---|
| Escena nueva (`.unity`) | `Assets/_Escenas/` |
| Modelo/textura/audio específico de un mundo | `Assets/_Mundos/<0X_Mundo>/<Modelos\|Texturas\|Audio>/` |
| Prefab específico de un mundo (NPC, objeto objetivo, libro) | `Assets/_Mundos/<0X_Mundo>/Prefabs/` |
| Prefab/textura/audio de UI compartido (cursor, panel diálogo, click) | `Assets/_Comunes/UI/` |
| Ajustes propios sobre el First Person Controller | `Assets/_Comunes/Jugador/` |
| Prop o material genérico reutilizable entre mundos | `Assets/_Comunes/Modelos/` o `Assets/_Comunes/Materiales/` |
| Script de interacción (raycast, teletransporte, diálogo, recolección) | `Assets/Scripts/Interaccion/` |
| Script de lógica central / interfaces | `Assets/Scripts/Core/` |
| Script de UI (paneles, prompts) | `Assets/Scripts/UI/` |
| Herramienta del editor de Unity | `Assets/Scripts/Editor/` |

## Cambios respecto al GDD original

Al bajar los assets no se consiguió todo lo que planteaba el documento de diseño original. Esto es lo que cambió y lo que falta:

### ⚠️ Harry Potter — cambio de escenario y NPC
Se descargó `dumbledores_office.glb` (oficina de Dumbledore) y `harry-potter_albus_dumbledore.glb` en vez de una mazmorra de pociones con el Sombrero Seleccionador. El diorama pasa a ambientarse en la **oficina de Dumbledore**, y el NPC que da la misión de la Snitch es **Dumbledore** en vez del Sombrero. Falta decidir/ajustar el diálogo de `DialogueTrigger` para este NPC. También faltan: caldero, frascos de poción y la textura `dungeon_stone_wall` (ya no aplicarían si se confirma el cambio de escenario). El SFX `magic_grab` ya está resuelto (`magic_grab.mp3`, un "magic twinkle" al atrapar la Snitch).

### ✅ El Señor de los Anillos — resuelto
`gandalf_blanco.glb` **es Gandalf** (confirmado): no había modelo 3D disponible del Gandalf gris "estándar", así que se usó su versión de Gandalf el Blanco. El interior de Bolsón Cerrado (`hobbit_house.glb`) está en el repo con una versión liviana (39 MB). No se va a usar el SFX `fireplace_loop` del GDD original porque este modelo de interior **no tiene chimenea** — decisión consciente, no un faltante; el ambiente sonoro del mundo queda cubierto por `countryside_birds_ambient`.

### ✅ El Principito — resuelto reutilizando assets
El Zorro y la Rosa están. No se consiguió un modelo de "superficie de asteroide rocoso", así que se reutiliza `asteroide_superficie.glb` (originalmente un modelo de luna) como terreno del diorama por su parecido visual — es una decisión de producción, no un placeholder temporal. El farol de calle antiguo también se agregó (`farol_calle.glb`). Sigue pendiente confirmar si hacen falta texturas separadas de skybox espacial y suelo del asteroide, o si el material ya viene embebido en los `.glb`.

### ✅ Biblioteca (Hub) — resuelto con una variante
El GDD pedía "un trío de libros antiguos" genérico; en la práctica se armó con 3 modelos distintos: uno de temática LOTR (`book_lotr.glb`), uno de Harry Potter (`book_harrypotter.glb` — venía descargado en la carpeta de Harry Potter pero es el libro físico de la biblioteca, no un asset del diorama) y un libro genérico animado (`book_historico_animado.glb`) que por descarte cubriría el rol del libro de **El Principito** (no se encontró un libro específico con temática de estrellas). Confirmar si ese es el uso previsto. El SFX `book_page_turn` ya está resuelto; falta la textura de piso/pared de la biblioteca.

### ✅ UI común — completo
`panel_dialogo.png`, `target_cursor.png` y el SFX `ui_interact_click` ya están en `_Comunes/`.

## Documentación

- [Documentacion/GDD.md](Documentacion/GDD.md) — documento de diseño completo y actualizado (narrativa, catálogo real de assets, arquitectura técnica).
- [Documentacion/Setup_Unity.md](Documentacion/Setup_Unity.md) — guía paso a paso para instalar Unity y crear el proyecto sobre este repo sin perder la carpeta `Assets/` ya armada.
- [Documentacion/Prompt_OneShot_Fable.md](Documentacion/Prompt_OneShot_Fable.md) — prompt de construcción para levantar el proyecto Unity de una pasada con un agente de IA.

## Notas

- No subir carpetas generadas automáticamente por Unity (`Library/`, `Temp/`, `Logs/`, `obj/`, `Build/`, `Builds/`, `UserSettings/`) ni configuraciones de IDE (`.vs/`, `.idea/`). Ver [.gitignore](.gitignore).
- Cada carpeta vacía contiene un archivo `.gitkeep` para que Git la registre; al agregar el primer asset real a una carpeta, el `.gitkeep` puede eliminarse.
- Las 4 escenas deben agregarse a **Build Settings → Scenes in Build** en el orden 00 → 01 → 02 → 03 para que `SceneManager.LoadScene` funcione correctamente.
- Los modelos `.glb` pesados (`gandalf_blanco.glb` a 44 MB, `hobbit_house.glb` a 39 MB) están al límite de lo razonable para un solo asset en tiempo real; si el proyecto sigue sumando modelos de ese tamaño, conviene reconsiderar Git LFS más adelante.
