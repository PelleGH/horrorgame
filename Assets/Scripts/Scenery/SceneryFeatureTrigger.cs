using UnityEngine;

/// <summary>Connect a carriage trigger or UnityEvent to a streamed exterior event.</summary>
[AddComponentMenu("Train/Scenery Feature Trigger")]
public class SceneryFeatureTrigger : MonoBehaviour
{
    public RollingLandscape landscape;
    public SceneryFeature feature;
    [Min(30f)] public float leadDistance = 180f;
    [Min(1f)] public float featureLength = 100f;
    public ScenerySide side = ScenerySide.Both;
    [Min(0f)] public float offsetFromTrack;
    public bool triggerOnce = true;
    [Tooltip("Only this tag may activate a trigger collider. Leave empty for any collider. UnityEvents can always call Trigger directly.")]
    public string actorTag = "Player";
    public bool HasTriggered { get; private set; }

    void Reset()
    {
        var volume = GetComponent<Collider>();
        if (volume != null) volume.isTrigger = true;
    }
    void OnTriggerEnter(Collider other)
    {
        if (!string.IsNullOrEmpty(actorTag))
        {
            bool matches = false;
            for (Transform t = other.transform; t != null; t = t.parent)
                if (t.CompareTag(actorTag)) { matches = true; break; }
            if (!matches) return;
        }
        Trigger();
    }
    [ContextMenu("Trigger Feature")]
    public void Trigger()
    {
        if (!Application.isPlaying || (triggerOnce && HasTriggered) || landscape == null || !landscape.IsReady || feature == null) return;
        landscape.RequestFeature(feature, leadDistance, featureLength, side, offsetFromTrack);
        HasTriggered = true;
    }
    public void ResetTrigger() => HasTriggered = false;
}
