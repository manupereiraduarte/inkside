using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Tecla Z: volver a la Biblioteca desde cualquier mundo, por si el jugador no encuentra el objetivo.
/// Usa el mismo fundido a negro que los libros y los objetivos, sin sonido y sin cartel (la tecla figura en
/// la imagen de instrucciones). En la Biblioteca no hace nada.
///
/// Igual que <see cref="QuitHandler"/>, no hay que agregarlo a ninguna escena ni prefab: se instala solo al
/// arrancar, sobrevive los cambios de escena con DontDestroyOnLoad, y por ser código puro no lo pisa un
/// rebuild de las escenas. A diferencia de QuitHandler, también funciona dentro del Editor.
/// </summary>
public class ReturnToLibrary : MonoBehaviour
{
    const string HubSceneName = "00_Biblioteca";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        var go = new GameObject("ReturnToLibrary");
        go.AddComponent<ReturnToLibrary>();
        DontDestroyOnLoad(go);
    }

    void Update()
    {
        if (!ReturnPressed()) return;
        if (SceneManager.GetActiveScene().name == HubSceneName) return;

        // El fader es de la escena activa (cada escena trae el suyo en su Canvas), así que se busca en el
        // momento. Si ya está cargando otra escena, se respeta esa salida.
        var fader = FindAnyObjectByType<ScreenFader>(FindObjectsInactive.Include);
        if (fader == null)
        {
            SceneManager.LoadScene(HubSceneName);
            return;
        }
        if (!fader.IsLoading) fader.FadeOutThenLoad(HubSceneName);
    }

    static bool ReturnPressed()
    {
#if ENABLE_INPUT_SYSTEM
        // El proyecto está en "Input System Package (New)" únicamente: Input.GetKeyDown lanzaría excepción.
        var keyboard = Keyboard.current;
        return keyboard != null && keyboard[Key.Z].wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Z);
#endif
    }
}
