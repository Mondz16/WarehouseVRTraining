using System;

namespace ImmersiveTraining.TrainingInteractions
{
    // Plain C# (no UnityEngine) rules behind the Packing and Dispatch module (M2), kept separate so they can be
    // unit-tested outside Unity: dotnet run --project Tests/PackingDispatchLogic

    public enum CartonSize { Small = 0, Medium = 1, Large = 2 }

    public enum CartonVerdict { Correct, TooSmall, Oversized }

    /// <summary>Three sides of a box (centimetres for the fit rule).</summary>
    public readonly struct Size3
    {
        public readonly float X, Y, Z;

        public Size3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>The sides, longest first (orientation does not matter for fitting).</summary>
        public float[] SortedDescending()
        {
            float[] sides = { X, Y, Z };
            Array.Sort(sides);
            Array.Reverse(sides);
            return sides;
        }

        public override string ToString() => $"{X:0.#} x {Y:0.#} x {Z:0.#}";
    }

    /// <summary>
    /// The minimum-fit rule (M2_03 / M2_08): use the smallest carton whose usable size - its inner size less the
    /// void-fill clearance on every side - holds the packed order in any orientation.
    /// </summary>
    public static class CartonFit
    {
        public const float DefaultClearanceCm = 5f;

        private static readonly CartonSize[] SmallestFirst = { CartonSize.Small, CartonSize.Medium, CartonSize.Large };

        /// <summary>Inner size in centimetres: length (X), height (Y), width (Z).</summary>
        public static Size3 InnerSize(CartonSize size)
        {
            switch (size)
            {
                case CartonSize.Small: return new Size3(30f, 15f, 20f);
                case CartonSize.Medium: return new Size3(45f, 25f, 35f);
                default: return new Size3(60f, 40f, 50f);
            }
        }

        /// <summary>The inner size less the clearance on both sides of every axis.</summary>
        public static Size3 UsableSize(CartonSize size, float clearanceCm = DefaultClearanceCm)
        {
            Size3 inner = InnerSize(size);
            float both = 2f * clearanceCm;
            return new Size3(Math.Max(0f, inner.X - both), Math.Max(0f, inner.Y - both), Math.Max(0f, inner.Z - both));
        }

        public static bool Fits(Size3 packed, CartonSize size, float clearanceCm = DefaultClearanceCm)
        {
            float[] need = packed.SortedDescending();
            float[] room = UsableSize(size, clearanceCm).SortedDescending();
            for (int i = 0; i < 3; i++)
                if (!(need[i] <= room[i])) return false; // NaN never fits
            return true;
        }

        /// <summary>The smallest carton that fits, or null when even the largest is too small.</summary>
        public static CartonSize? SmallestFit(Size3 packed, float clearanceCm = DefaultClearanceCm)
        {
            foreach (CartonSize size in SmallestFirst)
                if (Fits(packed, size, clearanceCm)) return size;
            return null;
        }

        public static CartonVerdict Judge(CartonSize chosen, Size3 packed, float clearanceCm = DefaultClearanceCm)
        {
            CartonSize? best = SmallestFit(packed, clearanceCm);
            if (best == null || chosen < best.Value) return CartonVerdict.TooSmall;
            return chosen == best.Value ? CartonVerdict.Correct : CartonVerdict.Oversized;
        }
    }

    public enum FillPhase { Base, Items, Top, Full }

    public enum FillResult { Added, PlaceItemsFirst, AlreadyFull }

    public enum ItemResult { Added, NeedsBaseFill, NoRoom }

    /// <summary>
    /// Void fill inside one carton (M2_04 / M2_08 / M2_09): a base layer first, then the item(s), then fill over and
    /// around them until nothing can move. Counts are in pillows.
    /// </summary>
    public sealed class VoidFillState
    {
        public VoidFillState(int baseRequired, int topRequired, int itemsRequired)
        {
            BaseRequired = Math.Max(0, baseRequired);
            TopRequired = Math.Max(0, topRequired);
            ItemsRequired = Math.Max(0, itemsRequired);
        }

        public int BaseRequired { get; }
        public int TopRequired { get; }
        public int ItemsRequired { get; }
        public int Fill { get; private set; }
        public int ItemsPlaced { get; private set; }

        public int FullLevel => BaseRequired + TopRequired;
        public bool AllItemsPlaced => ItemsPlaced >= ItemsRequired;
        public bool IsBaseMet => Fill >= BaseRequired;
        public bool IsFull => AllItemsPlaced && Fill >= FullLevel;

        public FillPhase Phase
        {
            get
            {
                if (!AllItemsPlaced) return IsBaseMet ? FillPhase.Items : FillPhase.Base;
                return Fill < FullLevel ? FillPhase.Top : FillPhase.Full;
            }
        }

        public FillResult TryAddFill()
        {
            switch (Phase)
            {
                case FillPhase.Items: return FillResult.PlaceItemsFirst;
                case FillPhase.Full: return FillResult.AlreadyFull;
                default:
                    Fill++;
                    return FillResult.Added;
            }
        }

        public ItemResult TryAddItem()
        {
            if (AllItemsPlaced) return ItemResult.NoRoom;
            if (!IsBaseMet) return ItemResult.NeedsBaseFill;
            ItemsPlaced++;
            return ItemResult.Added;
        }

        /// <summary>Puts the box straight into a given state (the pre-packed QC box).</summary>
        public void Set(int fill, int itemsPlaced)
        {
            Fill = Math.Max(0, Math.Min(fill, FullLevel));
            ItemsPlaced = Math.Max(0, Math.Min(itemsPlaced, ItemsRequired));
        }

        public void Reset() => Set(0, 0);

