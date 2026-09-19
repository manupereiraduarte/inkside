using TMPro;
using UnityEngine;

/// <summary>
/// Panel de diálogo de NPC. El GameObject arranca desactivado; Show lo activa con nombre y línea, Hide lo desactiva.
/// </summary>
public class DialoguePanelUI : MonoBehaviour
{
    [SerializeField] TMP_Text npcNameText;
    [SerializeField] TMP_Text dialogueBodyText;

    public void Show(string npcName, string line)
    {
        if (npcNameText != null) npcNameText.text = npcName;
        if (dialogueBodyText != null) dialogueBodyText.text = line;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }
}
