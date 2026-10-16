using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>An absolute-distance occurrence. Its identity stays stable as the visible window moves.</summary>
public sealed class SceneryFeatureOccurrence
{
    public SceneryFeatureRule rule;
    public SceneryFeature feature;
    public double start;
    public double end;
    public int id;
    public int sideSign;

    public float offsetFromTrack => rule != null ? Mathf.Max(0f, rule.offsetFromTrack) : 0f;
    public float scale => rule != null ? Mathf.Max(0.001f, rule.scale) : 1f;
    public float width => feature != null ? Mathf.Max(1f, feature.width) * scale : 1f;
    public float height => feature != null ? Mathf.Max(1f, feature.height) * scale : 1f;
}

/// <summary>
/// Queries deterministic distance events without keeping the travelled route in memory.
/// Time and manually queued events use a small bounded queue instead.
/// </summary>
public sealed class SceneryFeaturePlanner
{
    public const int MaxOccurrences = 256;
    public const int MaxQueuedOccurrences = 128;

    readonly LandscapeSettings settings;
    readonly int seed;
    readonly Func<double, SceneryBiome> biomeAt;
    readonly List<SceneryFeatureOccurrence> queued = new List<SceneryFeatureOccurrence>();
    readonly HashSet<int> triggeredTimeRules = new HashSet<int>();
    int nextQueuedId = 1;

    public int QueuedCount => queued.Count;
    public int Revision { get; private set; }

    public SceneryFeaturePlanner(LandscapeSettings settings, int seed, Func<double, SceneryBiome> biomeAt = null)
    {
        this.settings = settings;
        this.seed = seed;
        this.biomeAt = biomeAt;
    }

    /// <summary>
    /// Replace output with occurrences touching [from, to], in authoring rule order.
    /// Distance occurrences are evaluated directly, including after long-distance seeks.
    /// Negative journey distance contains no scheduled occurrences.
    /// </summary>
    public void Collect(double from, double to, List<SceneryFeatureOccurrence> output)
    {
        if (output == null) throw new ArgumentNullException(nameof(output));
        output.Clear();
        if (!IsFinite(from) || !IsFinite(to)) return;
        if (from > to) { double swap = from; from = to; to = swap; }

        if (settings != null && settings.features != null)
        {
            for (int i = 0; i < settings.features.Count && output.Count < MaxOccurrences; i++)
            {
                SceneryFeatureRule rule = settings.features[i];
                if (!CanSpawn(rule) || rule.schedule == SceneryFeatureSchedule.Time) continue;
                double origin = Math.Max(0d, rule.startDistance);
                double length = SafeLength(rule.length) * SafeScale(rule.scale);
                if (rule.schedule == SceneryFeatureSchedule.Distance)
                {
                    AddIfVisible(rule, i, 0, origin, length, from, to, output);
                    continue;
                }

                bool random = rule.schedule == SceneryFeatureSchedule.Random;
                double minimum = Math.Max(1d, Math.Min(rule.randomSpacing.x, rule.randomSpacing.y));
                double maximum = Math.Max(minimum, Math.Max(rule.randomSpacing.x, rule.randomSpacing.y));
                double step = random ? (minimum + maximum) * 0.5d : Math.Max(1d, rule.repeatDistance);
                double jitter = random ? (maximum - minimum) * 0.5d : 0d;
                if (!IsFinite(step) || !IsFinite(jitter)) continue;

                // A half-range of jitter around evenly spaced bins keeps successive gaps
                // inside the authored minimum and maximum, without a history scan.
                long first = ClampIndex(Math.Floor((from - length - origin - jitter) / step));
                long last = ClampIndex(Math.Floor((to - origin) / step));
                if (last < 0 || first > last) continue;
                first = Math.Max(0L, first);
                if (rule.endDistance > 0f)
                    last = Math.Min(last, ClampIndex(Math.Floor((rule.endDistance - origin) / step)));

                // Guard an accidental world-sized query or a tiny interval. Normal streaming
                // windows are far smaller; authoring order determines the capacity priority.
                long candidates = Math.Min(last - first + 1L, MaxOccurrences * 4L);
                for (long n = 0; n < candidates && output.Count < MaxOccurrences; n++)
                {
                    long index = first + n;
                    double start = origin + index * step;
                    if (random) start += Unit(Hash(seed, i, index, 0x9E3779B9u)) * jitter;
                    AddIfVisible(rule, i, index, start, length, from, to, output);
                }
            }
        }

        for (int i = 0; i < queued.Count && output.Count < MaxOccurrences; i++)
        {
            SceneryFeatureOccurrence occurrence = queued[i];
            if (CanSpawn(occurrence.rule) && occurrence.end >= from && occurrence.start <= to)
                output.Add(occurrence);
        }
    }

