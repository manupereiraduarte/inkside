# inkside — Biblioteca de Relatos Virtuales

Experiencia interactiva en primera persona hecha en Unity. Explorás una **biblioteca antigua** y, a través de tres libros mágicos, viajás a **dioramas cerrados** ambientados en tres obras literarias. En cada mundo un personaje te encarga buscar un objeto; al encontrarlo volvés a la biblioteca.

| Libro | Mundo | Personaje | Objeto a encontrar |
|---|---|---|---|
| 📕 El Señor de los Anillos | Interior de Bolsón Cerrado | Gandalf | El Anillo Único |
| 📘 El Principito | Asteroide B-612 | El Zorro | La Rosa |
| 📗 Harry Potter | Oficina de Dumbledore | Dumbledore | La Snitch Dorada |

---

## 1. Instalar Unity

El proyecto está hecho con una versión exacta del Editor. Si abrís con otra, Unity va a querer migrarlo y pueden romperse materiales y escenas.

> **Versión requerida: `6000.6.1f1`** (Unity 6)

1. Descargá **Unity Hub** desde [unity.com/download](https://unity.com/download) e instalalo.
2. Abrilo y creá una cuenta de Unity (es gratis). La licencia **Personal** alcanza.
3. Instalá el Editor en la versión exacta. La forma más rápida es hacer clic en este enlace, que le pide al Hub esa versión directamente:

   `unityhub://6000.6.1f1/7efac9f6c10e`

   Si el enlace no te funciona, andá a **Unity Hub → Installs → Install Editor → Archive** y buscá `6000.6.1f1` en el listado de descargas.
4. Cuando te pregunte qué módulos agregar, **dejá los que vienen por defecto**. No hace falta WebGL, Android ni iOS: el proyecto es solo para Windows de escritorio.

La instalación pesa varios GB y tarda un rato largo.

## 2. Bajar el proyecto

Necesitás [Git](https://git-scm.com/downloads) instalado. Abrí una terminal en la carpeta donde quieras guardar el proyecto y:

```bash
git clone https://github.com/manupereiraduarte/inkside.git
```

El repositorio pesa alrededor de **230 MB** porque incluye los modelos 3D, así que la descarga tarda unos minutos. No usa Git LFS: no hay que configurar nada extra.

Si no querés usar Git, podés bajar el ZIP desde la página del repositorio con **Code → Download ZIP** y descomprimirlo.

## 3. Abrir el proyecto en Unity

1. En **Unity Hub**, andá a la pestaña **Projects**.
2. Clic en **Add → Add project from disk**.
3. Elegí la carpeta `inkside` que acabás de bajar (la que contiene `Assets/`, `Packages/` y `ProjectSettings/`) y confirmá.
4. El proyecto aparece en la lista. Hacé clic en su nombre para abrirlo.

> **La primera vez tarda bastante: entre 5 y 15 minutos.** Unity tiene que importar casi 300 MB de modelos y texturas y armar su carpeta `Library/`. Van a aparecer varias barras de progreso y la ventana puede quedar sin responder. Es normal, no lo cierres. Las veces siguientes abre en segundos.

Si al abrirlo te avisa que el proyecto fue creado con otra versión, cancelá y revisá que hayas instalado `6000.6.1f1`.

## 4. Jugarlo desde el Editor

1. En la ventana **Project** (abajo), abrí `Assets` → `_Escenas` y hacé **doble clic en `00_Biblioteca`**. Es la escena por la que tiene que arrancar el juego.
2. Apretá el botón **▶ Play** arriba del todo, en el centro.
3. Clic dentro de la ventana **Game** para que tome el mouse.

Para salir del modo de juego, apretá **▶ Play** otra vez.

> El mouse queda capturado por la cámara. Si lo necesitás de vuelta sin salir del juego, apretá **Esc**.

## 5. Controles

| Acción | Tecla |
|---|---|
| Caminar | `W` `A` `S` `D` o las flechas |
| Mirar | Mover el mouse |
| Correr | Mantener `Shift` |
| Saltar | `Espacio` |
| **Interactuar** | **`E`** |

También funciona un joystick para caminar, mirar, correr y saltar. **Interactuar es solo con la tecla `E`.**

## 6. Cómo se juega

Todo pasa con una sola tecla. En el centro de la pantalla tenés una mira, y cuando apuntás a algo con lo que podés interactuar aparece un cartel justo debajo diciendo qué va a pasar. Si no aparece el cartel, es que no estás apuntando bien o estás demasiado lejos: el alcance es de **3 metros**.

1. **En la biblioteca**, acercate a la mesa y apuntá a uno de los tres libros. Va a aparecer **`[E] Abrir el libro`**. Apretá `E` y la pantalla funde a negro hacia ese mundo.
2. **En el mundo**, buscá al personaje y apuntale. Aparece **`[E] Charlar con Gandalf`** (o el Zorro, o Dumbledore). Apretá `E` y te cuenta qué tenés que buscar. Apretá `E` otra vez para cerrar la tarjeta, o simplemente alejate.
3. **Buscá el objeto** y apuntale. Aparece **`[E] Tomar`**. Al apretar `E` volvés solo a la biblioteca.

Pistas por mundo:

- **El Señor de los Anillos** — el Anillo no está apoyado en el piso: flota en el aire dentro de la casa, bastante por encima de tu cabeza. Mirá hacia arriba.
- **El Principito** — la Rosa está lejos y escondida detrás de la curvatura del asteroide. Es la única que hay que caminar para encontrar.
- **Harry Potter** — la Snitch también flota alto, pero tiene un brillo dorado propio y se la escucha batir las alas cuando te acercás.

No hay menú ni pantalla final, y no se guarda el progreso: podés entrar a los tres mundos en cualquier orden y repetirlos todas las veces que quieras.

## 7. Generar el ejecutable (.exe)

Si querés jugarlo sin abrir Unity:

1. **File → Build Profiles** (en versiones anteriores de Unity es *Build Settings*).
2. Verificá que la plataforma seleccionada sea **Windows**. Las cuatro escenas ya están configuradas en el orden correcto, no hay que agregarlas.
3. Clic en **Build**, elegí una carpeta vacía y esperá.
4. Al terminar, ejecutá el `.exe` que quedó en esa carpeta.

En el ejecutable se sale con **Alt + F4**.

---

## Problemas frecuentes

| Síntoma | Qué hacer |
|---|---|
| Unity ofrece actualizar el proyecto a otra versión | Cancelar e instalar `6000.6.1f1` desde el Hub |
| La primera apertura parece colgada | Esperar: la importación inicial tarda hasta 15 minutos |
| El juego arranca y no se ve nada | Estás en la escena equivocada: abrí `Assets/_Escenas/00_Biblioteca` |
| No puedo sacar el mouse de la ventana | `Esc` |
| Apunto a un libro y no aparece el cartel | Acercate más: el alcance es de 3 m, y no se puede interactuar a través de otro objeto |
| Errores rojos en la consola al abrir | Esperar a que termine de compilar; si persisten, **Assets → Reimport All** |

## Documentación

- **[Documentacion/GDD.md](Documentacion/GDD.md)** — documento de diseño: narrativa, catálogo de assets, arquitectura de scripts y decisiones técnicas.
- [Documentacion/Setup_Unity.md](Documentacion/Setup_Unity.md) — notas de cómo se creó el proyecto sobre este repositorio.

## Para desarrollar

El proyecto se construyó por código. En el menú **Inkside** del Editor hay cuatro herramientas:

| Herramienta | Para qué |
|---|---|
| `Build All Scenes` | Regenera las 4 escenas y los prefabs desde cero |
| `Guardar layout de la escena abierta` | Persiste el acomodado manual para que un rebuild no lo borre |
| `Ajustar tamaño del jugador (escena abierta)` | Aplica el tamaño de jugador que le corresponde a cada mundo |
| `Reajustar colliders de los interactuables (escena abierta)` | Recalcula los colliders sobre la geometría actual |

> Si vas a correr `Build All Scenes`, guardá antes el layout de las escenas que hayas acomodado a mano. El detalle de cómo encaja cada pieza está en el GDD.
