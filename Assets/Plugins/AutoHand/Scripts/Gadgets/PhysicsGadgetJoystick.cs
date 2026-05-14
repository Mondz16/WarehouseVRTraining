using UnityEngine;

namespace Autohand {
    [RequireComponent(typeof(ConfigurableJoint))]
    public class PhysicsGadgetJoystick : MonoBehaviour {
        public bool invertX;
        public bool invertY;

        [Tooltip("Minimum value required to output a nonzero result")]
        public float playRange = 0.05f;

        [Tooltip("Max expected angle in degrees for the X-axis (forward/backward tilt)")]
        public float maxXAngle = 45f;

        [Tooltip("Max expected angle in degrees for the Z-axis (left/right tilt)")]
        public float maxZAngle = 45f;

        private ConfigurableJoint joint;
        private Rigidbody body;
        private Quaternion initialLocalRotation;

        private Vector2 value;

        void Start() {
            joint = GetComponent<ConfigurableJoint>();
            body = GetComponent<Rigidbody>();
            initialLocalRotation = transform.localRotation;
        }
        
        public Vector2 GetValue() {
            
            Quaternion currentLocalRotation = transform.localRotation;
            Quaternion delta = Quaternion.Inverse(initialLocalRotation) * currentLocalRotation;

            // Convert to angle-axis representation
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;

            // Project the angle onto local joystick axes
            Vector3 localAxis = transform.InverseTransformDirection(axis.normalized) * angle;

            float xTilt = Mathf.Clamp(localAxis.x / maxXAngle, -1f, 1f);
            float zTilt = Mathf.Clamp(localAxis.z / maxZAngle, -1f, 1f);

            value = new Vector2(zTilt, xTilt); // (x, y) = (left-right, forward-back)

            // Apply deadzone
            if (Mathf.Abs(value.x) < playRange) value.x = 0;
            if (Mathf.Abs(value.y) < playRange) value.y = 0;

            // Invert if needed
            value.x = invertX ? -value.x : value.x;
            value.y = invertY ? -value.y : value.y;
            
            return value;
        }
    }
}
