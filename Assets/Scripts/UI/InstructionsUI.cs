using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Instrucciones del juego (imagen con los controles y qué hacer), en todos los mundos.
///
/// - Q las abre y Q las cierra. Esc también las cierra; en ese frame <see cref="QuitHandler"/> no sale del
///   juego (ver <see cref="EscConsumedFrame"/>). Mientras están abiertas el jugador se puede seguir moviendo,
///   pero <see cref="RaycastInteractor"/> no interactúa, para no abrir un libro sin querer por debajo.
/// - El cartel "[Q] Instrucciones" aparece sólo en la Biblioteca. Si el cartel de interactuar también está
///   visible, se ubica justo debajo de él; si no, ocupa su lugar, debajo de la mira.
///
/// Va en el objeto "Instructions" del prefab Canvas_UI, que está siempre activo (el panel es un hijo que
/// arranca desactivado, así que este script no puede vivir en él).
/// </summary>
public class InstructionsUI : MonoBehaviour
{
    [SerializeField] GameObject panel;                 // fondo oscuro + imagen; arranca desactivado
    [SerializeField] RectTransform hint;               // cartel "[Q] Instrucciones"
    [SerializeField] RectTransform interactionPrompt;  // contenedor del cartel de interactuar
    [SerializeField] GameObject interactionPromptText; // su texto: activo = el cartel se está viendo
    [SerializeField] string hubSceneName = "00_Biblioteca";
    [SerializeField] float gap = 8f;                   // separación entre los dos carteles

    /// <summary>true mientras las instrucciones están en pantalla. Lo consultan RaycastInteractor y QuitHandler.</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>Frame en el que Esc cerró las instrucciones: ese Esc no tiene que cerrar además el juego.</summary>
    public static int EscConsumedFrame { get; private set; } = -1;

    ScreenFader _fader;
    bool _isHub;

    void Awake()
    {
        _isHub = SceneManager.GetActiveScene().name == hubSceneName;
        _fader = FindAnyObjectByType<ScreenFader>(FindObjectsInactive.Include);
        if (panel == null) Debug.LogWarning("[InstructionsUI] Sin panel asignado: Q no va a mostrar nada.", this);
        SetOpen(false); // IsOpen es estático: cada escena arranca con las instrucciones cerradas
    }

    void OnDestroy()
    {
        IsOpen = false;
    }

    void Update()
    {
        bool loading = _fader != null && _fader.IsLoading;

#if ENABLE_INPUT_SYSTEM
        // El proyecto está en "Input System Package (New)" únicamente: Input.GetKeyDown lanzaría excepción.
        var keyboard = Keyboard.current;
        if (keyboard != null && !loading)
        {
            if (keyboard[Key.Q].wasPressedThisFrame) SetOpen(!IsOpen);
            else if (IsOpen && keyboard[Key.Escape].wasPressedThisFrame)
            {
                EscConsumedFrame = Time.frameCount;
                SetOpen(false);
            }
        }
#endif
        if (loading && IsOpen) SetOpen(false);

        UpdateHint(loading);
    }

    void SetOpen(bool open)
    {
        IsOpen = open;
        if (panel != null && panel.activeSelf != open) panel.SetActive(open);
    }

    void UpdateHint(bool loading)
    {
        if (hint == null) return;

        bool show = _isHub && !IsOpen && !loading;
        if (hint.gameObject.activeSelf != show) hint.gameObject.SetActive(show);
        if (!show || interactionPrompt == null) return;

        // Mismo ancla y pivote que el cartel de interactuar (centro, pivote arriba): se apila debajo de él
        // sólo cuando se está viendo, así nunca se superponen y tampoco queda un hueco cuando no está.
        float y = interactionPrompt.anchoredPosition.y;
        if (interactionPromptText != null && interactionPromptText.activeSelf)
            y -= interactionPrompt.rect.height + gap;
        var pos = hint.anchoredPosition;
        if (!Mathf.Approximately(pos.y, y)) hint.anchoredPosition = new Vector2(pos.x, y);
    }
}
