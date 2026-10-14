using UnityEngine;

/// <summary>
/// Plays the notes planned by VoiceContour. Uses a small pool of AudioSources because
/// AudioSource.pitch is per source: re-pitching a single source (as PlayOneShot would)
/// also re-pitches any blip that is still playing.
///
/// With no clips assigned it synthesises its own voice: one short vowel sound per vowel
/// (a glottal pulse run through vowel resonances), so letters sound like syllables rather than beeps.
/// </summary>
public class DialogueVoice : MonoBehaviour
{
    [Tooltip("Optional shared mixer routing. Existing voice synthesis and timing are preserved.")]
    public UnityEngine.Audio.AudioMixerGroup outputMixer;
    private const int PoolSize = 4;
    private const int SampleRate = 22050;

    // Fundamental of the generated voice at pitch 1 (a neutral low male voice).
    private const float BaseFundamental = 140f;

    // a, e, i, o, u, neutral. Formant frequencies F1/F2/F3 in Hz.
    private static readonly float[][] VowelFormants =
    {
        new[] { 730f, 1090f, 2440f },
        new[] { 530f, 1840f, 2480f },
        new[] { 270f, 2290f, 3010f },
        new[] { 570f, 840f, 2410f },
        new[] { 300f, 870f, 2240f },
        new[] { 500f, 1500f, 2500f }
    };

    // Formants are scaled down slightly: a longer "vocal tract" reads as a darker voice.
    private const float FormantScale = 0.94f;
    private const float GeneratedLowPassHz = 3200f;

    private static AudioClip[] generatedClips;

    private AudioSource[] pool;
    private AudioLowPassFilter[] filters;
    private AudioDistortionFilter[] distortions;
    private int next;

    private void Awake()
    {
        pool = new AudioSource[PoolSize];
        filters = new AudioLowPassFilter[PoolSize];
        distortions = new AudioDistortionFilter[PoolSize];

        for (int i = 0; i < PoolSize; i++)
        {
            // One object per slot: a low-pass filter affects every AudioSource on its GameObject,
            // so sharing one object would make the filters stack on each other.
            GameObject slot = new GameObject("VoiceSlot" + i);
            slot.transform.SetParent(transform, false);

            AudioSource source = slot.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = outputMixer;
            pool[i] = source;

            // Filters run in component order: distortion first, then the low-pass tames its harsh
            // overtones. Both are set per sound (see Play).
            distortions[i] = slot.AddComponent<AudioDistortionFilter>();
            distortions[i].distortionLevel = 0f;

            filters[i] = slot.AddComponent<AudioLowPassFilter>();
            filters[i].cutoffFrequency = 22000f;
        }
    }

    /// <summary>
    /// Plays the sound for characters [from, to) that were just revealed.
    /// At most one sound per call, so low frame rates or skips never stack a burst of blips.
    /// </summary>
    public void PlayRange(VoiceSettings settings, VoiceNote[] notes, int from, int to)
    {
        if (settings == null || notes == null)
            return;

        to = Mathf.Min(to, notes.Length);

        for (int i = to - 1; i >= from && i >= 0; i--)
        {
            if (notes[i].play)
            {
                Play(settings, notes[i]);
                return;
            }
        }
    }

    public void StopAll()
    {
        if (pool == null)
            return;

        foreach (AudioSource source in pool)
            source.Stop();
    }

    private void Play(VoiceSettings settings, VoiceNote note)
    {
        AudioClip clip = PickClip(settings, note.clipIndex);

        int slot = next;
        next = (next + 1) % PoolSize;

        AudioSource source = pool[slot];
        source.Stop();
        source.clip = clip;
        source.pitch = Mathf.Clamp(settings.basePitch * Mathf.Pow(2f, note.semitones / 12f), 0.3f, 3f);

        // Distortion makes the signal louder, so the volume is pulled back as it increases.
        float distortion = Mathf.Clamp01(settings.distortion);
        distortions[slot].distortionLevel = distortion;
        source.volume = note.volume * (1f - 0.4f * distortion);

        // Exponential sweep so the slider feels even: 0 = open (22 kHz), 1 = very muffled (1.2 kHz).
        filters[slot].cutoffFrequency = 22000f * Mathf.Pow(1200f / 22000f, Mathf.Clamp01(settings.muffle));

        source.Play();
    }

    private static AudioClip PickClip(VoiceSettings settings, int index)
    {
        if (settings.clips != null && settings.clips.Length > 0)
        {
            // Skip empty slots so a half-filled array still works.
            for (int k = 0; k < settings.clips.Length; k++)
            {
                AudioClip clip = settings.clips[(index + k) % settings.clips.Length];

                if (clip != null)
                    return clip;
            }
        }

        return GetGeneratedClip(index);
    }

