using UnityEngine;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// Draws the path-efficiency review overlay for M1_08: a green "optimal" route line through the
    /// hand-authored <see cref="_optimalWaypoints"/> and an amber "actual" route line through the
    /// points recorded by <see cref="_recorder"/>. Call <see cref="ShowOverlay"/> when the M1_08
    /// review step opens and <see cref="HideOverlay"/> to clear it.
    /// </summary>
    public class RouteOverlayController : MonoBehaviour
    {
        [Header("Lines")]
        [SerializeField] private LineRenderer _optimalLine;
        [SerializeField] private LineRenderer _actualLine;

        [Header("Route sources")]
        [Tooltip("Ordered markers describing the optimal route (the green line connects them).")]
        [SerializeField] private Transform[] _optimalWaypoints;

        [Tooltip("Recorder supplying the trainee's actual route (the amber line).")]
        [SerializeField] private RouteRecorder _recorder;

        [Tooltip("World Y the optimal waypoints are flattened to (match the recorder's floor Y).")]
        [SerializeField] private float _lineY = 0.03f;

        private void Awake()
        {
            HideOverlay();
        }

        [ContextMenu("Show Overlay")]
        public void ShowOverlay()
        {
            DrawOptimal();
            DrawActual();

            if (_optimalLine != null) _optimalLine.enabled = true;
            if (_actualLine != null) _actualLine.enabled = true;
        }

        [ContextMenu("Hide Overlay")]
        public void HideOverlay()
        {
            if (_optimalLine != null) _optimalLine.enabled = false;
            if (_actualLine != null) _actualLine.enabled = false;
        }

        private void DrawOptimal()
        {
            if (_optimalLine == null || _optimalWaypoints == null) return;

            _optimalLine.useWorldSpace = true;
            _optimalLine.positionCount = _optimalWaypoints.Length;
            for (int i = 0; i < _optimalWaypoints.Length; i++)
            {
                Vector3 p = _optimalWaypoints[i] != null ? _optimalWaypoints[i].position : Vector3.zero;
                _optimalLine.SetPosition(i, new Vector3(p.x, _lineY, p.z));
            }
        }

        private void DrawActual()
        {
            if (_actualLine == null || _recorder == null) return;

            var pts = _recorder.Points;
            _actualLine.useWorldSpace = true;
            _actualLine.positionCount = pts.Count;
            for (int i = 0; i < pts.Count; i++)
                _actualLine.SetPosition(i, pts[i]);
        }
    }
}