    /// <summary>
    /// Time rules fire once when their threshold is reached. Place them ahead of the
    /// current train position so geometry does not suddenly appear around the player.
    /// Also retire queued events behind the streamed window using the same look-ahead.
    /// </summary>
    public void TriggerTimeRules(double elapsedTime, double currentDistance, float lookAhead)
    {
        if (!IsFinite(elapsedTime) || !IsFinite(currentDistance)) return;
        double ahead = Mathf.Max(0f, lookAhead);
        double keepBehind = Math.Max(100d, ahead * 2d);
        for (int i = queued.Count - 1; i >= 0; i--)
            if (queued[i].end < currentDistance - keepBehind) { queued.RemoveAt(i); Revision++; }

        if (settings == null || settings.features == null) return;
        for (int i = 0; i < settings.features.Count; i++)
        {
            SceneryFeatureRule rule = settings.features[i];
            if (!CanSpawn(rule) || rule.schedule != SceneryFeatureSchedule.Time ||
                elapsedTime < Math.Max(0f, rule.startTime) || triggeredTimeRules.Contains(i)) continue;
            triggeredTimeRules.Add(i);
            double start = Math.Max(0d, currentDistance + ahead);
            if (rule.endDistance > 0f && start > rule.endDistance) continue;
            if (!AllowsBiome(rule, start)) continue;
            uint hash = Hash(seed, i, 0, 0x85EBCA6Bu);
            if (Unit(hash) >= rule.chance) continue;
            Enqueue(MakeOccurrence(rule, i, 0, start, SafeLength(rule.length) * SafeScale(rule.scale)));
        }
    }

    /// <summary>Schedule a runtime feature at an absolute journey distance.</summary>
    public void QueueFeature(SceneryFeature feature, double start, float length,
        ScenerySide side = ScenerySide.Both, float offset = 0f)
    {
        if (feature == null || !IsFinite(start)) return;
        int ordinal = nextQueuedId++;
        var rule = new SceneryFeatureRule
        {
            name = feature.name,
            feature = feature,
            schedule = SceneryFeatureSchedule.Distance,
            startDistance = (float)Math.Max(0d, start),
            length = (float)SafeLength(length),
            side = side,
            offsetFromTrack = Mathf.Max(0f, offset)
        };
        SceneryFeatureOccurrence occurrence = MakeOccurrence(rule, int.MinValue, ordinal,
            Math.Max(0d, start), SafeLength(length));
        Enqueue(occurrence);
    }

    void AddIfVisible(SceneryFeatureRule rule, int ruleIndex, long index, double start, double length,
        double from, double to, List<SceneryFeatureOccurrence> output)
    {
        if (!IsFinite(start) || start > to || start + length < from ||
            (rule.endDistance > 0f && start > rule.endDistance)) return;
        if (!AllowsBiome(rule, start)) return;
        uint hash = Hash(seed, ruleIndex, index, 0x85EBCA6Bu);
        if (Unit(hash) >= rule.chance) return;
        output.Add(MakeOccurrence(rule, ruleIndex, index, start, length));
    }

    SceneryFeatureOccurrence MakeOccurrence(SceneryFeatureRule rule, int ruleIndex, long index,
        double start, double length)
    {
        int sideSign = rule.side == ScenerySide.Left ? -1 : rule.side == ScenerySide.Right ? 1 :
            ((Hash(seed, ruleIndex, index, 0x27D4EB2Du) & 1u) == 0u ? -1 : 1);
        if (rule.feature.kind == SceneryFeatureKind.Tunnel) sideSign = 0;
        return new SceneryFeatureOccurrence
        {
            rule = rule,
            feature = rule.feature,
            start = start,
            end = start + length,
            id = unchecked((int)Hash(seed, ruleIndex, index, 0x165667B1u)),
            sideSign = sideSign
        };
    }

    void Enqueue(SceneryFeatureOccurrence occurrence)
    {
        if (queued.Count >= MaxQueuedOccurrences) return;
        queued.Add(occurrence); Revision++;
    }

    static bool CanSpawn(SceneryFeatureRule rule) => rule != null && rule.enabled &&
        rule.feature != null && rule.chance > 0f;

    bool AllowsBiome(SceneryFeatureRule rule, double distance) => rule.allowedBiomes == null ||
        rule.allowedBiomes.Count == 0 || (biomeAt != null && rule.allowedBiomes.Contains(biomeAt(distance)));

    static double SafeLength(float length) => float.IsNaN(length) || float.IsInfinity(length)
        ? 1d : Math.Max(1d, length);

    static double SafeScale(float scale) => float.IsNaN(scale) || float.IsInfinity(scale)
        ? 1d : Math.Max(0.001d, scale);

    static long ClampIndex(double index)
    {
        if (double.IsNaN(index)) return 0;
        if (index <= 0d) return 0;
        return index >= long.MaxValue / 2d ? long.MaxValue / 2 : (long)index;
    }

    static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    static double Unit(uint value) => (value >> 8) * (1d / 16777216d);

    static uint Hash(int journeySeed, int ruleIndex, long index, uint salt)
    {
        unchecked
        {
            uint value = (uint)journeySeed ^ salt;
            value ^= (uint)ruleIndex * 0x9E3779B9u;
            value ^= (uint)index * 0x85EBCA6Bu;
            value ^= (uint)((ulong)index >> 32) * 0xC2B2AE35u;
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            return value ^ (value >> 16);
        }
    }
}
