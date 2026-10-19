using UnityEngine;

/// <summary>
/// Va en cada NPC. El diálogo se abre apuntando al personaje y presionando la tecla de interacción,
/// igual que los libros de la Biblioteca: ya no salta por proximidad. Eso exige que el NPC tenga un
/// Collider SÓLIDO (no trigger), porque <see cref="RaycastInteractor"/> consulta con
/// QueryTriggerInteraction.Ignore y un trigger sería invisible para el rayo.
/// El contenido del diálogo es una imagen que ya trae el nombre y el texto del personaje.
/// Volver a presionar la tecla cierra la tarjeta, y también se cierra sola si el jugador se aleja.
/// </summary>
public class DialogueTrigger : MonoBehaviour, IInteractable
{
    [SerializeField] string npcName = "el personaje";
    [SerializeField] Sprite dialogueSprite;
    [SerializeField] DialoguePanelUI dialoguePanel;
    [SerializeField] float closeDistance = 6f;

    const string PlayerTag = "Player";

    Transform _player;
    bool _open;

    void Awake()
    {
        bool hasSolidCollider = false;
        foreach (var col in GetComponentsInChildren<Collider>(true))
        {
            if (!col.isTrigger) { hasSolidCollider = true; break; }
        }
        if (!hasSolidCollider)
            Debug.LogWarning($"[DialogueTrigger] '{name}' no tiene ningún Collider sólido; el rayo de interacción lo ignora y no se va a poder hablar con él.", this);
        if (dialoguePanel == null)
            Debug.LogWarning($"[DialogueTrigger] '{name}' no tiene dialoguePanel asignado.", this);
        if (dialogueSprite == null)
            Debug.LogWarning($"[DialogueTrigger] '{name}' no tiene dialogueSprite asignado; el panel va a aparecer vacío.", this);

        var player = GameObject.FindGameObjectWithTag(PlayerTag);
        if (player != null) _player = player.transform;
    }

    public string GetInteractionPrompt() => _open ? "[E] Cerrar" : $"[E] Charlar con {npcName}";

    public void Interact()
    {
        if (dialoguePanel == null) return;

        if (_open) Close();
        else
        {
            dialoguePanel.Show(dialogueSprite);
            _open = true;
        }
    }

    void Update()
    {
        // Si el jugador se va caminando con la tarjeta abierta no tiene cómo cerrarla: el prompt sólo
        // aparece mientras apunta al NPC. Así que al alejarse se cierra sola.
        if (!_open || _player == null) return;
        if (Vector3.Distance(_player.position, transform.position) > closeDistance) Close();
    }

    void Close()
    {
        if (dialoguePanel != null) dialoguePanel.Hide();
        _open = false;
    }
}
