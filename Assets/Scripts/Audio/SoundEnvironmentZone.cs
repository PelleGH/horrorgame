using UnityEngine;

namespace HorrorAudio
{
    [RequireComponent(typeof(BoxCollider))]
    public class SoundEnvironmentZone : MonoBehaviour
    {
        public int priority;
        [Range(0f, 1f)] public float volume = 1f;
        [Range(10f, 22000f)] public float lowPass = 22000f;
        public AudioReverbPreset reverb = AudioReverbPreset.Room;
        [Header("Room acoustics (Unity units are metres)")]
        public bool calculateFromRoomSize;
        [Range(0.01f, 1f)] public float wallAbsorption = 0.15f;
        [Range(0.01f, 1f)] public float floorAbsorption = 0.25f;
        [Range(0.01f, 1f)] public float ceilingAbsorption = 0.15f;
        [Range(-10000f, 0f)] public float roomLevel = -1200f;
        [Range(-10000f, 0f)] public float reflectionsLevel = -1800f;
        [Range(-10000f, 0f)] public float tailLevel = -1000f;
        [Range(0f, 0.5f)] public float roomEchoWet;
        [Range(0f, 1f)] public float roomEchoDecay = 0.25f;
        [Header("Optional six-direction measurement")]
        public LayerMask measurementMask = ~0;
        [Min(1f)] public float maxMeasurementDistance = 40f;
        private BoxCollider region;
        public Vector3 RoomSize
        {
            get
            {
                BoxCollider box = region != null ? region : GetComponent<BoxCollider>();
                Vector3 scale = transform.lossyScale;
                return Vector3.Scale(box.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            }
        }
        public float RoomVolume => RoomSize.x * RoomSize.y * RoomSize.z;
        public float SurfaceArea => 2f * (RoomSize.x * RoomSize.y + RoomSize.x * RoomSize.z + RoomSize.y * RoomSize.z);
        public float EstimatedDecayTime
        {
            get
            {
                Vector3 size = RoomSize;
                float absorptionArea = 2f * (size.x * size.y + size.y * size.z) * Mathf.Max(0.01f, wallAbsorption)
                    + size.x * size.z * Mathf.Max(0.01f, floorAbsorption + ceilingAbsorption);
                return Mathf.Clamp(0.161f * RoomVolume / Mathf.Max(0.01f, absorptionArea), 0.1f, 10f);
            }
        }
        public float ReflectionDelay => Mathf.Clamp(Mathf.Min(RoomSize.x, RoomSize.y, RoomSize.z) / 343f, 0f, 0.3f);
        public float EchoDelayMs => Mathf.Round(Mathf.Clamp(2000f * Mathf.Max(RoomSize.x, RoomSize.y, RoomSize.z) / 343f, 10f, 2000f));
        public void ApplyRoomReverb(AudioReverbFilter filter, float blend)
        {
            filter.reverbPreset = AudioReverbPreset.User;
            filter.room = roomLevel;
            filter.reflectionsLevel = reflectionsLevel;
            filter.reverbLevel = tailLevel;
            filter.decayTime = Mathf.Lerp(filter.decayTime, EstimatedDecayTime, blend);
            filter.reflectionsDelay = Mathf.Lerp(filter.reflectionsDelay, ReflectionDelay, blend);
            filter.reverbDelay = Mathf.Clamp(ReflectionDelay * 0.5f, 0f, 0.1f);
            float absorption = (wallAbsorption * 4f + floorAbsorption + ceilingAbsorption) / 6f;
            filter.roomHF = Mathf.Lerp(-500f, -6000f, absorption);
            filter.decayHFRatio = Mathf.Clamp(1f - absorption * 0.8f, 0.1f, 2f);
        }
        public bool MeasureRoomFromWalls()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            Vector3 origin = transform.TransformPoint(box.center);
            Vector3[] axes = {transform.right, transform.up, transform.forward};
            Vector3 size = Vector3.zero, centerOffset = Vector3.zero;
            float wallSum = 0f; int wallCount = 0;
            RaycastHit[,] results = new RaycastHit[3, 2];
            Physics.SyncTransforms();
            for (int axis = 0; axis < 3; axis++)
            {
                if (!Physics.Raycast(origin, axes[axis], out results[axis, 0], maxMeasurementDistance, measurementMask, QueryTriggerInteraction.Ignore)
                    || !Physics.Raycast(origin, -axes[axis], out results[axis, 1], maxMeasurementDistance, measurementMask, QueryTriggerInteraction.Ignore)) return false;
                size[axis] = results[axis, 0].distance + results[axis, 1].distance;
                centerOffset[axis] = (results[axis, 0].distance - results[axis, 1].distance) * 0.5f;
            }
            Vector3 scale = transform.lossyScale;
            if (Mathf.Min(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)) < 0.0001f) return false;
            box.size = new Vector3(size.x / Mathf.Abs(scale.x), size.y / Mathf.Abs(scale.y), size.z / Mathf.Abs(scale.z));
            box.center += new Vector3(centerOffset.x / scale.x, centerOffset.y / scale.y, centerOffset.z / scale.z);
            for (int axis = 0; axis < 3; axis++)
                for (int side = 0; side < 2; side++)
                {
                    var material = results[axis, side].collider.GetComponentInParent<SoundAcousticMaterial>();
                    if (material == null) continue;
                    if (axis == 1) { if (side == 0) ceilingAbsorption = material.absorption; else floorAbsorption = material.absorption; }
                    else { wallSum += material.absorption; wallCount++; }
                }
            if (wallCount > 0) wallAbsorption = wallSum / wallCount;
            calculateFromRoomSize = true;
            return true;
        }
        void Reset() { GetComponent<BoxCollider>().isTrigger = true; }
        void OnEnable() { region = GetComponent<BoxCollider>(); region.isTrigger = true; SoundEngine.Zones.Add(this); SoundEngine.Ensure(); }
        void OnDisable() { SoundEngine.Zones.Remove(this); }
        public bool Contains(Vector3 point) => region != null && region.enabled && region.bounds.Contains(point)
            && (region.ClosestPoint(point) - point).sqrMagnitude < 0.00001f;
    }
}
