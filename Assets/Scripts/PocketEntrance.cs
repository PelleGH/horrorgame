using UnityEngine;

public class PocketEntrance : MonoBehaviour
{
    [HideInInspector] public int pocketIndex;
    public string pocketName = "Pocket";
    public Transform interiorView;
    public GameObject highlight;
    public void SetHighlight(bool enabled) { if (highlight != null) highlight.SetActive(enabled); }
}
