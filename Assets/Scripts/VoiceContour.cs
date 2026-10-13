using System.Collections.Generic;
using UnityEngine;

/// <summary>One planned sound, for one visible character.</summary>
public struct VoiceNote
{
    public bool play;
    public float semitones;
    public float volume;
    public int clipIndex;
}

/// <summary>
/// Turns a line of text into a melody: one VoiceNote per character. Pure logic with no audio,
/// so it is cheap to run once when a line starts. The same text + seed always gives the same notes.
/// </summary>
public static class VoiceContour
{
    // How many letters before a sentence end the question/exclamation/fall contour spans.
    private const int ContourLetters = 4;

    private enum Terminator
    {
        None,
        Period,
        Question,
        Exclaim,
        Ellipsis
    }

    /// <summary>Stable seed from text (string.GetHashCode is not guaranteed stable).</summary>
    public static int Seed(string text)
    {
        unchecked
        {
            int hash = (int)2166136261;

            if (text != null)
            {
                foreach (char c in text)
                    hash = (hash ^ c) * 16777619;
            }

            return hash;
        }
    }

    /// <param name="text">The visible characters only (no rich-text tags), index-aligned with the typewriter.</param>
    public static VoiceNote[] Build(string text, VoiceSettings settings, int seed)
    {
        int n = text != null ? text.Length : 0;
        VoiceNote[] notes = new VoiceNote[n];

        if (n == 0 || settings == null)
            return notes;

        System.Random rng = new System.Random(seed);
        float[] table = settings.GetLetterTable();
        int clipCount = Mathf.Max(1, CountClips(settings));
        int every = Mathf.Max(1, settings.soundEveryNLetters);

        // Look-ahead: which sentence ends how, and how far each letter is from that end.
        Terminator[] terminators = new Terminator[n];
        int[] rankFromEnd = new int[n];
        AnalyseSentences(text, terminators, rankFromEnd);

        float drift = 0f;
        int wordLetter = 0;

        for (int i = 0; i < n; i++)
        {
            char c = text[i];

            // Apostrophes stay inside the word ("don't") and are never voiced.
            if (IsApostrophe(c))
                continue;

            // Spaces and punctuation are silent and end the word.
            if (!char.IsLetterOrDigit(c))
            {
                wordLetter = 0;
                continue;
            }

            // The random draws below always happen, voiced or not, so a line stays reproducible.
            drift += ((float)rng.NextDouble() - 0.5f) * 0.8f;
            drift = Mathf.Clamp(drift, -settings.driftSemitones, settings.driftSemitones);
            float jitter = ((float)rng.NextDouble() * 2f - 1f) * settings.jitterSemitones;
            float volumeNoise = ((float)rng.NextDouble() * 2f - 1f) * settings.volumeJitter;
            int clipIndex = rng.Next(clipCount);

            // With no clips assigned the voice is synthesised, one sound per vowel: pick the vowel for this letter.
            if (CountClips(settings) == 0)
                clipIndex = VowelFor(text, i);

            float semitones = 0f;

            if (char.IsLetter(c))
            {
                char lower = char.ToLowerInvariant(c);
                // a-z by position; other letters (å, ä, ö, é...) fall back to a stable hash so they are not silent.
                int letterIndex = (lower >= 'a' && lower <= 'z') ? lower - 'a' : c % 26;
                semitones = table[letterIndex];
            }

            bool wordEnd = i + 1 >= n || !(char.IsLetterOrDigit(text[i + 1]) || IsApostrophe(text[i + 1]));
            float stress = wordLetter == 0
                ? settings.wordStressSemitones
                : (wordEnd ? -settings.wordStressSemitones * 0.5f : 0f);

            int rank = rankFromEnd[i];
            float t = rank < ContourLetters
                ? Mathf.SmoothStep(0f, 1f, 1f - rank / (float)ContourLetters)
                : 0f;

            float contour = 0f;
            float volume = settings.volume * (1f + volumeNoise);

            switch (terminators[i])
            {
                case Terminator.Question:
                    contour = settings.questionLift * t;
                    break;
                case Terminator.Exclaim:
                    contour = settings.exclamationBoost * t;
                    volume += settings.exclamationVolume * Mathf.Lerp(0.5f, 1f, t);
                    break;
                case Terminator.Period:
                    contour = settings.periodFall * t;
                    break;
                case Terminator.Ellipsis:
                    contour = settings.ellipsisDrop * t;
                    volume *= Mathf.Lerp(1f, settings.ellipsisVolume, t);
                    break;
            }

            if (settings.voiceCapitals && char.IsUpper(c))
            {
                semitones += settings.capitalAccent;
                volume *= 1.15f;
            }

            bool sentenceEnd = rank == 0 && terminators[i] != Terminator.None;

            notes[i] = new VoiceNote
            {
                // Thin the sounds out: every word starts with one, then every Nth letter, plus the
                // last letter before a sentence end so a "?" lift is always audible.
                play = wordLetter == 0 || sentenceEnd || wordLetter % every == 0,
                semitones = semitones + drift + jitter + stress + contour,
                volume = Mathf.Clamp01(volume),
                clipIndex = clipIndex
            };

            wordLetter++;
        }

        return notes;
    }

