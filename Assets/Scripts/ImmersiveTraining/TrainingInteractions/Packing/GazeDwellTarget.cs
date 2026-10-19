using UnityEngine;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// A station sign the trainee locates by looking at it (M2_02): half a second of gaze within 12 degrees from up to
    /// 4.5 m. Once found it turns green, shows its found mark and reports itself met to <see cref="_gate"/>, which
    /// completes the step when every sign is found. Detection starts when the station activates it (module start).
    /// </summary>
    public class GazeDwellTarget : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private MultiCounterGate _gate;
        [SerializeField] private float _maxAngleDeg = 12f;
        [SerializeField] private float _maxDistance = 4.5f;
        [SerializeField] private float _dwellSeconds = 0.5f;

        [Tooltip("Renderer tinted when the sign is found (the sign board).")]
        [SerializeField] private Renderer _board;

        [SerializeField] private Color _foundColor = new Color(0.15f, 0.6f, 0.25f);
        [SerializeField] private GameObject _foundMark;

        private GazeDwell _dwell;
        private MaterialPropertyBlock _block;
        private Color _baseColor = Color.white;
        private bool _active;

        /// <summary>Head used for the gaze test instead of the main camera (Play-mode test driver).</summary>
        public Transform HeadOverride { get; set; }

        public bool IsFound => _dwell != null && _dwell.Found;

        private void Awake()
        {
            _dwell = new GazeDwell(_maxAngleDeg, _maxDistance, _dwellSeconds);
            _block = new MaterialPropertyBlock();
            if (_board != null && _board.sharedMaterial != null && _board.sharedMaterial.HasProperty(BaseColorId))
                _baseColor = _board.sharedMaterial.GetColor(BaseColorId);
            ShowFound(false);
        }

        /// <summary>Starts (or restarts) detection, not found.</summary>
        public void Activate()
        {
            if (_dwell == null) Awake();
            _dwell.Reset();
            _active = true;
            ShowFound(false);
            if (_gate != null) _gate.SetSourceMet(this, false);
        }

        private void Update()
        {
            if (!_active || _dwell.Found) return;

            Transform head = HeadOverride != null ? HeadOverride : Camera.main != null ? Camera.main.transform : null;
            if (head == null) return;

            Vector3 toTarget = transform.position - head.position;
            Vector3 forward = head.forward;
            float angle = GazeDwell.AngleBetween(new Vec3(forward.x, forward.y, forward.z), new Vec3(toTarget.x, toTarget.y, toTarget.z));
            if (!_dwell.Update(angle, toTarget.magnitude, Time.deltaTime)) return;

            ShowFound(true);
            if (_gate != null) _gate.SetSourceMet(this, true);
        }

        private void ShowFound(bool found)
        {
            if (_foundMark != null) _foundMark.SetActive(found);
            if (_board == null) return;
            _board.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, found ? _foundColor : _baseColor);
            _board.SetPropertyBlock(_block);
        }
    }
}
