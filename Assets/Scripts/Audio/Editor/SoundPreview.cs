using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace HorrorAudio.Editor
{
    [InitializeOnLoad]
    public static class SoundPreview
    {
        static AudioClip rendered;
        static MethodInfo play, stop;
        public static string Status { get; private set; } = "Preview is idle.";
        static SoundPreview()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type utility = assembly.GetType("UnityEditor.AudioUtil");
                if (utility == null) continue;
                play = utility.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new[] {typeof(AudioClip), typeof(int), typeof(bool)}, null);
                stop = utility.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            }
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
            EditorApplication.playModeStateChanged += _ => Stop();
        }
        public static GameSoundProfile CreateProfile(SoundEmitter emitter)
        {
            if (!emitter.HasDirectSound) return emitter.defaultProfile != null ? UnityEngine.Object.Instantiate(emitter.defaultProfile) : null;
            var profile = ScriptableObject.CreateInstance<GameSoundProfile>(); emitter.soundSettings.Apply(profile);
            var layers = new System.Collections.Generic.List<SoundLayer>();
            var clips = new System.Collections.Generic.List<AudioClip>();
            if (emitter.audioClip != null) clips.Add(emitter.audioClip);
            if (emitter.clipVariations != null) foreach (AudioClip clip in emitter.clipVariations) if (clip != null) clips.Add(clip);
            if (clips.Count > 0) layers.Add(new SoundLayer { clips = clips.ToArray(), volume = 1f, pitch = emitter.soundSettings.pitchVariation });
            if (emitter.extraLayers != null)
                foreach (SoundLayer layer in emitter.extraLayers)
                    if (layer != null) layers.Add(JsonUtility.FromJson<SoundLayer>(JsonUtility.ToJson(layer)));
            profile.layers = layers.ToArray(); return profile;
        }
        public static bool Play(SoundEmitter emitter)
        {
            Stop();
            if (play == null || stop == null) { Status = "Audio preview is unavailable in this Unity editor version. Use Play Mode."; return false; }
            GameSoundProfile profile = CreateProfile(emitter);
            if (profile == null) { Status = "Assign an audio clip or profile first."; return false; }
            try
            {
                rendered = Render(profile, emitter.volume, emitter.pitch);
                if (rendered == null) { Status = "No readable audio. For imported clips, use Decompress On Load; streaming clips can be auditioned in Play Mode."; return false; }
                rendered.hideFlags = HideFlags.HideAndDontSave;
                play.Invoke(null, new object[] {rendered, 0, false});
                Status = "Playing 2D preview (up to 10 seconds). Reverb/distortion approximate the runtime effects; rooms, mixer routing, occlusion and Doppler require Play Mode.";
                return true;
            }
            catch (Exception error) { Stop(); Status = "Preview failed: " + error.GetBaseException().Message; return false; }
            finally { UnityEngine.Object.DestroyImmediate(profile); }
        }
        public static void Stop()
        {
            stop?.Invoke(null, null);
            if (rendered != null) UnityEngine.Object.DestroyImmediate(rendered);
            rendered = null; Status = "Preview stopped.";
        }
        public static AudioClip Render(GameSoundProfile profile, float objectVolume = 1f, float objectPitch = 1f)
        {
            const int rate = 22050;
            int length = rate * 10;
            float[] mixed = new float[length]; int used = 0;
            if (profile.layers == null) return null;
            foreach (SoundLayer layer in profile.layers)
            {
                if (layer == null) continue;
                AudioClip clip = layer.NextClip(profile.loop);
                if (clip == null || clip.loadType != AudioClipLoadType.DecompressOnLoad) continue;
                clip.LoadAudioData();
                // Bound temporary memory for unusually long or multichannel imported clips.
                if ((long)clip.samples * clip.channels > 8000000) continue;
                float[] source = new float[clip.samples * clip.channels];
                if (!clip.GetData(source, 0)) continue;
                float pitch = Mathf.Clamp((layer.pitch.x + layer.pitch.y) * 0.5f * objectPitch, 0.1f, 3f);
                int frames = profile.loop ? length : Mathf.Min(length, Mathf.CeilToInt(clip.samples * (float)rate / clip.frequency / pitch));
                for (int i = 0; i < frames; i++)
                {
                    double index = i * (double)clip.frequency / rate * pitch;
                    int a = (int)index % clip.samples, b = (a + 1) % clip.samples;
                    float sample = 0f;
                    for (int channel = 0; channel < clip.channels; channel++)
                        sample += Mathf.Lerp(source[a * clip.channels + channel], source[b * clip.channels + channel], (float)(index - Math.Floor(index)));
                    mixed[i] += sample / clip.channels * layer.volume;
                }
                used = Math.Max(used, frames);
            }
            if (used == 0) return null;
            var bass = SoundToneFilter.CreateShelf(profile.bassDb, 250f, rate, false);
            var treble = SoundToneFilter.CreateShelf(profile.trebleDb, 4000f, rate, true);
            double[] bassState = new double[4], trebleState = new double[4];
            float low = 0f, highLow = 0f;
            float lowAlpha = 1f - Mathf.Exp(-2f * Mathf.PI * Mathf.Clamp(profile.lowPass, 10f, 22000f) / rate);
            float highAlpha = 1f - Mathf.Exp(-2f * Mathf.PI * Mathf.Clamp(profile.highPass, 10f, 22000f) / rate);
            int echoSamples = Mathf.Clamp(Mathf.RoundToInt(profile.echoDelay * rate / 1000f), 1, length - 1);
            int[] reflectionSamples = {rate * 31 / 1000, rate * 47 / 1000, rate * 73 / 1000, rate * 101 / 1000};
            float[] tail = new float[length];
            int outputLength = Math.Min(length, used + (profile.echoWet > 0f || profile.reverb != AudioReverbPreset.Off ? rate * 2 : 0));
            for (int i = 0; i < outputLength; i++)
            {
                float sample = (float)treble.Process(bass.Process(mixed[i], bassState, 0), trebleState, 0);
                if (profile.distortion > 0f)
                {
                    float drive = 1f + profile.distortion * 12f;
                    sample = Mathf.Lerp(sample, (float)Math.Tanh(sample * drive) / (float)Math.Tanh(drive), profile.distortion);
                }
                low += lowAlpha * (sample - low); if (profile.lowPass < 21900f) sample = low;
                highLow += highAlpha * (sample - highLow); if (profile.highPass > 10f) sample -= highLow;
                float wet = 0f;
                if (i >= echoSamples) wet += tail[i - echoSamples] * profile.echoWet * profile.echoDecay;
                if (profile.reverb != AudioReverbPreset.Off)
                    foreach (int delay in reflectionSamples)
                        if (i >= delay) wet += tail[i - delay] * 0.18f * Mathf.Pow(10f, profile.reverbLevel / 2000f);
                tail[i] = Mathf.Clamp(sample + wet, -4f, 4f);
                mixed[i] = Mathf.Clamp(tail[i] * profile.volume * objectVolume, -0.95f, 0.95f);
            }
            float[] result = new float[outputLength]; Array.Copy(mixed, result, outputLength);
            AudioClip output = AudioClip.Create("Horror Audio editor preview", result.Length, 1, rate, false);
            output.SetData(result, 0); return output;
        }
    }
}
