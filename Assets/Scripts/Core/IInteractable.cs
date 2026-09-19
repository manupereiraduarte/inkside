/// <summary>
/// Contrato de todo objeto con el que el jugador puede interactuar apuntando y presionando la tecla de interacción.
/// Lo detecta <see cref="RaycastInteractor"/> sobre el collider golpeado por el rayo.
/// </summary>
public interface IInteractable
{
    string GetInteractionPrompt();
    void Interact();
}
