# Guía de instalación y creación del proyecto Unity — inkside

Paso a paso para dejar el proyecto Unity 6 LTS (URP) creado y enganchado a este mismo repositorio, sin perder ni duplicar la carpeta `Assets/` que ya está armada. Marcá cada casillero a medida que lo hagas.

## 1. Unity Hub y versión del Editor

- [x] Instalar **Unity Hub**.
- [x] Instalar el Editor Unity 6 LTS.
- [x] Módulos por defecto (sin WebGL/Android/iOS, el proyecto es solo Windows de escritorio).

## 2. Crear el proyecto — el paso más importante ✅ (ya resuelto)

**Lo que pasó en la práctica:** Unity Hub no dejó crear el proyecto directamente sobre `C:\Users\manup\TP-CDEV\inkside` (quedó un registro fantasma en la base de datos del Hub de un intento anterior fallido, con el mismo nombre). La solución fue crear el proyecto desde cero con otro nombre y migrar el repo git adentro:

- [x] Unity Hub → **New project** → Template **3D (URP)** → Name: `TP-inkside` → Location: `C:\Users\manup\TP-CDEV`. Resultado: `C:\Users\manup\TP-CDEV\TP-inkside\` con el scaffold completo de Unity (`ProjectSettings/`, `Packages/`, `Library/`, `Assets/Settings`, `Assets/Scenes/SampleScene`, etc.).
- [x] Se cerró el Editor y se migró todo el repo (`.git`, `.gitignore`, `README.md`, `Documentacion/`, y `Assets/_Mundos`, `Assets/_Comunes`, `Assets/Scripts`, `Assets/_Escenas`) adentro de `TP-inkside`, fusionado con lo que generó Unity. La carpeta vieja `inkside` quedó vacía (se puede borrar a mano desde el Explorador cuando quieras).
- [x] Se confirmó con `git status` que el historial y el remoto (`origin` → `github.com/manupereiraduarte/inkside`) siguen intactos, y que `.gitignore` ya ignora correctamente `Library/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.sln`.

**La carpeta del proyecto (y de aquí en adelante, la raíz del repo) es:** `C:\Users\manup\TP-CDEV\TP-inkside`

- [ ] Confirmá que en Unity Hub el proyecto "TP-inkside" aparece en la lista y abre sin errores.

## 3. Primer chequeo dentro del Editor

- [ ] Confirmá que en la ventana **Project** (abajo) aparecen tus carpetas: `_Mundos`, `_Comunes`, `Scripts`, `_Escenas`.
- [ ] Es normal ver íconos de "archivo no reconocido" en los `.glb` por ahora — se van a arreglar solos en el paso 4.

## 4. Instalar paquetes (Package Manager)

`Window > Package Manager` (arriba a la izquierda del Editor, dentro del menú `Window`).

- [ ] **glTFast** (obligatorio para que los `.glb` funcionen): botón `+` (arriba a la izquierda de la ventana de Package Manager) → **Add package by name** → escribir `com.unity.cloud.gltfast` → **Add**.
- [ ] **Input System**: mismo botón `+` → **Add package by name** → `com.unity.inputsystem` → **Add**. Unity va a preguntar si querés activar el nuevo sistema de input y reiniciar el Editor — decile que **sí**.
- [ ] Verificá que **Universal RP** ya figura en la lista "In Project" (debería venir solo por haber usado el template 3D (URP)).
- [ ] TextMeshPro no hace falta instalarlo ahora — Unity te va a ofrecer "Import TMP Essentials" la primera vez que uses un texto TMP, ahí aceptás.
- [ ] Después de instalar glTFast, hacé click derecho en la ventana **Project** → **Reimport All** (o simplemente esperá — Unity suele reimportar solo). Confirmá que ahora los `.glb` (por ejemplo `Assets/_Mundos/01_LotR/Modelos/one_ring.glb`) muestran una miniatura 3D en vez del ícono genérico.

## 5. Importar Starter Assets (First Person Character Controller) ✅ hecho

El paquete original "Starter Assets - First Person Character Controller" ya no existe como tal en la Asset Store — Unity lo reemplazó por **"First & Third Person Character Controllers"** (assetstore.unity.com/packages/3d/characters/first-person-third-person-character-controllers-196526), compatible con URP. Se importó vía `Window > Package Manager > My Assets`.

Quedó en `Assets/Starter Assets/` (con espacio, **no** `StarterAssets/`) — **no reorganizar esta carpeta**. Lo que nos interesa de ahí:

- Prefab del jugador: `Assets/Starter Assets/Runtime/FirstPersonController/Prefabs/PlayerCapsule.prefab`
- Cámara: `Assets/Starter Assets/Runtime/FirstPersonController/Prefabs/MainCamera.prefab` (y hay un `NestedParent_Unpack.prefab` en la misma carpeta que podría combinar ambos — confirmar abriendo `Assets/Starter Assets/Sample/FirstPersonController/Playground.unity` una vez para ver cómo están armados en la práctica).
- Script de movimiento: `Assets/Starter Assets/Runtime/FirstPersonController/Scripts/FirstPersonController.cs`
- Input: `Assets/Starter Assets/Runtime/InputSystem/StarterAssets.inputactions` + `StarterAssetsInputs.cs`

El paquete también trae Third Person Controller, un robot de ejemplo (Kyle) y controles táctiles para mobile — no los usamos, pero no molestan si quedan sin tocar.

## 6. Confirmar que git ve todo bien

Una vez completado esto, avisame y corro `git status` para confirmar que:
- `Library/`, `Temp/`, `obj/`, `UserSettings/` (que Unity acaba de generar) están correctamente ignorados por nuestro `.gitignore` y no aparecen como "para commitear".
- `ProjectSettings/`, `Packages/manifest.json` sí aparecen como nuevos (esos sí se versionan).

---

Cuando termines estos 6 pasos (o si te trabás en cualquiera), contame en qué punto estás y seguimos — no hace falta que hagas todo de una sola vez.
