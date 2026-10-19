using System;

namespace ImmersiveTraining.TrainingInteractions
{
    // Plain C# (no UnityEngine) steering and decision logic behind the pushable cart (PushCart,
    // CartCargo, CartDropOffZone), kept separate so it can be unit-tested outside Unity:
    // dotnet run --project Tests/OrderPickingLogic
    //
    // Everything lives on the floor plane: X/Z are world metres, yaw is degrees about +Y in Unity's
    // convention (yaw 0 faces +Z, yaw 90 faces +X).

    /// <summary>A point or direction on the floor plane.</summary>
    public readonly struct Planar
    {
        public readonly float X;
        public readonly float Z;

        public Planar(float x, float z)
        {
            X = x;
            Z = z;
        }

        public float Length => (float)Math.Sqrt((double)X * X + (double)Z * Z);

        /// <summary>Heading of this direction in degrees: +Z is 0, +X is 90.</summary>
        public float Yaw => (float)(Math.Atan2(X, Z) * 180.0 / Math.PI);

        /// <summary>This vector turned by <paramref name="yaw"/> degrees about +Y (like Quaternion.Euler(0, yaw, 0)).</summary>
        public Planar Rotated(float yaw)
        {
            double radians = yaw * Math.PI / 180.0;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            return new Planar((float)(X * cos + Z * sin), (float)(-X * sin + Z * cos));
        }

        public static Planar Midpoint(Planar a, Planar b) => new Planar((a.X + b.X) * 0.5f, (a.Z + b.Z) * 0.5f);

        public static Planar operator +(Planar a, Planar b) => new Planar(a.X + b.X, a.Z + b.Z);
        public static Planar operator -(Planar a, Planar b) => new Planar(a.X - b.X, a.Z - b.Z);
        public static Planar operator *(Planar a, float scale) => new Planar(a.X * scale, a.Z * scale);

        public override string ToString() => $"({X:F3}, {Z:F3})";
    }

    /// <summary>Angle helpers, in degrees.</summary>
    public static class Angles
    {
        /// <summary>Signed turn from <paramref name="from"/> to <paramref name="to"/> the short way round, in (-180, 180].</summary>
        public static float Delta(float from, float to)
        {
            float delta = (to - from) % 360f;
            if (delta > 180f) delta -= 360f;
            else if (delta <= -180f) delta += 360f;
            return delta;
        }

        /// <summary><paramref name="angle"/> wrapped into (-180, 180].</summary>
        public static float Wrap(float angle) => Delta(0f, angle);
    }

    /// <summary>Where the cart stands on the floor and which way it faces.</summary>
    public readonly struct CartPose
    {
        public readonly Planar Position;
        public readonly float Yaw;

        public CartPose(Planar position, float yaw)
        {
            Position = position;
            Yaw = yaw;
        }

        /// <summary>World floor point of a point given in the cart's own floor coordinates.</summary>
        public Planar ToWorld(Planar local) => Position + local.Rotated(Yaw);

        /// <summary>The cart's own floor coordinates of a world floor point.</summary>
        public Planar ToLocal(Planar world) => (world - Position).Rotated(-Yaw);

        /// <summary>False if any component is NaN or infinite (e.g. from a bad tracking sample); never move there.</summary>
        public bool IsFinite => float.IsFinite(Position.X) && float.IsFinite(Position.Z) && float.IsFinite(Yaw);

        public override string ToString() => $"{Position} yaw {Yaw:F1}";
    }

    /// <summary>
    /// Works out where the cart should be from the hands holding it. The grip is taken again from the
    /// cart's current pose every time the set of holding hands changes, so grabbing or letting go never
    /// makes the cart jump.
    ///
    /// One hand: the held point follows the hand, and the cart turns as much as the direction from the
    /// head to the hand turns (turning your body or swinging the hand sideways steers; looking around
    /// does not). Two hands: the cart follows the hands' midpoint and turns with the line between them,
    /// like a handlebar.
    /// </summary>
    public sealed class CartSteering
    {
        /// <summary>Head-to-hand distance (m) below which the one-hand heading is held.</summary>
        public const float MinReach = 0.2f;

        /// <summary>Hand spacing (m) below which two hands stop turning the cart.</summary>
        public const float MinSpan = 0.1f;

        private Planar _anchorA;
        private Planar _anchorB;
        private float _gripYaw;
        private float _reachYaw;
        private bool _hasReach;
        private float _heading;

        /// <summary>Takes a one-hand grip at the cart's current pose.</summary>
        public void GripOne(CartPose cart, Planar hand, Planar head)
        {
            _anchorA = cart.ToLocal(hand);
            _gripYaw = cart.Yaw;
            _heading = cart.Yaw;

            Planar reach = hand - head;
            _hasReach = reach.Length >= MinReach;
            if (_hasReach) _reachYaw = reach.Yaw;
        }

        /// <summary>Takes a two-hand grip at the cart's current pose.</summary>
        public void GripTwo(CartPose cart, Planar handA, Planar handB)
        {
            _anchorA = cart.ToLocal(handA);
            _anchorB = cart.ToLocal(handB);
            _heading = cart.Yaw;
        }

