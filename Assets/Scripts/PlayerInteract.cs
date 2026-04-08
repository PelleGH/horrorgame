using UnityEngine;

public class PlayerInteract : MonoBehaviour
{
    public float interactDistance = 3f;
    public LayerMask interactLayer;

    private Door currentDoor;

    void Update()
    {

        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactDistance, interactLayer))
        {
            Door door = hit.collider.GetComponentInParent<Door>();

            if (door != null)
            {
                currentDoor = door;
                door.SetHighlight(true);

                if (Input.GetKeyDown(KeyCode.F))
                {
                    door.ToggleDoor();
                }

                return;
            }
        }

        // If not looking anymore
        if (currentDoor != null)
        {
            currentDoor.SetHighlight(false);
            currentDoor = null;
        }
    }
}