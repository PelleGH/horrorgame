using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Distance-based bends and hills shared by the view outside and the train simulation.</summary>
[Serializable]
public class SceneryRouteSettings
{
    public bool enabled = true;
    [Min(0f), Tooltip("Multiplies the strength of every bend. Zero removes bends. Wide terrain may limit the effective strength to prevent folding.")]
    public float turnStrength = 1f;
    [Range(0f, 85f), Tooltip("Maximum heading variation before the turn strength multiplier, in degrees.")]
    public float maximumHeading = 35f;
    [Min(40f), Tooltip("Length of the main left/right bend cycle, in metres. Longer cycles make gentler bends.")]
    public float turnWavelength = 900f;
    [Min(0f), Tooltip("Height of the route's hills in metres. This moves the whole railway, independently of the surrounding terrain hills.")]
    public float hillHeight = 8f;
    [Min(40f), Tooltip("Length of the main uphill/downhill cycle, in metres.")]
    public float hillWavelength = 700f;
    [Min(0f), Tooltip("Multiplies the height of the route's hills. Zero keeps the railway level.")]
    public float gradeStrength = 1f;
}

/// <summary>
/// A reproducible, infinite railway centreline. Distance is travelled metres, +Z is forward,
/// positive heading turns right and positive grade climbs. No train transforms are moved.
/// The settings object is retained, so changes take effect on the next sample.
/// </summary>
public sealed class SceneryRoute
{
    private const double TwoPi = Math.PI * 2.0;
    private const double DegreesToRadians = Math.PI / 180.0;
    private const double RadiansToDegrees = 180.0 / Math.PI;
    private const double GridSpacing = 4.0;
    private const long MaximumGridOffset = 1024;
    private const int MaximumRelativeSamples = 4096;

    private readonly SceneryRouteSettings settings;
    private readonly double minimumTurnRadius;
    private readonly double headingPhase, headingDetailPhase, hillPhase, hillDetailPhase;
    private readonly Dictionary<long, DoublePoint> grid = new Dictionary<long, DoublePoint>(512);
    private readonly Dictionary<double, RelativeSample> relativeSamples = new Dictionary<double, RelativeSample>(512);
    private Configuration configuration;
    private bool configurationReady, gridReady, originReady;
    private long anchorCell, firstCell, lastCell;
    private double currentOrigin;
    private DoublePoint originPoint;
    private Quaternion inverseOriginRotation;

