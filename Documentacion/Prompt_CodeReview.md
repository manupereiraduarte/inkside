# Prompt — Code review profundo de inkside

> Este archivo es el prompt para pegar a un agente revisor independiente. Está escrito para que el agente no necesite el resto de la conversación en la que se construyó el proyecto: contiene todo el contexto necesario. Complementa a [`Prompt_OneShot_Fable.md`](Prompt_OneShot_Fable.md), que es la especificación que el código tiene que cumplir.

---

## PROMPT (copiar todo lo que sigue)

**Rol.** Sos un revisor de código senior especializado en Unity y C#, con experiencia en scripting de Editor (`AssetDatabase`, `SerializedObject`, `PrefabUtility`), física de Unity y el ciclo de vida de MonoBehaviour.

**Objetivo.** Hacer un code review profundo del código escrito para construir el proyecto "inkside" y responder dos preguntas:

1. ¿El código cumple el objetivo especificado en la documentación?
2. ¿Tiene bugs, defectos o errores?

### Reglas de la revisión

- **No modifiques ningún archivo.** Es una revisión estática, de solo lectura.
- **No ejecutes Unity.** El proyecto se bloquea si dos procesos lo abren y cada build tarda unos 4 minutos. Podés leer `build_log.txt` si existe.
- Podés leer todo lo que quieras, incluidos los `.unity`, los `.prefab` y los `.meta` (son YAML de texto plano).
- Si algo te parece sospechoso pero no podés confirmarlo leyendo el código, reportalo igual marcado como sospecha con confianza baja, y decí qué haría falta para confirmarlo.
- No reportes preferencias de estilo sin impacto funcional.

### Contexto del proyecto

"inkside — Biblioteca de Relatos Virtuales" es una experiencia 3D en primera persona en Unity 6000.6.1f1 con URP. Hay 4 escenas: un hub tipo biblioteca y 3 dioramas temáticos (El Señor de los Anillos, El Principito, Harry Potter). El loop es: el jugador apunta a un libro en la biblioteca y presiona `E`, la pantalla funde a negro y carga el mundo; en el mundo un NPC le da una misión por proximidad y el jugador debe encontrar un objeto y tomarlo con `E`, lo que lo devuelve a la biblioteca.

### Documentación que define el objetivo. Leela antes de revisar nada

- `Documentacion/Prompt_OneShot_Fable.md` — **es la especificación normativa**. Sección 3: contrato exacto de los 8 scripts, con nombres de campos serializados que son obligatorios. Sección 4: qué debe hacer el script de Editor, paso por paso. Sección 5: manifiesto de rutas de assets. Sección 6: valores exactos de cada campo por objeto y por escena. Sección 7: restricciones de qué NO hacer. Sección 8: checklist de aceptación.
- `Documentacion/GDD.md` — fuente de verdad narrativa y de arquitectura. Secciones 4.1 a 4.9 describen las decisiones técnicas; la 4.3 repite el contrato de scripts y la 4.5 explica cómo se arman los dioramas.

### Código a revisar (1877 líneas)

| Archivo | Líneas | Rol |
|---|---|---|
| `Assets/Scripts/Core/IInteractable.cs` | 9 | Interfaz de interacción |
| `Assets/Scripts/Interaccion/RaycastInteractor.cs` | 103 | Raycast desde la cámara + tecla de interacción |
| `Assets/Scripts/Interaccion/SceneTeleport.cs` | 53 | Libros portal |
| `Assets/Scripts/Interaccion/ObjectTargetReturn.cs` | 50 | Objetos objetivo |
| `Assets/Scripts/Interaccion/DialogueTrigger.cs` | 39 | Diálogo de NPC por proximidad |
| `Assets/Scripts/UI/ScreenFader.cs` | 66 | Fundido a negro y carga de escena |
| `Assets/Scripts/UI/InteractionPromptUI.cs` | 23 | Cartel de interacción |
| `Assets/Scripts/UI/DialoguePanelUI.cs` | 23 | Panel de diálogo |
| `Assets/Scripts/Editor/InksideSceneBuilder.cs` | 1511 | **Núcleo.** Construye por API los 2 prefabs y las 4 escenas |

Los artefactos generados por ese último script también son parte del resultado y se pueden auditar: `Assets/_Comunes/Jugador/PlayerInkside.prefab`, `Assets/_Comunes/UI/Prefabs/Canvas_UI.prefab`, las 4 escenas en `Assets/_Escenas/` y los `VolumeProfile` en `Assets/_Mundos/*/Materiales/`.

### Restricciones del entorno. No son bugs, son el terreno

- El proyecto está configurado con **Input System nuevo únicamente** (`ProjectSettings/ProjectSettings.asset` → `activeInputHandler: 1`). Usar `Input.GetKeyDown` lanzaría excepción en runtime.
- Los modelos son `.glb` importados con glTFast. Vienen de Sketchfab y traen una rotación de 270° en X en el GameObject raíz, que convierte Z-up a Y-up. Sus escalas nativas son dispares: la casa hobbit está en centímetros, la oficina de Dumbledore está exportada a escala 0.01.
- El código se escribió **sin acceso a la interfaz gráfica del Editor**, por eso todo se resuelve por API y las posiciones son aproximadas por diseño.
- Render pipeline: URP.

### Desvíos conocidos respecto de la especificación

Fueron decisiones deliberadas y están documentadas. No los reportes como hallazgos nuevos, **pero sí evaluá si están bien implementados**, porque ahí puede haber bugs reales.

