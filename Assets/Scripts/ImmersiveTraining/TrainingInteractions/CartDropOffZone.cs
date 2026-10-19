using UnityEngine;
using UnityEngine.Rendering;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// The cart drop-off spot (M1_06). Tells a <see cref="MultiCounterGate"/>, live, whether the cart's
    /// centre is inside this rectangle on the floor, so the multi-line order only completes once the cart
    /// is parked back here (a trainee who never moved the cart is already parked). Draws its own flat
    /// outline with the LineRenderer on this GameObject, so the floor marking always matches the zone.
    /// The rectangle is centred on this transform, turned with its yaw and sized by <see cref="_size"/>.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class CartDropOffZone : MonoBehaviour
    {
        [Tooltip("The cart root; its position is the cart's centre.")]
        [SerializeField] private Transform _cart;

        [Tooltip("Gate that needs the cart parked here (M1_MultiLineGate).")]
        [SerializeField] private MultiCounterGate _gate;

        [Tooltip("Zone size in metres along its own X and Z.")]
        [SerializeField] private Vector2 _size = new Vector2(1.6f, 1.3f);

        [Header("Outline")]
        [SerializeField] private Material _outlineMaterial;
        [SerializeField] private Color _outlineColor = new Color(1f, 0.85f, 0f, 1f);
        [SerializeField] private float _outlineWidth = 0.05f;

        [Tooltip("Height of the outline above this transform, so it doesn't z-fight with the floor.")]
        [SerializeField] private float _outlineHeight = 0.02f;

        private bool _reported;
        private bool _inside;

        /// <summary>True while the cart's centre is inside the zone.</summary>
        public bool CartInside => _inside;

        private void Awake()
        {
            DrawOutline();
            // An unwired zone would silently keep M1_06 from ever completing.
            if (_cart == null || _gate == null)
                Debug.LogWarning($"#{GetType().Name}# Cart or gate is not assigned, so the drop-off can't report.", this);
        }

        private void OnValidate() => DrawOutline();

        private void OnEnable() => _reported = false;

        // A disabled zone doesn't count as the cart being parked.
        private void OnDisable()
        {
            if (_reported && _inside && _gate != null) _gate.SetSourceMet(this, false);
            _inside = false;
            _reported = false;
        }

        private void FixedUpdate()
        {
            if (_cart == null) return;

            Vector3 centre = transform.position;
            Vector3 cart = _cart.position;
            bool inside = DropOffArea.Contains(new Planar(centre.x, centre.z), transform.eulerAngles.y,
                                               new Planar(Mathf.Abs(_size.x) * 0.5f, Mathf.Abs(_size.y) * 0.5f),
                                               new Planar(cart.x, cart.z));
            if (_reported && inside == _inside) return;

            _inside = inside;
            _reported = true;
            if (_gate != null) _gate.SetSourceMet(this, inside);
        }

        private void DrawOutline()
        {
            var line = GetComponent<LineRenderer>();
            if (line == null) return;

            float x = Mathf.Abs(_size.x) * 0.5f;
            float z = Mathf.Abs(_size.y) * 0.5f;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 4;
            line.SetPosition(0, new Vector3(-x, _outlineHeight, -z));
            line.SetPosition(1, new Vector3(-x, _outlineHeight, z));
            line.SetPosition(2, new Vector3(x, _outlineHeight, z));
            line.SetPosition(3, new Vector3(x, _outlineHeight, -z));
            line.startWidth = _outlineWidth;
            line.endWidth = _outlineWidth;
            line.startColor = _outlineColor;
            line.endColor = _outlineColor;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            if (_outlineMaterial != null) line.sharedMaterial = _outlineMaterial;
        }
    }
}