        /// <summary>Pose the cart should take for a one-hand grip.</summary>
        public CartPose TargetOne(Planar hand, Planar head)
        {
            Planar reach = hand - head;
            if (reach.Length >= MinReach)
            {
                if (!_hasReach)
                {
                    // The reach (just) became usable: steer relative to this moment, so a hand that passed close
                    // to the head and came out on another side doesn't swing the cart round.
                    _reachYaw = reach.Yaw;
                    _gripYaw = _heading;
                    _hasReach = true;
                }

                _heading = Angles.Wrap(_gripYaw + Angles.Delta(_reachYaw, reach.Yaw));
            }
            else
            {
                _hasReach = false;   // too close to read a direction from: hold the heading until it's usable again
            }

            return new CartPose(hand - _anchorA.Rotated(_heading), _heading);
        }

        /// <summary>Pose the cart should take for a two-hand grip.</summary>
        public CartPose TargetTwo(Planar handA, Planar handB)
        {
            Planar span = handB - handA;
            Planar anchorSpan = _anchorB - _anchorA;
            if (span.Length >= MinSpan && anchorSpan.Length >= MinSpan)
                _heading = Angles.Wrap(span.Yaw - anchorSpan.Yaw);

            Planar anchorMid = Planar.Midpoint(_anchorA, _anchorB);
            return new CartPose(Planar.Midpoint(handA, handB) - anchorMid.Rotated(_heading), _heading);
        }

        /// <summary>
        /// Moves <paramref name="current"/> toward <paramref name="target"/> by at most
        /// <paramref name="maxMove"/> metres and <paramref name="maxTurn"/> degrees (the short way round).
        /// </summary>
        public static CartPose Step(CartPose current, CartPose target, float maxMove, float maxTurn)
        {
            Planar move = target.Position - current.Position;
            float distance = move.Length;
            if (distance > maxMove && distance > 0f) move = move * (maxMove / distance);

            float turn = Angles.Delta(current.Yaw, target.Yaw);
            if (turn > maxTurn) turn = maxTurn;
            else if (turn < -maxTurn) turn = -maxTurn;

            return new CartPose(current.Position + move, Angles.Wrap(current.Yaw + turn));
        }

        /// <summary>
        /// Poses to try, in order, when the move from <paramref name="current"/> to <paramref name="next"/>
        /// may be blocked: the full move, the move without turning, then sliding along X only and Z only.
        /// <paramref name="into"/> must hold at least 4 poses.
        /// </summary>
        public static void BlockedFallbacks(CartPose current, CartPose next, CartPose[] into)
        {
            into[0] = next;
            into[1] = new CartPose(next.Position, current.Yaw);
            into[2] = new CartPose(new Planar(next.Position.X, current.Position.Z), current.Yaw);
            into[3] = new CartPose(new Planar(current.Position.X, next.Position.Z), current.Yaw);
        }
    }

    /// <summary>
    /// Decides when an item resting in the cart should lock to it: it must be free (not held) and still
    /// relative to the cart for <see cref="SettleTime"/> seconds.
    /// </summary>
    public sealed class SettleTimer
    {
        // Absorbs float error so that, e.g., 15 ticks of 0.02 s count as 0.3 s.
        private const float Epsilon = 1e-4f;

        private float _rested;

        public float SettleTime { get; set; } = 0.3f;

        /// <summary>Largest movement (m) per tick, relative to the cart, that still counts as resting.</summary>
        public float MoveTolerance { get; set; } = 0.003f;

        /// <summary>Largest rotation (degrees) per tick, relative to the cart, that still counts as resting.</summary>
        public float TurnTolerance { get; set; } = 1f;

        /// <summary>Feeds one physics tick; returns true once the item has rested long enough.</summary>
        public bool Tick(bool held, float moved, float turned, float deltaTime)
        {
            // Written as !(x <= tolerance) so NaN motion (a physics blow-up) never counts as resting.
            if (held || !(moved <= MoveTolerance) || !(turned <= TurnTolerance))
            {
                _rested = 0f;
                return false;
            }

            _rested += deltaTime;
            return _rested >= SettleTime - Epsilon;
        }

        public void Reset() => _rested = 0f;
    }

    /// <summary>A rectangle on the floor (the cart drop-off).</summary>
    public static class DropOffArea
    {
        /// <summary>
        /// True if <paramref name="point"/> lies inside the rectangle centred on <paramref name="centre"/>,
        /// turned by <paramref name="yaw"/> degrees, with half-sizes <paramref name="halfSize"/> along its own X and Z.
        /// </summary>
        public static bool Contains(Planar centre, float yaw, Planar halfSize, Planar point)
        {
            Planar local = new CartPose(centre, yaw).ToLocal(point);
            return Math.Abs(local.X) <= halfSize.X && Math.Abs(local.Z) <= halfSize.Z;
        }
    }
}
