using System.Collections.Generic;
using UnityEngine;
using Valari.Managers;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// Records the trainee's walking route through the warehouse during the Order Picking module.
    /// Starts (and clears) when the OrderPicking training begins and samples the tracked player's
    /// floor position whenever they have moved at least <see cref="_minSampleDistance"/> on the XZ
    /// plane, until <see cref="StopRecording"/> is called (at the M1_08 review step). The recorded
    /// <see cref="Points"/> feed the amber "actual route" line in RouteOverlayController.
    /// </summary>
    public class RouteRecorder : MonoBehaviour
    {
        [Tooltip("Transform whose XZ position is sampled (the player rig / body).")]
        [SerializeField] private Transform _player;

        [Tooltip("Minimum XZ distance (metres) the player must move before a new point is recorded.")]
        [SerializeField] private float _minSampleDistance = 0.3f;

        [Tooltip("World Y the recorded points are flattened to (line sits just above the floor).")]
        [SerializeField] private float _floorY = 0.03f;

        [Tooltip("Start recording automatically when the OrderPicking module begins.")]
        [SerializeField] private bool _autoStartOnModule = true;

        private readonly List<Vector3> _points = new List<Vector3>();
        private bool _recording;
        private Vector3 _lastSample;

        public IReadOnlyList<Vector3> Points => _points;
        public bool IsRecording => _recording;

        private void OnEnable()
        {
            TutorialManager.OnTrainingStartedEvent -= HandleTrainingStarted;
            TutorialManager.OnTrainingStartedEvent += HandleTrainingStarted;
        }

        private void OnDisable()
        {
            TutorialManager.OnTrainingStartedEvent -= HandleTrainingStarted;
        }

        private void HandleTrainingStarted(TrainingID id)
        {
            if (_autoStartOnModule && id == TrainingID.OrderPicking)
                StartRecording();
        }

        /// <summary>Clears any previous route and begins sampling.</summary>
        public void StartRecording()
        {
            _points.Clear();
            _recording = true;
            if (_player != null)
            {
                _lastSample = Flatten(_player.position);
                _points.Add(_lastSample);
            }
        }

        /// <summary>Stops sampling; the accumulated <see cref="Points"/> remain for the overlay.</summary>
        public void StopRecording()
        {
            _recording = false;
        }

        private void Update()
        {
            if (!_recording || _player == null) return;

            Vector3 current = Flatten(_player.position);
            if ((current - _lastSample).sqrMagnitude >= _minSampleDistance * _minSampleDistance)
            {
                _points.Add(current);
                _lastSample = current;
            }
        }

        private Vector3 Flatten(Vector3 p) => new Vector3(p.x, _floorY, p.z);
    }
}
