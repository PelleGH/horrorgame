
public interface IInteractable
{
    bool CanInteract { get; }
    string InteractionPrompt { get; }

    void Interact(PlayerInteract player);
    void SetHighlight(bool highlighted);
}
