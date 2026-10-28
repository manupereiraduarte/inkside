using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Salida del juego con Escape. El proyecto no tiene menú ni pantalla final, y el ejecutable arranca en
/// pantalla completa sin bordes (Player Settings: fullscreenMode = FullScreenWindow, resizableWindow = off),
/// así que sin esto la única forma de cerrarlo es Alt+F4.
///
/// No hay que agregarlo a ninguna escena ni prefab: se instala solo al arrancar el juego, sobrevive los
/// cambios de escena con DontDestroyOnLoad, y por ser código puro no lo pisa un rebuild de las escenas.
/// </summary>
public class QuitHandler : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        // En el Editor no se instala a propósito. Ahí Escape ya sirve para liberar el mouse de la ventana
        // de juego, y además Application.Quit() no hace nada dentro del Editor: quedaría una tecla que
        // parece no funcionar y se perdería una función que se usa todo el tiempo al acomodar escenas.
        if (Application.isEditor) return;

        var go = new GameObject("QuitHandler");
        go.AddComponent<QuitHandler>();
        DontDestroyOnLoad(go);
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        // El proyecto está en "Input System Package (New)" únicamente (activeInputHandler = 1),
        // por lo que Input.GetKeyDown lanzaría excepción.
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard[Key.Escape].wasPressedThisFrame) Application.Quit();
#else
        if (Input.GetKeyDown(KeyCode.Escape)) Application.Quit();
#endif
    }
}