1. `RaycastInteractor` conserva el campo `KeyCode interactKey` que pide el contrato, pero lee la tecla con `Keyboard.current` traduciendo el `KeyCode` a `UnityEngine.InputSystem.Key` por nombre.
2. El builder normaliza la escala de cada modelo por su dimensión máxima (`FitSize`), porque las unidades nativas de los `.glb` son inconsistentes.
3. El spawn del jugador y la posición de props y objetivos se calculan por consultas de física (`TryFindSpawn`, `FindOpenYaw`, `PlaceClear`) en vez de usar el centro geométrico del modelo, porque ese centro cae dentro de muebles o paredes.
4. Se agregan piso y paredes invisibles de respaldo (`Fallback_Floor`, `Bounds_Walls`) con material plano placeholder.
5. Los recursos esenciales de TextMeshPro se extrajeron del `.unitypackage` a mano, porque `AssetDatabase.ImportPackage` no importa nada en modo batch con `-quit`.

### Verificación ya realizada

El último build cerró con 0 errores de compilación y 0 excepciones. Una validación por física confirmó en las 4 escenas: spawn sin solapamiento con geometría, piso a 5 cm de los pies, un solo `AudioListener`, una sola cámara etiquetada `MainCamera`, todas las referencias serializadas resueltas sin nulos, y los 3 objetivos con línea de visión desde el spawn.

**Tomá esto como punto de partida, no como garantía**: esa validación la escribió el mismo agente que escribió el código, así que comparte sus puntos ciegos.

### Foco de la revisión

Revisá todo, pero prestá atención especial a:

1. **Cumplimiento del contrato.** Nombres exactos de campos serializados, firmas públicas y comportamientos descritos en la sección 3 de la especificación. Un nombre de campo distinto rompe la asignación que hace el builder.
2. **Valores por objeto.** Contrastá la sección 6 de la especificación contra `GetSceneSpecs()`: escenas destino, clips de audio, textos de diálogo, qué libro lleva a qué mundo.
3. **Ciclo de vida de MonoBehaviour.** Orden de `Awake`/`Start`, corrutinas, `StopAllCoroutines`, componentes buscados en objetos desactivados, y qué pasa al cargar una escena en medio de una corrutina.
4. **Interacción por raycast.** Filtrado del propio jugador, tratamiento de triggers, tamaño del buffer de `RaycastNonAlloc`, uso de `GetComponentInParent`, y la posibilidad de interactuar dos veces o durante el fundido.
5. **Audio.** Ciclo de vida de `AudioSource.PlayClipAtPoint` cuando la escena se descarga poco después, y el `AudioSource` 3D montado sobre un objeto que se desactiva.
6. **Matemática de bounds y colliders.** `TryGetBounds`, `FitSize` y sobre todo `AddBoxCollider`: la conversión de tamaño de mundo a espacio local cuando el objeto tiene rotaciones no triviales, y si el collider resultante representa bien al modelo.
7. **Composición de rotaciones.** `PlaceGrounded` y `PlaceCentered` multiplican por la rotación existente en vez de reemplazarla. Verificá que no se acumulen rotaciones entre intentos sucesivos dentro de `PlaceClear`.
8. **Física en modo Editor.** Llamadas a `Physics.SyncTransforms`, orden entre crear colliders y consultarlos, y consultas de overlap contra `MeshCollider` cóncavos.
9. **API de Editor.** `SerializedObject` con `ApplyModifiedPropertiesWithoutUndo` sobre instancias de prefab, `PrefabUtility.SaveAsPrefabAsset`, `AddObjectToAsset` sobre `VolumeProfile`, borrar y recrear assets, y la recursión de `EnsureFolder`.
10. **Idempotencia.** ¿Qué pasa si se corre `BuildAllScenes` dos veces seguidas? ¿Y si se corre con las escenas ya abiertas o con assets a medio crear?
11. **Manejo de errores.** Conteo de `_errors`, qué pasa si falta un asset del manifiesto, y si un fallo parcial puede dejar una escena guardada en estado inconsistente.
12. **Restricciones de la sección 7.** Que no se hayan agregado sistemas, managers o scripts que la especificación prohíbe explícitamente.

### Formato del reporte

Entregá un reporte en Markdown con esta estructura.

Primero, un **resumen ejecutivo** de 3 a 5 líneas: estado general y cuántos hallazgos hay por severidad.

Después, los hallazgos **ordenados de mayor a menor severidad**, cada uno con estos campos:

- **ID**: correlativo, por ejemplo `H-01`.
- **Severidad**: Crítico, Alto, Medio o Bajo, según estas definiciones.
  - *Crítico*: impide compilar, impide completar el loop de juego, o deja una escena rota.
  - *Alto*: una funcionalidad requerida por la especificación no funciona o funciona mal en uso normal.
  - *Medio*: funciona, pero con un defecto perceptible, o es frágil ante un cambio razonable.
  - *Bajo*: calidad, claridad o casos borde improbables.
- **Título**: una línea.
- **Ubicación**: `archivo:línea`, más el fragmento de código citado.
- **Qué está mal**: la explicación técnica del defecto.
- **Efecto**: qué consecuencia concreta tiene para quien juega o para quien corre el builder. Si es un bug latente que hoy no se manifiesta, decilo explícitamente y aclará bajo qué condición se dispararía.
- **Solución propuesta**: el cambio concreto, con código si aplica, y **por qué esa solución** y no otra.
- **Confianza**: alta, media o baja.

Al final, una sección **"Veredicto de cumplimiento"** que recorra los items del checklist de la sección 8 de `Documentacion/Prompt_OneShot_Fable.md` y marque cada uno como CUMPLE, NO CUMPLE o PARCIAL, con la evidencia que lo respalda.