    private static int CountClips(VoiceSettings settings)
    {
        if (settings.clips == null)
            return 0;

        int count = 0;
        foreach (AudioClip clip in settings.clips)
        {
            if (clip != null)
                count++;
        }

        return count;
    }

    // Index into DialogueVoice's generated vowels: 0 a, 1 e, 2 i, 3 o, 4 u, 5 neutral.
    private static int VowelIndex(char c)
    {
        switch (char.ToLowerInvariant(c))
        {
            case 'a': return 0;
            case 'e':
            case 'ä': // ä
            case 'é': // é
                return 1;
            case 'i':
            case 'y':
                return 2;
            case 'o':
            case 'å': // å
            case 'ö': // ö
                return 3;
            case 'u': return 4;
            default: return -1;
        }
    }

    // A vowel keeps its own sound. A consonant borrows the next vowel in its word (like "ba", "te"),
    // so the babble flows in syllables instead of repeating one flat sound.
    private static int VowelFor(string text, int i)
    {
        int own = VowelIndex(text[i]);
        if (own >= 0)
            return own;

        for (int k = i + 1; k < text.Length && k <= i + 2; k++)
        {
            if (!char.IsLetter(text[k]))
                break;

            int vowel = VowelIndex(text[k]);
            if (vowel >= 0)
                return vowel;
        }

        return 5;
    }

    private static bool IsApostrophe(char c) => c == '\'' || c == '’';

    private static bool IsTerminator(char c) => c == '.' || c == '!' || c == '?' || c == '…';

    // Splits the text into sentences ending in . ! ? or ... and records, for every letter, the
    // sentence's terminator and how many letters follow it in that sentence.
    private static void AnalyseSentences(string text, Terminator[] terminators, int[] rankFromEnd)
    {
        List<int> letters = new List<int>();
        int n = text.Length;
        int i = 0;

        while (i < n)
        {
            char c = text[i];

            if (IsTerminator(c))
            {
                // A decimal point (3.5) is not a sentence end.
                if (c == '.' && i > 0 && i + 1 < n && char.IsDigit(text[i - 1]) && char.IsDigit(text[i + 1]))
                {
                    i++;
                    continue;
                }

                int j = i;
                bool question = false, exclaim = false, ellipsis = false;
                int count = 0;

                while (j < n && IsTerminator(text[j]))
                {
                    if (text[j] == '?') question = true;
                    else if (text[j] == '!') exclaim = true;
                    else if (text[j] == '…') ellipsis = true;

                    count++;
                    j++;
                }

                Terminator type = question ? Terminator.Question
                    : exclaim ? Terminator.Exclaim
                    : (ellipsis || count >= 2) ? Terminator.Ellipsis
                    : Terminator.Period;

                CloseSentence(letters, type, terminators, rankFromEnd);
                i = j;
                continue;
            }

            if (char.IsLetterOrDigit(c))
                letters.Add(i);

            i++;
        }

        // Trailing text with no punctuation.
        CloseSentence(letters, Terminator.None, terminators, rankFromEnd);
    }

    private static void CloseSentence(List<int> letters, Terminator type, Terminator[] terminators, int[] rankFromEnd)
    {
        for (int k = 0; k < letters.Count; k++)
        {
            terminators[letters[k]] = type;
            rankFromEnd[letters[k]] = letters.Count - 1 - k;
        }

        letters.Clear();
    }
}
