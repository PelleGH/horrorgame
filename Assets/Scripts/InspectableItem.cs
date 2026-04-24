using UnityEngine;

public class InspectableItem : MonoBehaviour
{
    [Header("Position")]
    public float desiredDistance = 1.2f;
    public float minDistance = 0.35f;
    public float wallPadding = 0.08f;
    public float collisionRadius = 0.25f;

    [Header("Rotation")]
    public Vector3 startingRotationOffset;
    public float rotationSpeed = 160f;

    [Header("Scale")]
    public bool overrideScale = false;
    public Vector3 inspectScale = Vector3.one;
}