using UnityEngine;

namespace Autohand {
    [RequireComponent(typeof(HingeJoint))]
    public class PhysicsGadgetHingeAngleReader : MonoBehaviour {
        public bool invertValue = false;

        [Tooltip("Minimum movement threshold to return a nonzero value")]
        public float playRange = 0.05f;

        [Tooltip("Max rotation angle expected from rest, in degrees")]
        [SerializeField] private float maxAngle = 45f;

        private HingeJoint joint;
        private float value = 0;
        private Quaternion startRotation;

        protected virtual void Start() {
            joint = GetComponent<HingeJoint>();
            startRotation = transform.localRotation;
        }

        /// <summary>
        /// Returns a value from -1 to 1 based on the lever's current angle relative to its rest position.
        /// </summary>
        public float GetValue() {
            Quaternion delta = Quaternion.Inverse(startRotation) * transform.localRotation;
            float angle = delta.eulerAngles.x; // Or y/z depending on your lever's hinge axis

            if (angle > 180f)
                angle -= 360f;

            value = angle / maxAngle;
            value = invertValue ? -value : value;

            if (Mathf.Abs(value) < playRange)
                value = 0;

            return Mathf.Clamp(value, -1f, 1f);
        }

        public HingeJoint GetJoint() => joint;
    }
}