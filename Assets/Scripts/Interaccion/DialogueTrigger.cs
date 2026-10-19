using UnityEngine;

/// <summary>
/// Va en cada NPC. Requiere un BoxCollider con isTrigger = true en el mismo GameObject o en un hijo.
/// No usa IInteractable: el diálogo aparece solo por proximidad al entrar en el trigger y se oculta al salir.
/// El contenido del diálogo es una imagen que ya trae el nombre y el texto del personaje.
/// </summary>
public class DialogueTrigger : MonoBehaviour
{
    [SerializeField] Sprite dialogueSprite;
    [SerializeField] DialoguePanelUI dialoguePanel;

    const string PlayerTag = "Player";

    void Awake()
    {
        bool hasTrigger = false;
        foreach (var col in GetComponentsInChildren<Collider>(true))
        {
            if (col.isTrigger) { hasTrigger = true; break; }
        }
        if (!hasTrigger)
            Debug.LogWarning($"[DialogueTrigger] '{name}' no tiene ningún Collider con isTrigger; el diálogo nunca se va a disparar.", this);
        if (dialoguePanel == null)
            Debug.LogWarning($"[DialogueTrigger] '{name}' no tiene dialoguePanel asignado.", this);
        if (dialogueSprite == null)
            Debug.LogWarning($"[DialogueTrigger] '{name}' no tiene dialogueSprite asignado; el panel va a aparecer vacío.", this);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(PlayerTag) || dialoguePanel == null) return;
        dialoguePanel.Show(dialogueSprite);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(PlayerTag) || dialoguePanel == null) return;
        dialoguePanel.Hide();
    }
}
