using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel de diálogo de NPC. Muestra una imagen que ya trae adentro el nombre del personaje y su texto,
/// así que acá no se compone nada: se asigna el sprite y se activa. El GameObject arranca desactivado.
/// </summary>
public class DialoguePanelUI : MonoBehaviour
{
    [SerializeField] Image dialogueImage;

    void Awake()
    {
        if (dialogueImage == null) dialogueImage = GetComponent<Image>();
    }

    public void Show(Sprite sprite)
    {
        if (dialogueImage == null)
        {
            Debug.LogWarning("[DialoguePanelUI] Sin Image asignada: no se puede mostrar el diálogo.", this);
            return;
        }

        dialogueImage.sprite = sprite;
        // Las imágenes son de 2752x1536 (16:9) y traen fondo propio: estirarlas las arruina.
        dialogueImage.preserveAspect = true;

        if (!gameObject.activeSelf) gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }
}
