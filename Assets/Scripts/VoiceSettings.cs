using System;
using UnityEngine;

public enum VoiceScale
{
    Pentatonic,
    Chromatic,
    Custom
}

/// <summary>
/// How a character's speech sounds: which blip(s) to use and how letters and punctuation
/// shape the pitch. Used by VoiceContour (builds the melody) and DialogueVoice (plays it).
/// </summary>
[Serializable]
public class VoiceSettings
{
    private static readonly float[] PentatonicDegrees = { 0f, 2f, 4f, 7f, 9f, 12f, 14f };
    private static readonly float[] ChromaticDegrees = { 0f, 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f, 11f, 12f };

    [Header("Source")]
    [Tooltip("Short blips (60-100 ms). A random one is picked per sound. Empty = the built-in synthesised vowel voice.")]
    public AudioClip[] clips;
    [Range(0f, 1f)] public float volume = 0.5f;
    [Tooltip("Low-pass filter. 0 = clear, 1 = very muffled and dark.")]
    [Range(0f, 1f)] public float muffle = 0.35f;
    [Tooltip("Distortion. 0 = clean, 1 = heavily crushed and gritty. Applied before the muffle filter.")]
    [Range(0f, 1f)] public float distortion = 0f;

    [Header("Pitch Map")]
    [Tooltip("Voice register. 1 = clip as recorded, below 1 = deeper (and darker: vowels shift down too).")]
    public float basePitch = 0.85f;
    public VoiceScale scale = VoiceScale.Pentatonic;
    [Tooltip("Scales how far apart the letters' pitches are. Lower = calmer, more speech-like.")]
    public float pitchSpread = 0.6f;
    [Tooltip("Only used when Scale is Custom: 26 semitone offsets for a-z.")]
    public float[] letterSemitones = new float[26];

    [Header("Variation (semitones)")]
    public float jitterSemitones = 0.3f;
    [Tooltip("Slow random wander across the sentence, so it never sits on one contour.")]
    public float driftSemitones = 1f;
    [Tooltip("First letter of a word is lifted, the last one dips.")]
    public float wordStressSemitones = 0.6f;
    [Range(0f, 0.5f)] public float volumeJitter = 0.15f;

    [Header("Punctuation")]
    [Tooltip("Pitch rise over the last letters before a '?'.")]
    public float questionLift = 4f;
    [Tooltip("Extra pitch before a '!'.")]
    public float exclamationBoost = 2f;
    [Tooltip("Extra volume for '!' sentences.")]
    [Range(0f, 1f)] public float exclamationVolume = 0.25f;
    [Tooltip("Pitch change over the last letters before a '.'. Negative falls.")]
    public float periodFall = -1.5f;
    [Tooltip("Pitch change before '...'. Negative falls.")]
    public float ellipsisDrop = -3f;
    [Tooltip("Volume multiplier at the end of a '...' (1 = unchanged).")]
    [Range(0f, 1f)] public float ellipsisVolume = 0.6f;

    [Header("Rhythm")]
    [Tooltip("One sound per N letters within a word. The first letter of each word always sounds.")]
    [Min(1)] public int soundEveryNLetters = 3;
    public bool voiceCapitals = true;
    public float capitalAccent = 1f;

    /// <summary>Semitone offset for each letter a-z, derived from the scale (or the custom table).</summary>
    public float[] GetLetterTable()
    {
        float[] table = new float[26];

        if (scale == VoiceScale.Custom)
        {
            if (letterSemitones != null)
            {
                for (int i = 0; i < 26 && i < letterSemitones.Length; i++)
                    table[i] = letterSemitones[i];
            }

            return table;
        }

        float[] degrees = scale == VoiceScale.Pentatonic ? PentatonicDegrees : ChromaticDegrees;

        float mean = 0f;
        foreach (float degree in degrees)
            mean += degree;
        mean /= degrees.Length;

        // Stepping by 3 scatters neighbouring letters across the scale (3 is coprime with 7 and 13).
        for (int i = 0; i < 26; i++)
            table[i] = (degrees[(i * 3) % degrees.Length] - mean) * pitchSpread;

        return table;
    }
}
