using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Train/Train Route")]
[DisallowMultipleComponent]
public class TrainRoute : MonoBehaviour
{
    [Tooltip("World-space control points in travel order. Keep them outside the moving train.")]
    public Transform[] waypoints = new Transform[0];
    public bool loop = true;
    [Range(8, 128)] public int samplesPerSegment = 48;
    public Color routeColor = new Color(0.2f, 0.85f, 1f);

    private readonly List<Vector3> samples = new List<Vector3>();
    private readonly List<float> distances = new List<float>();
    public float Length { get; private set; }
    public bool IsValid => samples.Count > 1 && Length > 0.01f;

    [ContextMenu("Rebuild Route")]
    public void Rebuild()
    {
        samples.Clear();
        distances.Clear();
        Length = 0f;
        if (waypoints == null || waypoints.Length < (loop ? 3 : 2)) return;
        foreach (Transform point in waypoints) if (point == null) return;
        int segments = loop ? waypoints.Length : waypoints.Length - 1;
        int resolution = Mathf.Clamp(samplesPerSegment, 8, 128);
        for (int segment = 0; segment < segments; segment++)
        {
            for (int step = 0; step < resolution; step++)
                AddSample(Curve(segment, step / (float)resolution));
        }
        AddSample(Curve(segments - 1, 1f));
    }

    void AddSample(Vector3 point)
    {
        if (samples.Count > 0) Length += Vector3.Distance(samples[samples.Count - 1], point);
        samples.Add(point);
        distances.Add(Length);
    }

    Vector3 Point(int index)
    {
        int count = waypoints.Length;
        if (loop) return waypoints[(index % count + count) % count].position;
        // Extrapolate endpoint controls to avoid a zero endpoint tangent.
        if (index < 0) return 2f * waypoints[0].position - waypoints[1].position;
        if (index >= count) return 2f * waypoints[count - 1].position - waypoints[count - 2].position;
        return waypoints[index].position;
    }

    Vector3 Curve(int segment, float t)
    {
        Vector3 a = Point(segment - 1), b = Point(segment), c = Point(segment + 1), d = Point(segment + 2);
        return 0.5f * ((2f * b) + (-a + c) * t
            + (2f * a - 5f * b + 4f * c - d) * t * t
            + (-a + 3f * b - 3f * c + d) * t * t * t);
    }

    public bool Evaluate(float distance, out Vector3 position, out Vector3 forward)
    {
        position = transform.position;
        forward = transform.forward;
        if (!IsValid) return false;
        float along = loop ? Mathf.Repeat(distance, Length) : Mathf.Clamp(distance, 0f, Length);
        int low = 1, high = distances.Count - 1;
        while (low < high)
        {
            int mid = (low + high) / 2;
            if (distances[mid] < along) low = mid + 1;
            else high = mid;
        }
        int index = low;
        float span = distances[index] - distances[index - 1];
        float blend = span > 0.00001f ? (along - distances[index - 1]) / span : 0f;
        position = Vector3.Lerp(samples[index - 1], samples[index], blend);
        // Blend neighboring segment tangents to keep rotation smooth across samples.
        Vector3 before = samples[index] - samples[Mathf.Max(0, index - 2)];
        Vector3 after = samples[Mathf.Min(samples.Count - 1, index + 1)] - samples[index - 1];
        if (loop && index == 1) before = samples[1] - samples[samples.Count - 2];
        if (loop && index == samples.Count - 1) after = samples[1] - samples[index - 1];
        forward = Vector3.Slerp(before.normalized, after.normalized, blend).normalized;
        if (forward.sqrMagnitude < 0.001f) forward = transform.forward;
        return true;
    }

    void OnValidate() { if (!Application.isPlaying) Rebuild(); }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) Rebuild();
        Gizmos.color = routeColor;
        for (int i = 1; i < samples.Count; i++) Gizmos.DrawLine(samples[i - 1], samples[i]);
        if (waypoints == null) return;
        foreach (Transform point in waypoints)
            if (point != null) Gizmos.DrawWireSphere(point.position, 0.35f);
    }
}
