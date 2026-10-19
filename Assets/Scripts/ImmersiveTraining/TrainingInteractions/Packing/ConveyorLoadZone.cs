using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// The outbound conveyor (M2_07). A sealed, labelled carton let go inside the load zone with its label (top face)
    /// facing up is taken over: it rides along the belt toward the dock and is dispatched at the end, which tells the
    /// station to start the next order. A box with the label down or on its side is refused with a message.
    /// Completion is arm-aware: a box loaded before M2_07 opens counts when it opens.
    /// </summary>
    public class ConveyorLoadZone : MonoBehaviour
    {
        [Tooltip("Load-zone centre. Its local axes define the zone box; the belt runs along its +X.")]
        [SerializeField] private Transform _zone;

        [SerializeField] private Vector3 _zoneHalfExtents = new Vector3(0.5f, 0.35f, 0.32f);
        [SerializeField] private float _beltSpeed = 0.5f;
        [SerializeField] private float _dispatchDistance = 4.2f;
        [SerializeField] private float _maxTiltDeg = 35f;

        [Tooltip("A box still moving faster than this (m/s) is not judged yet.")]
        [SerializeField] private float _maxRestSpeed = 0.4f;

        [SerializeField] private PackingStation _station;
        [SerializeField] private PackingFeedback _feedback;
        [SerializeField] private ChallengeTutorialTrigger _challengeTrigger;

        private readonly ArmedLatch _latch = new ArmedLatch();
        private readonly List<PackingCarton> _riding = new List<PackingCarton>();
        private readonly Dictionary<PackingCarton, float> _travelled = new Dictionary<PackingCarton, float>();
        private readonly HashSet<PackingCarton> _warned = new HashSet<PackingCarton>();
        private readonly List<PackingCarton> _scratch = new List<PackingCarton>();
        private bool _accepted;

        public bool HasAccepted => _accepted;
        public Vector3 LoadPoint => Zone.position;
        private Transform Zone => _zone != null ? _zone : transform;

        private void Awake()
        {
            if (_challengeTrigger != null) _challengeTrigger.Armed += HandleArmed;
        }

        private void OnDestroy()
        {
            if (_challengeTrigger != null) _challengeTrigger.Armed -= HandleArmed;
        }

        private void Update()
        {
            _scratch.Clear();
            _scratch.AddRange(PackingCarton.All);
            foreach (PackingCarton carton in _scratch) Judge(carton);
            MoveRiding();
        }

        private void Judge(PackingCarton carton)
        {
            if (carton == null || carton.IsOnConveyor || carton.IsLocked) return;
            if (!InZone(carton.transform.position) || carton.IsHeld)
            {
                _warned.Remove(carton); // a fresh attempt gets a fresh message
                return;
            }

            Rigidbody body = carton.Body;
            if (body != null && !body.isKinematic && body.linearVelocity.magnitude > _maxRestSpeed) return;

            if (!carton.IsLabelled)
            {
                Warn(carton, "Only sealed, labelled boxes go on the outbound conveyor.");
                return;
            }

            if (!Orientation.IsFaceUp(carton.transform.up.y, _maxTiltDeg))
            {
                Warn(carton, "The label must face UP - the sorter scanners read it from above. Turn the box over.");
                return;
            }

            Accept(carton);
        }

        private bool InZone(Vector3 world)
        {
            Vector3 p = Zone.InverseTransformPoint(world);
            var zone = new BoxRegion(new Vec3(0f, 0f, 0f), new Vec3(_zoneHalfExtents.x, _zoneHalfExtents.y, _zoneHalfExtents.z));
            return zone.Contains(new Vec3(p.x, p.y, p.z));
        }

        private void Warn(PackingCarton carton, string message)
        {
            if (_warned.Add(carton) && _feedback != null) _feedback.Show(message, false);
        }

        private void Accept(PackingCarton carton)
        {
            carton.PutOnConveyor();

            // Settle square on the belt with the label up, keeping the box's heading.
            Vector3 heading = Vector3.ProjectOnPlane(carton.transform.forward, Vector3.up);
            if (heading.sqrMagnitude < 1e-4f) heading = Zone.forward;
            carton.transform.rotation = Quaternion.LookRotation(heading, Vector3.up);

            _riding.Add(carton);
            _travelled[carton] = 0f;
            _warned.Remove(carton);
            _accepted = true;
            if (_feedback != null) _feedback.Show("Box loaded label-up - on its way to the dock.", true);
            Offer();
        }

        private void MoveRiding()
        {
            for (int i = _riding.Count - 1; i >= 0; i--)
            {
                PackingCarton carton = _riding[i];
                if (carton == null)
                {
                    _riding.RemoveAt(i);
                    continue;
                }

                float step = _beltSpeed * Time.deltaTime;
                carton.transform.position += Zone.right * step;
                float travelled = _travelled[carton] + step;
                _travelled[carton] = travelled;
                if (travelled < _dispatchDistance) continue;

                _riding.RemoveAt(i);
                _travelled.Remove(carton);
                carton.gameObject.SetActive(false);
                if (_station != null) _station.HandleDispatched(carton);
            }
        }

        private void HandleArmed(ChallengeTutorialTrigger trigger) => Offer();

        private void Offer() => _latch.Offer(_accepted, () => _challengeTrigger == null || _challengeTrigger.TryComplete());

        /// <summary>Clears the belt and the completion (module restart; the station destroys the cartons).</summary>
        public void ResetZone()
        {
            _riding.Clear();
            _travelled.Clear();
            _warned.Clear();
            _accepted = false;
            _latch.Reset();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.matrix = Zone.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, _zoneHalfExtents * 2f);
        }
    }
}
