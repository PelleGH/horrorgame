using UnityEngine;

namespace HorrorAudio
{
    public class SoundAcousticMaterial : MonoBehaviour
    {
        [Tooltip("Broadband approximation, not inferred from the visual material.")]
        [Range(0.01f, 1f)] public float absorption = 0.2f;
        public bool measureThicknessFromCollider = true;
        [Min(0.001f)] public float fallbackThickness = 0.1f;
        [Range(0f, 60f)] public float baseTransmissionLossDb = 8f;
        [Range(0f, 100f)] public float lossDbPerMetre = 30f;
        [Range(10f, 22000f)] public float transmissionLowPass = 1400f;
        [Min(0.001f)] public float fullFilteringThickness = 0.2f;

        public float MeasureThickness(RaycastHit entry, Vector3 listener, Vector3 direction, float distance)
        {
            if (measureThicknessFromCollider && entry.collider.Raycast(new Ray(listener, -direction), out RaycastHit exit, distance))
            {
                float thickness = distance - entry.distance - exit.distance;
                if (thickness > 0.001f) return thickness;
            }
            return Mathf.Max(0.001f, fallbackThickness);
        }
        public float TransmissionGain(float thickness) => Mathf.Pow(10f, -(baseTransmissionLossDb + lossDbPerMetre * thickness) / 20f);
        public float TransmissionCutoff(float thickness) => Mathf.Lerp(22000f, transmissionLowPass, Mathf.Clamp01(thickness / Mathf.Max(0.001f, fullFilteringThickness)));

        public float EstimateSmallestDimension()
        {
            Collider collider = GetComponent<Collider>();
            if (collider == null) collider = GetComponentInChildren<Collider>();
            if (collider == null) return fallbackThickness;
            Vector3 size;
            if (collider is BoxCollider box)
            {
                Vector3 scale = box.transform.lossyScale;
                size = Vector3.Scale(box.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            }
            else size = collider.bounds.size;
            return Mathf.Max(0.001f, Mathf.Min(size.x, size.y, size.z));
        }
    }
}
