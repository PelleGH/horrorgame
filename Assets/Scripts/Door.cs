using UnityEngine;

public class Door : MonoBehaviour
{
    public Transform doorRoot; // the hinge object
    public Renderer handleRenderer;

    public float openAngle = 90f;
    public float speed = 3f;

    private bool isOpen = false;
    private bool isAnimating = false;

    private Quaternion closedRot;
    private Quaternion openRot;

    private Material mat;

    void Start()
    {
        closedRot = doorRoot.localRotation;
        openRot = Quaternion.Euler(0, openAngle, 0) * closedRot;

        mat = handleRenderer.material;
        mat.DisableKeyword("_EMISSION");
    }

    public void SetHighlight(bool state)
    {
        if (state)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.green * 2f);
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
        }
    }

    public void ToggleDoor()
    {
        if (!isAnimating)
            StartCoroutine(RotateDoor());
    }

    System.Collections.IEnumerator RotateDoor()
    {
        isAnimating = true;

        Quaternion target = isOpen ? closedRot : openRot;

        while (Quaternion.Angle(doorRoot.localRotation, target) > 0.1f)
        {
            doorRoot.localRotation = Quaternion.Slerp(
                doorRoot.localRotation,
                target,
                Time.deltaTime * speed
            );
            yield return null;
        }

        doorRoot.localRotation = target;
        isOpen = !isOpen;
        isAnimating = false;
    }
}