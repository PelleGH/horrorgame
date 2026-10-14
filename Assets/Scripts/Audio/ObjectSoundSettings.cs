using System;
using UnityEngine;
using UnityEngine.Audio;

namespace HorrorAudio
{
    [Serializable]
    public class ObjectSoundSettings
    {
        public bool loop;
        [Range(0f, 1f)] public float volume = 0.7f;
        public Vector2 pitchVariation = Vector2.one;
        [Range(0f, 1f)] public float spatialBlend = 1f;
        [Min(0.01f)] public float minDistance = 1f;
        [Min(0.1f)] public float maxDistance = 25f;
        [Range(0f, 5f)] public float doppler;
        public AudioMixerGroup output;
        [Header("Tone")]
        [Range(-12f, 12f)] public float bassDb;
        [Range(-12f, 12f)] public float trebleDb;
        [Header("Effects")]
        [Range(0f, 1f)] public float distortion;
        [Range(10f, 22000f)] public float lowPass = 22000f;
        [Range(10f, 22000f)] public float highPass = 10f;
        public AudioReverbPreset reverb = AudioReverbPreset.Off;
        [Range(-10000f, 0f)] public float reverbLevel = -1500f;
        [Range(0f, 1f)] public float echoWet;
        [Range(10f, 5000f)] public float echoDelay = 180f;
        [Range(0f, 1f)] public float echoDecay = 0.25f;

        public void Apply(GameSoundProfile profile)
        {
            profile.loop = loop; profile.volume = volume; profile.spatialBlend = spatialBlend;
            profile.minDistance = minDistance; profile.maxDistance = maxDistance;
            profile.dopplerLevel = doppler; profile.output = output;
            profile.bassDb = bassDb; profile.trebleDb = trebleDb;
            profile.distortion = distortion; profile.lowPass = lowPass; profile.highPass = highPass;
            profile.reverb = reverb; profile.reverbLevel = reverbLevel;
            profile.echoWet = echoWet; profile.echoDelay = echoDelay; profile.echoDecay = echoDecay;
        }
    }
}
