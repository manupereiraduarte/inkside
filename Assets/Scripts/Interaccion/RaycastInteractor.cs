using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Va en la MainCamera del jugador. Cada frame lanza un rayo desde el centro de la cámara;
/// si golpea un <see cref="IInteractable"/> muestra el prompt y, al presionar la tecla, llama a Interact().
/// Si no golpea nada interactuable, oculta el prompt de inmediato.
///
/// Nota: el proyecto está configurado con "Input System Package (New)" únicamente
/// (ProjectSettings → activeInputHandler = 1), por lo que Input.GetKeyDown lanzaría excepción.
/// Se mantiene el campo KeyCode del contrato y se traduce a UnityEngine.InputSystem.Key.
/// </summary>
public class RaycastInteractor : MonoBehaviour
{
    [SerializeField] float interactionRange = 3f;
    [SerializeField] KeyCode interactKey = KeyCode.E;
    [SerializeField] InteractionPromptUI promptUI;

    Camera _camera;
    ScreenFader _fader;
    int _layerMask = ~0;

#if ENABLE_INPUT_SYSTEM
    Key _key = Key.E;
#endif

    void Awake()
    {
        _camera = GetComponent<Camera>();
        if (_camera == null) _camera = Camera.main;

        // El rayo nace dentro de la cápsula del jugador: en vez de filtrar impactos a mano (que obliga a
        // recorrer todos los resultados y puede desbordar un buffer), se excluye la capa del propio jugador.
        var controller = transform.root.GetComponentInChildren<CharacterController>(true);
        if (controller != null) _layerMask = ~(1 << controller.gameObject.layer);

        _fader = FindFirstObjectByType<ScreenFader>(FindObjectsInactive.Include);

        if (promptUI == null)
            Debug.LogWarning("[RaycastInteractor] Sin promptUI asignado: no se va a ver el cartel, pero la interacción sigue activa.", this);

#if ENABLE_INPUT_SYSTEM
        // Los nombres de KeyCode y de Key no coinciden para muchas teclas (Alpha1/Digit1, Return/Enter...).
        // Si la traducción no es posible se avisa y se usa E, en vez de quedar con una tecla inválida.
        if (!System.Enum.TryParse(interactKey.ToString(), out _key)
            || !System.Enum.IsDefined(typeof(Key), _key)
            || _key == Key.None)
        {
            _key = Key.E;
            Debug.LogWarning($"[RaycastInteractor] KeyCode.{interactKey} no tiene equivalente en Input System; se usa la tecla E.", this);
        }
#endif
    }

    void Update()
    {
        if (_camera == null) return;

        // Durante el fundido de salida la escena ya se está yendo: no aceptar más interacciones.
        if (_fader != null && _fader.IsLoading)
        {
            if (promptUI != null) promptUI.Hide();
            return;
        }

        IInteractable target = FindInteractable();

        if (target == null)
        {
            if (promptUI != null) promptUI.Hide();
            return;
        }

        if (promptUI != null) promptUI.Show(target.GetInteractionPrompt());

        if (InteractPressed())
            target.Interact();
    }

    IInteractable FindInteractable()
    {
        var ray = new Ray(_camera.transform.position, _camera.transform.forward);

        // Physics.Raycast devuelve siempre el impacto MÁS CERCANO. Si ese impacto no es interactuable,
        // tapa lo que haya detrás: no se puede interactuar a través de una pared.
        if (!Physics.Raycast(ray, out var hit, interactionRange, _layerMask, QueryTriggerInteraction.Ignore))
            return null;

        return hit.collider.GetComponentInParent<IInteractable>();
    }

    bool InteractPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        return keyboard != null && keyboard[_key].wasPressedThisFrame;
#else
        return Input.GetKeyDown(interactKey);
#endif
    }
}