    private static AudioClip GetGeneratedClip(int index)
    {
        if (generatedClips == null || generatedClips.Length != VowelFormants.Length || generatedClips[0] == null)
        {
            generatedClips = new AudioClip[VowelFormants.Length];

            for (int i = 0; i < generatedClips.Length; i++)
            {
                generatedClips[i] = SynthesizeVowel(i);
                generatedClips[i].hideFlags = HideFlags.HideAndDontSave;
            }
        }

        return generatedClips[Mathf.Abs(index) % generatedClips.Length];
    }

    // Source-filter synthesis: a glottal pulse train (the vocal folds) fed through three
    // resonators (the vocal tract) tuned to the vowel's formants.
    private static AudioClip SynthesizeVowel(int vowel)
    {
        bool neutral = vowel == VowelFormants.Length - 1;
        float duration = neutral ? 0.075f : 0.095f;
        int count = Mathf.RoundToInt(duration * SampleRate);
        float[] data = new float[count];

        System.Random rng = new System.Random(4242 + vowel);
        float[] formants = VowelFormants[vowel];
        float dt = 1f / SampleRate;

        // Resonator state for the three formants.
        float[] y1 = new float[3];
        float[] y2 = new float[3];
        float[] a = new float[3];
        float[] b = new float[3];
        float[] c = new float[3];

        for (int f = 0; f < 3; f++)
        {
            float frequency = formants[f] * FormantScale;
            float bandwidth = 60f + frequency * 0.08f;
            c[f] = -Mathf.Exp(-2f * Mathf.PI * bandwidth * dt);
            b[f] = 2f * Mathf.Exp(-Mathf.PI * bandwidth * dt) * Mathf.Cos(2f * Mathf.PI * frequency * dt);
            a[f] = 1f - b[f] - c[f];
        }

        float phase = 0f;
        float previousGlottal = 0f;
        float lowPassState = 0f;
        float lowPassAlpha = 1f - Mathf.Exp(-2f * Mathf.PI * GeneratedLowPassHz * dt);

        for (int i = 0; i < count; i++)
        {
            float t = i * dt;
            float progress = t / duration;

            // Pitch falls a little over the sound, with a faint vibrato: speech, not a held note.
            float fundamental = BaseFundamental * (1f - 0.1f * progress) * (1f + 0.012f * Mathf.Sin(2f * Mathf.PI * 5.5f * t));
            phase += fundamental * dt;
            if (phase >= 1f)
                phase -= 1f;

            // Rosenberg-style glottal pulse: smooth opening, quick closing, then silence.
            float glottal;
            if (phase < 0.55f)
                glottal = 0.5f * (1f - Mathf.Cos(Mathf.PI * phase / 0.55f));
            else if (phase < 0.75f)
                glottal = Mathf.Cos(0.5f * Mathf.PI * (phase - 0.55f) / 0.2f);
            else
                glottal = 0f;

            // The derivative of the pulse is what leaves the lips (adds the natural high-frequency tilt).
            float source = (glottal - previousGlottal) * 12f;
            previousGlottal = glottal;

            // A little breath noise, and a burst at the start of the neutral sound to hint at a consonant.
            float breath = ((float)rng.NextDouble() - 0.5f) * 0.08f;
            if (neutral && t < 0.014f)
                breath += ((float)rng.NextDouble() - 0.5f) * 0.5f * (1f - t / 0.014f);

            float x = source + breath;

            // Cascade of the three resonators.
            for (int f = 0; f < 3; f++)
            {
                float y = a[f] * x + b[f] * y1[f] + c[f] * y2[f];
                y2[f] = y1[f];
                y1[f] = y;
                x = y;
            }

            // Soften the top end: darker, less buzzy.
            lowPassState += lowPassAlpha * (x - lowPassState);

            // Soft attack and a longer release so syllables glide into each other.
            float attack = Mathf.SmoothStep(0f, 1f, t / 0.014f);
            float release = Mathf.SmoothStep(0f, 1f, (duration - t) / 0.04f);

            data[i] = lowPassState * attack * release;
        }

        // Normalize.
        float peak = 0.0001f;
        for (int i = 0; i < count; i++)
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));

        float gain = 0.85f / peak;
        for (int i = 0; i < count; i++)
            data[i] *= gain;

        AudioClip clip = AudioClip.Create("GeneratedVowel" + vowel, count, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private void OnDisable()
    {
        StopAll();
    }
}
