using TMPro;
using UnityEngine;

/// <summary>
/// Cartel "[E] Interactuar". Show activa el texto y lo setea; Hide lo desactiva.
/// </summary>
public class InteractionPromptUI : MonoBehaviour
{
    [SerializeField] TMP_Text promptText;

    public void Show(string text)
    {
        if (promptText == null) return;
        if (promptText.text != text) promptText.text = text;
        if (!promptText.gameObject.activeSelf) promptText.gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (promptText == null) return;
        if (promptText.gameObject.activeSelf) promptText.gameObject.SetActive(false);
    }
}