        /// <summary>
        /// Height of the fill as a share of the inner height: the base layer grows to <paramref name="baseShare"/>,
        /// the top fill from there to <paramref name="fullShare"/>.
        /// </summary>
        public float FillHeight(float baseShare, float fullShare)
        {
            if (FullLevel == 0) return fullShare;
            if (Fill <= BaseRequired) return BaseRequired == 0 ? 0f : baseShare * Fill / BaseRequired;
            float top = Math.Min(1f, (float)(Fill - BaseRequired) / Math.Max(1, TopRequired));
            return baseShare + (fullShare - baseShare) * top;
        }
    }

    /// <summary>How much of a seam a tool has swept (tape gun M2_05 / M2_09, box cutter M2_09).</summary>
    public sealed class SeamCoverage
    {
        private readonly bool[] _bins;

        public SeamCoverage(int bins = 12, float completeFraction = 0.85f)
        {
            _bins = new bool[Math.Max(1, bins)];
            CompleteFraction = completeFraction;
        }

        public float CompleteFraction { get; }

        public float Fraction
        {
            get
            {
                int covered = 0;
                foreach (bool bin in _bins)
                    if (bin) covered++;
                return (float)covered / _bins.Length;
            }
        }

        public bool IsComplete => Fraction >= CompleteFraction;

        /// <summary>
        /// Marks the bin at <paramref name="t"/> (0 = seam start, 1 = seam end). Values outside 0-1 are ignored.
        /// Returns true if that bin was not covered yet.
        /// </summary>
        public bool Mark(float t)
        {
            if (float.IsNaN(t) || t < 0f || t > 1f) return false;
            int index = Math.Min(_bins.Length - 1, (int)(t * _bins.Length));
            if (_bins[index]) return false;
            _bins[index] = true;
            return true;
        }

        public void Reset() => Array.Clear(_bins, 0, _bins.Length);
    }

    public readonly struct Vec3
    {
        public readonly float X, Y, Z;

        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator *(Vec3 a, float s) => new Vec3(a.X * s, a.Y * s, a.Z * s);

        public float Dot(Vec3 other) => X * other.X + Y * other.Y + Z * other.Z;
        public float Length => (float)Math.Sqrt(Dot(this));
    }

    public static class SegmentMath
    {
        /// <summary>
        /// Parameter of the point on line ab closest to <paramref name="p"/> (0 = a, 1 = b, not clamped) and
        /// <paramref name="p"/>'s distance to the segment itself (clamped to its ends).
        /// </summary>
        public static float Closest(Vec3 p, Vec3 a, Vec3 b, out float distance)
        {
            Vec3 ab = b - a;
            float lengthSq = ab.Dot(ab);
            float t = lengthSq <= 1e-12f ? 0f : (p - a).Dot(ab) / lengthSq;
            float clamped = Math.Max(0f, Math.Min(1f, t));
            distance = (p - (a + ab * clamped)).Length;
            return t;
        }
    }

    /// <summary>"Look at it for a moment" (M2_02): the gaze must stay within an angle and distance for a dwell time.</summary>
    public sealed class GazeDwell
    {
        private float _timer;

        public GazeDwell(float maxAngleDeg = 12f, float maxDistance = 4.5f, float dwellSeconds = 0.5f)
        {
            MaxAngleDeg = maxAngleDeg;
            MaxDistance = maxDistance;
            DwellSeconds = dwellSeconds;
        }

        public float MaxAngleDeg { get; }
        public float MaxDistance { get; }
        public float DwellSeconds { get; }
        public bool Found { get; private set; }

        /// <summary>Advances the dwell; returns true on the update that finds the target.</summary>
        public bool Update(float angleDeg, float distance, float deltaTime)
        {
            if (Found) return false;
            bool looking = angleDeg <= MaxAngleDeg && distance <= MaxDistance; // false for NaN
            _timer = looking ? _timer + Math.Max(0f, deltaTime) : 0f;
            if (_timer < DwellSeconds) return false;
            Found = true;
            return true;
        }

        public void Reset()
        {
            Found = false;
            _timer = 0f;
        }

        /// <summary>Angle in degrees between two directions of any length; NaN if either is zero.</summary>
        public static float AngleBetween(Vec3 a, Vec3 b)
        {
            float la = a.Length, lb = b.Length;
            if (la < 1e-6f || lb < 1e-6f) return float.NaN;
            double cos = Math.Max(-1.0, Math.Min(1.0, a.Dot(b) / (la * lb)));
            return (float)(Math.Acos(cos) * 180.0 / Math.PI);
        }
    }

    public static class Orientation
    {
        /// <summary>
        /// True if a face whose unit normal has world-up component <paramref name="normalUp"/> points up within
        /// <paramref name="maxTiltDeg"/> (M2_07: the label must face up).
        /// </summary>
        public static bool IsFaceUp(float normalUp, float maxTiltDeg)
        {
            if (float.IsNaN(normalUp)) return false;
            return normalUp >= Math.Cos(maxTiltDeg * Math.PI / 180.0) - 1e-6;
        }
    }

    /// <summary>An axis-aligned box in some local space (carton interior, packing spot, conveyor load zone).</summary>
    public readonly struct BoxRegion
    {
        public readonly Vec3 Center, HalfExtents;

        public BoxRegion(Vec3 center, Vec3 halfExtents)
        {
            Center = center;
            HalfExtents = halfExtents;
        }

        public bool Contains(Vec3 p, float tolerance = 0f) =>
            Math.Abs(p.X - Center.X) <= HalfExtents.X + tolerance &&
            Math.Abs(p.Y - Center.Y) <= HalfExtents.Y + tolerance &&
            Math.Abs(p.Z - Center.Z) <= HalfExtents.Z + tolerance;
    }
}