    public SceneryRoute(SceneryRouteSettings settings, int seed, float minimumTurnRadius = 0f)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.minimumTurnRadius = Math.Max(0.0, minimumTurnRadius);
        var random = new System.Random(unchecked(seed ^ 0x5f3759df));
        headingPhase = random.NextDouble() * TwoPi;
        headingDetailPhase = random.NextDouble() * TwoPi;
        hillPhase = random.NextDouble() * TwoPi;
        hillDetailPhase = random.NextDouble() * TwoPi;
    }

    /// <summary>Actual bend multiplier after the optional terrain-width safety limit.</summary>
    public float EffectiveTurnStrength
    {
        get
        {
            UpdateConfiguration();
            double baseAmplitude = Math.Max(0.0, settings.maximumHeading) * DegreesToRadians;
            return configuration.enabled && baseAmplitude > 0.0 ? (float)(configuration.headingAmplitude / baseAmplitude) : 0f;
        }
    }

    public Quaternion Rotation(double distance)
    {
        UpdateConfiguration();
        return EvaluateRotation(distance);
    }

    public float Heading(double distance)
    {
        UpdateConfiguration();
        return configuration.enabled ? (float)(HeadingRadians(distance) * RadiansToDegrees) : 0f;
    }

    public float Grade(double distance)
    {
        UpdateConfiguration();
        return configuration.enabled ? (float)(Math.Asin(HeightDerivative(distance)) * RadiansToDegrees) : 0f;
    }

    /// <summary>Absolute analytic railway elevation, useful for keeping water surfaces level.</summary>
    public double Elevation(double distance)
    {
        UpdateConfiguration();
        return configuration.enabled ? HeightAt(distance) : 0.0;
    }

    /// <summary>Signed change in heading, in degrees per travelled metre.</summary>
    public float Curvature(double distance)
    {
        UpdateConfiguration();
        if (!configuration.enabled) return 0f;
        double phase = Phase(distance, configuration.headingFrequency, headingPhase);
        double detail = Phase(distance, configuration.headingFrequency * 2.0, headingDetailPhase);
        return (float)(configuration.headingAmplitude * configuration.headingFrequency *
            (0.76 * Math.Cos(phase) + 0.48 * Math.Cos(detail)) * RadiansToDegrees);
    }

    /// <summary>A centreline point in the frame of a stationary train at originDistance.</summary>
    public Vector3 PositionRelative(double distance, double originDistance)
    {
        return SampleRelative(distance, originDistance).position;
    }

    /// <summary>
    /// Positions terrain and objects in the frame of a stationary train. Lateral and height
    /// are measured in the railway's right/up directions at the target distance.
    /// </summary>
    public Vector3 PointRelative(float lateral, float height, double distance, double originDistance)
    {
        RelativeSample sample = SampleRelative(distance, originDistance);
        return sample.position + sample.rotation * new Vector3(lateral, height, 0f);
    }

    private RelativeSample SampleRelative(double distance, double originDistance)
    {
        UpdateConfiguration();
        if (!configuration.enabled)
            return new RelativeSample(new Vector3(0f, 0f, (float)(distance - originDistance)), Quaternion.identity);

        PrepareOrigin(originDistance);
        if (relativeSamples.TryGetValue(distance, out RelativeSample cached)) return cached;

        DoublePoint point;
        long cell = CellAt(distance);
        if (Math.Abs((double)cell - anchorCell) <= MaximumGridOffset)
            point = EvaluatePoint(distance);
        else
            point = originPoint + Integrate(originDistance, distance);

        Vector3 offset = new Vector3((float)(point.x - originPoint.x), (float)(point.y - originPoint.y), (float)(point.z - originPoint.z));
        var sample = new RelativeSample(inverseOriginRotation * offset, inverseOriginRotation * EvaluateRotation(distance));
        if (relativeSamples.Count >= MaximumRelativeSamples) relativeSamples.Clear();
        relativeSamples[distance] = sample;
        return sample;
    }

    private void PrepareOrigin(double originDistance)
    {
        long originCell = CellAt(originDistance);
        // Keep the numerical anchor near the train; retained storage never grows with journey length.
        if (!gridReady || Math.Abs((double)originCell - anchorCell) > MaximumGridOffset / 2)
        {
            grid.Clear();
            anchorCell = firstCell = lastCell = originCell;
            grid[anchorCell] = new DoublePoint(0.0, HeightAt(anchorCell * GridSpacing), 0.0);
            gridReady = true;
            originReady = false;
        }
        if (originReady && currentOrigin == originDistance) return;
        currentOrigin = originDistance;
        originPoint = EvaluatePoint(originDistance);
        inverseOriginRotation = Quaternion.Inverse(EvaluateRotation(originDistance));
        relativeSamples.Clear();
        originReady = true;
    }

    private DoublePoint EvaluatePoint(double distance)
    {
        long cell = CellAt(distance);
        double cellDistance = cell * GridSpacing;
        DoublePoint start = GridPoint(cell);
        return start + Integrate(cellDistance, distance);
    }

    private DoublePoint GridPoint(long cell)
    {
        if (grid.TryGetValue(cell, out DoublePoint point)) return point;
        while (cell > lastCell)
        {
            DoublePoint previous = grid[lastCell];
            double start = lastCell * GridSpacing;
            ++lastCell;
            grid[lastCell] = previous + Integrate(start, lastCell * GridSpacing);
        }
        while (cell < firstCell)
        {
            DoublePoint previous = grid[firstCell];
            double start = firstCell * GridSpacing;
            --firstCell;
            grid[firstCell] = previous + Integrate(start, firstCell * GridSpacing);
        }
        return grid[cell];
    }

    private DoublePoint Integrate(double start, double end)
    {
        double span = end - start;
        if (span == 0.0) return default;
        int steps = Math.Max(1, (int)Math.Ceiling(Math.Abs(span) / GridSpacing));
        double step = span / steps;
        double x = 0.0, z = 0.0;
        for (int i = 0; i < steps; ++i)
        {
            double from = start + step * i;
            DoublePoint a = HorizontalTangent(from);
            DoublePoint b = HorizontalTangent(from + step * 0.5);
            DoublePoint c = HorizontalTangent(from + step);
            x += (a.x + 4.0 * b.x + c.x) * step / 6.0;
            z += (a.z + 4.0 * b.z + c.z) * step / 6.0;
        }
        return new DoublePoint(x, HeightAt(end) - HeightAt(start), z);
    }

    private DoublePoint HorizontalTangent(double distance)
    {
        double heading = HeadingRadians(distance);
        double slope = HeightDerivative(distance);
        double horizontalSpeed = Math.Sqrt(Math.Max(0.0, 1.0 - slope * slope));
        return new DoublePoint(Math.Sin(heading) * horizontalSpeed, 0.0, Math.Cos(heading) * horizontalSpeed);
    }

    private Quaternion EvaluateRotation(double distance)
    {
        if (!configuration.enabled) return Quaternion.identity;
        float heading = (float)(HeadingRadians(distance) * RadiansToDegrees);
        float grade = (float)(Math.Asin(HeightDerivative(distance)) * RadiansToDegrees);
        return Quaternion.Euler(-grade, heading, 0f);
    }

    private double HeadingRadians(double distance)
    {
        return configuration.headingAmplitude *
            (0.76 * Math.Sin(Phase(distance, configuration.headingFrequency, headingPhase)) +
             0.24 * Math.Sin(Phase(distance, configuration.headingFrequency * 2.0, headingDetailPhase)));
    }

    private double HeightAt(double distance)
    {
        return configuration.heightAmplitude *
            (0.78 * Math.Sin(Phase(distance, configuration.heightFrequency, hillPhase)) +
             0.22 * Math.Sin(Phase(distance, configuration.heightFrequency * 2.0, hillDetailPhase)));
    }

    private double HeightDerivative(double distance)
    {
        return configuration.heightAmplitude * configuration.heightFrequency *
            (0.78 * Math.Cos(Phase(distance, configuration.heightFrequency, hillPhase)) +
             0.44 * Math.Cos(Phase(distance, configuration.heightFrequency * 2.0, hillDetailPhase)));
    }

    private void UpdateConfiguration()
    {
        var next = new Configuration
        {
            enabled = settings.enabled,
            headingAmplitude = Math.Max(0.0, settings.maximumHeading) * Math.Max(0.0, settings.turnStrength) * DegreesToRadians,
            headingFrequency = TwoPi / Math.Max(40.0, settings.turnWavelength),
            heightFrequency = TwoPi / Math.Max(40.0, settings.hillWavelength)
        };
        // The analytic height derivative is sin(grade). Bound its amplitude so the
        // route remains a valid arc-length parameterization even with extreme settings.
        next.heightAmplitude = Math.Min(Math.Max(0.0, settings.hillHeight) * Math.Max(0.0, settings.gradeStrength),
            0.65 / (1.22 * next.heightFrequency));
        if (minimumTurnRadius > 0.0)
        {
            double maximumSlope = next.heightAmplitude * next.heightFrequency * 1.22;
            double minimumHorizontalSpeed = Math.Sqrt(1.0 - maximumSlope * maximumSlope);
            next.headingAmplitude = Math.Min(next.headingAmplitude,
                minimumHorizontalSpeed / (minimumTurnRadius * next.headingFrequency * 1.24));
        }
        if (configurationReady && next.Equals(configuration)) return;
        configuration = next;
        configurationReady = true;
        gridReady = originReady = false;
        grid.Clear();
        relativeSamples.Clear();
    }

    private static double Phase(double distance, double frequency, double phase)
    {
        return Math.IEEERemainder(distance * frequency + phase, TwoPi);
    }

    private static long CellAt(double distance) => (long)Math.Floor(distance / GridSpacing);

    private struct Configuration : IEquatable<Configuration>
    {
        public bool enabled;
        public double headingAmplitude, headingFrequency, heightAmplitude, heightFrequency;

        public bool Equals(Configuration other) => enabled == other.enabled &&
            headingAmplitude == other.headingAmplitude && headingFrequency == other.headingFrequency &&
            heightAmplitude == other.heightAmplitude && heightFrequency == other.heightFrequency;
    }

    private readonly struct DoublePoint
    {
        public readonly double x, y, z;
        public DoublePoint(double x, double y, double z) { this.x = x; this.y = y; this.z = z; }
        public static DoublePoint operator +(DoublePoint a, DoublePoint b) => new DoublePoint(a.x + b.x, a.y + b.y, a.z + b.z);
    }

    private readonly struct RelativeSample
    {
        public readonly Vector3 position;
        public readonly Quaternion rotation;
        public RelativeSample(Vector3 position, Quaternion rotation) { this.position = position; this.rotation = rotation; }
    }
}
