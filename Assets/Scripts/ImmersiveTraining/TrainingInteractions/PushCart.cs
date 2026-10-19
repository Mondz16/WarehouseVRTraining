using System.Collections.Generic;
using Autohand;
using UnityEngine;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// Makes the order-picking cart pushable. While hands hold its handles (an AutoHand
    /// <see cref="Grabbable"/> on a child kinematic body) the cart follows them on the floor: the held
    /// point stays with the controller, one hand steers with the head-to-hand direction and two hands
    /// steer like a handlebar (<see cref="CartSteering"/>). Speed and turn rate are capped, and every move
    /// is checked against static geometry (racks, walls, pallets): a blocked cart slides along the
    /// obstacle or stops, and the stretched hand lets go as with a real snag. Loose physics objects are
    /// pushed aside.
    ///
    /// The cart body is kinematic (MovePosition/MoveRotation), so it never tips, drifts or jitters and
    /// hands can't shove it while it is parked. While it is held it ignores the player's body and head:
    /// the player rig follows its physics body, so a cart shoving the player would drag the controllers,
    /// and with them the cart, after it. Items that settle in it are held by <see cref="CartCargo"/>.
    /// Put this on an upright root at floor level; the model, handles and tote are its children.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PushCart : MonoBehaviour
    {
        [Tooltip("AutoHand Grabbable on the Handles child (kinematic Rigidbody + the handle colliders).")]
        [SerializeField] private Grabbable _handles;

        [Tooltip("The player's head camera. One hand steers with the direction from the head to the hand.")]
        [SerializeField] private Transform _head;

        [Tooltip("The cart model's MeshFilter. Its bounds size the box used to check for obstacles.")]
        [SerializeField] private MeshFilter _footprint;

        [Tooltip("The player rig; the cart ignores its body and head colliders while held. Found automatically if empty.")]
        [SerializeField] private AutoHandPlayer _player;

        [Header("Movement")]
        [Tooltip("Fastest the cart moves, in m/s (the player walks at up to 2.3 m/s).")]
        [SerializeField, Min(0f)] private float _maxSpeed = 2.5f;

        [Tooltip("Fastest the cart turns, in degrees per second.")]
        [SerializeField, Min(0f)] private float _maxTurnSpeed = 180f;

        [Tooltip("How far behind the head camera the steering reference sits (m). The camera is ahead of the neck, so " +
                 "turning the head swings it sideways; steering from the neck keeps looking around from steering.")]
        [SerializeField, Min(0f)] private float _neckOffset = 0.1f;

        [Header("Obstacles")]
        [Tooltip("Layers checked for obstacles. Leave the hand, grabbable and UI layers out: they never block and would " +
                 "only fill the query buffer. Only static or kinematic colliders outside the cart and the hands block it.")]
        [SerializeField] private LayerMask _blockingMask = ~0;

        [Tooltip("Height above the cart's base where the obstacle check starts, so the floor and low floor plates (e.g. the 6 cm B pick-area plate) never block it.")]
        [SerializeField, Min(0f)] private float _floorClearance = 0.08f;

        [Header("Editor testing")]
        [Tooltip("Editor only: while no hand holds the cart, drives it at this world X/Z velocity (m/s) through the same caps and obstacle checks as a push. Leave at zero.")]
        [SerializeField] private Vector2 _testDriveVelocity;

        [Tooltip("Editor only: while no hand holds the cart, turns it at this rate (degrees per second) through the same checks. Leave at zero.")]
        [SerializeField] private float _testDriveTurnSpeed;

        private const float Skin = 0.01f;

        private readonly List<Hand> _hands = new List<Hand>(2);
        private readonly List<Hand> _holding = new List<Hand>(2);
        private readonly CartSteering _steering = new CartSteering();
        private readonly HashSet<Collider> _ignored = new HashSet<Collider>();
        private readonly List<Collider> _cleared = new List<Collider>();
        private readonly List<Collider> _cartColliders = new List<Collider>();
        private readonly List<Collider> _playerColliders = new List<Collider>(2);
        private readonly Collider[] _overlaps = new Collider[64];
        private readonly CartPose[] _tries = new CartPose[4];

        private Rigidbody _body;
        private Rigidbody _handlesBody;
        private Vector3 _handlesLocalPosition;
        private Quaternion _handlesLocalRotation = Quaternion.identity;
        private Vector3 _boxCenter;
        private Vector3 _boxHalfExtents;
        private bool _wasTestDriving;
        private bool _ignoringPlayer;
        private bool _warnedFullBuffer;

        /// <summary>True while at least one hand holds the handles.</summary>
        public bool IsHeld => _hands.Count > 0;

        // Default for a newly added component: leave out the layers that never block (UI, AutoHand's hand and
        // grabbable layers, the player).
        private void Reset() =>
            _blockingMask = ~LayerMask.GetMask("UI", "Grabbing", "Grabbable", "Hand", "HandPlayer");

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.useGravity = false;
            _body.interpolation = RigidbodyInterpolation.Interpolate;

            if (_handles != null)
            {
                _handlesBody = _handles.GetComponent<Rigidbody>();
                _handlesLocalPosition = transform.InverseTransformPoint(_handles.transform.position);
                _handlesLocalRotation = Quaternion.Inverse(transform.rotation) * _handles.transform.rotation;
            }

            // The cart body's own solid colliders (model, side walls) — the handles have their own body and the
            // tote collider is a trigger.
            foreach (Collider col in GetComponentsInChildren<Collider>(true))
                if (!col.isTrigger && col.attachedRigidbody == _body)
                    _cartColliders.Add(col);

            CacheObstacleBox();
        }

        private void OnDisable()
        {
            _hands.Clear();
            _wasTestDriving = false;
            IgnorePlayer(false);
        }

        private void FixedUpdate()
        {
            SyncHands();

            // Parked again: become solid for the player once they no longer overlap (no pop-out).
            if (_ignoringPlayer && _hands.Count == 0 && !OverlapsPlayer()) IgnorePlayer(false);

            bool testDriving = Application.isEditor && _hands.Count == 0 &&
                               (_testDriveVelocity != Vector2.zero || _testDriveTurnSpeed != 0f);
            if (testDriving && !_wasTestDriving) CaptureIgnored();
            _wasTestDriving = testDriving;

            float dt = Time.fixedDeltaTime;
            CartPose current = CurrentPose();
            CartPose target;
            if (_hands.Count == 2)
                target = _steering.TargetTwo(HandPoint(_hands[0]), HandPoint(_hands[1]));
            else if (_hands.Count == 1)
                target = _steering.TargetOne(HandPoint(_hands[0]), HeadPoint(_hands[0]));
            else if (testDriving)
                target = new CartPose(current.Position + new Planar(_testDriveVelocity.x, _testDriveVelocity.y) * dt,
                                      current.Yaw + _testDriveTurnSpeed * dt);
            else
                return;

            if (!target.IsFinite) return;   // a bad tracking sample must never reach the physics body

            CartPose next = CartSteering.Step(current, target, _maxSpeed * dt, _maxTurnSpeed * dt);
            CartSteering.BlockedFallbacks(current, next, _tries);
            foreach (CartPose pose in _tries)
            {
                if (IsBlocked(pose, out int overlapCount)) continue;
                PruneIgnored(overlapCount);
                MoveTo(pose);
                return;
            }
        }

        // Mirrors the (at most two) hands holding the handles and re-grips whenever that set changes,
        // so grabbing or letting go never moves the cart.
        private void SyncHands()
        {
            _holding.Clear();
            if (_handles != null)
                foreach (Hand hand in _handles.GetHeldBy())
                    if (hand != null && hand.follow != null && _holding.Count < 2)
                        _holding.Add(hand);

            if (SameHands()) return;

            bool pushStarted = _hands.Count == 0;
            _hands.Clear();
            _hands.AddRange(_holding);
            if (_hands.Count == 0) return;

            CartPose cart = CurrentPose();
            if (_hands.Count == 1) _steering.GripOne(cart, HandPoint(_hands[0]), HeadPoint(_hands[0]));
            else _steering.GripTwo(cart, HandPoint(_hands[0]), HandPoint(_hands[1]));

            if (pushStarted)
            {
                CaptureIgnored();
                IgnorePlayer(true);
            }
        }

        private bool SameHands()
        {
            if (_holding.Count != _hands.Count) return false;
            for (int i = 0; i < _hands.Count; i++)
                if (_holding[i] != _hands[i]) return false;
            return true;
        }

        private CartPose CurrentPose()
        {
            Vector3 position = _body.position;
            return new CartPose(new Planar(position.x, position.z), _body.rotation.eulerAngles.y);
        }

        private static Planar HandPoint(Hand hand) => Flat(hand.follow.position);

        // Steering reference: a point behind the head camera, so turning the head (which swings the camera
        // sideways around the neck) doesn't steer. Without a head reference the hand itself is used, which
        // leaves the heading unchanged (no steering).
        private Planar HeadPoint(Hand hand)
        {
            if (_head == null) return HandPoint(hand);

            Vector3 forward = _head.forward;
            forward.y = 0f;
            Vector3 neck = forward.sqrMagnitude > 1e-4f ? _head.position - forward.normalized * _neckOffset : _head.position;
            return Flat(neck);
        }

        private static Planar Flat(Vector3 point) => new Planar(point.x, point.z);

        private void MoveTo(CartPose pose)
        {
            var position = new Vector3(pose.Position.X, _body.position.y, pose.Position.Z);
            Quaternion rotation = Quaternion.Euler(0f, pose.Yaw, 0f);
            _body.MovePosition(position);
            _body.MoveRotation(rotation);

            // Move the handles in the same physics step so the hand holding them never lags the cart.
            if (_handlesBody != null)
            {
                _handlesBody.MovePosition(position + rotation * _handlesLocalPosition);
                _handlesBody.MoveRotation(rotation * _handlesLocalRotation);
            }
        }

        private bool IsBlocked(CartPose pose, out int count)
        {
            count = OverlapAt(pose);
            if (count == _overlaps.Length)
            {
                // The buffer may have dropped a real obstacle: fail safe (don't move) rather than pass through it.
                if (!_warnedFullBuffer)
                {
                    Debug.LogWarning($"#{GetType().Name}# Obstacle query hit its {_overlaps.Length}-collider limit; " +
                                     "treating the move as blocked. Leave hand/grabbable/UI layers out of the Blocking Mask.", this);
                    _warnedFullBuffer = true;
                }
                return true;
            }

            for (int i = 0; i < count; i++)
                if (Blocks(_overlaps[i]) && !_ignored.Contains(_overlaps[i])) return true;
            return false;
        }

        // Static and kinematic colliders outside the cart block it. Dynamic bodies (loose items, the
        // player's body, hands) are pushed aside, and anything on an AutoHand hand (e.g. the wrist device,
        // which may be kinematic) never blocks.
        private bool Blocks(Collider other)
        {
            if (other.transform.IsChildOf(transform)) return false;
            Rigidbody body = other.attachedRigidbody;
            if (body != null && !body.isKinematic) return false;
            return other.GetComponentInParent<Hand>() == null;
        }

        // Anything the cart already overlaps when a push starts is ignored until the cart has moved clear of it,
        // so it never gets stuck but can't be driven through later either.
        private void CaptureIgnored()
        {
            _ignored.Clear();
            int count = OverlapAt(CurrentPose());
            for (int i = 0; i < count; i++)
                if (Blocks(_overlaps[i])) _ignored.Add(_overlaps[i]);
        }

        // _overlaps[0..count) holds what the accepted pose overlaps; ignored colliders not in it block again.
        private void PruneIgnored(int count)
        {
            if (_ignored.Count == 0) return;

            _cleared.Clear();
            foreach (Collider ignored in _ignored)
                if (System.Array.IndexOf(_overlaps, ignored, 0, count) < 0) _cleared.Add(ignored);
            foreach (Collider cleared in _cleared) _ignored.Remove(cleared);
        }

        private int OverlapAt(CartPose pose)
        {
            if (_boxHalfExtents == Vector3.zero) return 0;

            Quaternion rotation = Quaternion.Euler(0f, pose.Yaw, 0f);
            Vector3 center = new Vector3(pose.Position.X, _body.position.y, pose.Position.Z) + rotation * _boxCenter;
            return Physics.OverlapBoxNonAlloc(center, _boxHalfExtents, _overlaps, rotation, _blockingMask,
                                              QueryTriggerInteraction.Ignore);
        }

        private void IgnorePlayer(bool ignore)
        {
            if (_ignoringPlayer == ignore) return;
            if (ignore && _playerColliders.Count == 0) FindPlayerColliders();

            foreach (Collider player in _playerColliders)
                foreach (Collider cart in _cartColliders)
                    if (player != null && cart != null) Physics.IgnoreCollision(player, cart, ignore);
            _ignoringPlayer = ignore;
        }

        // The body capsule is on AutoHandPlayer; the head collider belongs to the HeadPhysicsFollower that
        // AutoHandPlayer creates at runtime.
        private void FindPlayerColliders()
        {
            if (_player == null) _player = FindFirstObjectByType<AutoHandPlayer>();
            if (_player == null) return;

            if (_player.bodyCollider != null) _playerColliders.Add(_player.bodyCollider);
            var head = FindFirstObjectByType<HeadPhysicsFollower>();
            if (head != null && head.TryGetComponent(out Collider headCollider)) _playerColliders.Add(headCollider);
        }

        // Bounds test (conservative) — works while collisions are ignored, unlike contacts.
        private bool OverlapsPlayer()
        {
            foreach (Collider player in _playerColliders)
                foreach (Collider cart in _cartColliders)
                    if (player != null && cart != null && player.bounds.Intersects(cart.bounds)) return true;
            return false;
        }

        // Obstacle box = the model's mesh bounds in cart space, shrunk by a skin and lifted off the floor.
        private void CacheObstacleBox()
        {
            _boxHalfExtents = Vector3.zero;
            if (_footprint == null || _footprint.sharedMesh == null) return;

            Bounds bounds = _footprint.sharedMesh.bounds;
            Vector3 min = Vector3.positiveInfinity;
            Vector3 max = Vector3.negativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                var sign = new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
                Vector3 corner = _footprint.transform.TransformPoint(bounds.center + Vector3.Scale(bounds.extents, sign));
                Vector3 local = transform.InverseTransformPoint(corner);
                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
            }

            min = new Vector3(min.x + Skin, Mathf.Max(min.y, _floorClearance), min.z + Skin);
            max -= new Vector3(Skin, Skin, Skin);
            _boxCenter = (min + max) * 0.5f;
            _boxHalfExtents = Vector3.Max(Vector3.zero, (max - min) * 0.5f);
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) CacheObstacleBox();
            if (_boxHalfExtents == Vector3.zero) return;

            Gizmos.color = new Color(1f, 0.6f, 0f, 0.8f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.Euler(0f, transform.eulerAngles.y, 0f), Vector3.one);
            Gizmos.DrawWireCube(_boxCenter, _boxHalfExtents * 2f);
        }
    }
}
