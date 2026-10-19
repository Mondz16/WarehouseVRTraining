using System.Collections.Generic;
using Autohand;
using UnityEngine;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// A product that is packed into a <see cref="PackingCarton"/> (M2). Cartons find loose products through
    /// <see cref="All"/>; an accepted product is made inert and parented into the carton, a refused one goes back to
    /// where it was spawned.
    /// </summary>
    public class PackableItem : MonoBehaviour
    {
        public static readonly List<PackableItem> All = new List<PackableItem>();

        [SerializeField] private string _displayName;

        [Tooltip("Outer size in centimetres: length (local X), height (Y), width (Z).")]
        [SerializeField] private Vector3 _sizeCm = new Vector3(20f, 10f, 10f);

        [SerializeField] private Grabbable _grabbable;
        [SerializeField] private Rigidbody _body;

        private Vector3 _homePosition;
        private Quaternion _homeRotation;

        public string DisplayName => _displayName;
        public Vector3 SizeMetres => _sizeCm * 0.01f;

        /// <summary>Order the product belongs to (set by the station when it spawns the product).</summary>
        public string OrderId { get; set; }

        public bool IsPacked { get; private set; }
        public bool IsHeld => _grabbable != null && _grabbable.IsHeld();

        private void Awake()
        {
            if (_grabbable == null) _grabbable = GetComponent<Grabbable>();
            if (_body == null) _body = GetComponent<Rigidbody>();
            SetHome(transform.position, transform.rotation);
        }

        private void OnEnable() => All.Add(this);

        private void OnDisable() => All.Remove(this);

        public void SetHome(Vector3 position, Quaternion rotation)
        {
            _homePosition = position;
            _homeRotation = rotation;
        }

        /// <summary>Puts a refused product back where it was spawned.</summary>
        public void ReturnHome()
        {
            if (IsPacked) return;
            if (_grabbable != null) _grabbable.ForceHandsRelease();
            transform.SetPositionAndRotation(_homePosition, _homeRotation);
            if (_body == null || _body.isKinematic) return;
            _body.position = _homePosition;
            _body.rotation = _homeRotation;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
        }

        /// <summary>Makes the product part of the carton: no longer grabbable or physical, parented at a slot.</summary>
        public void PackInto(Transform parent, Vector3 localPosition, Quaternion localRotation)
        {
            if (_grabbable != null)
            {
                _grabbable.ForceHandsRelease();
                _grabbable.isGrabbable = false;
            }

            if (_body != null)
            {
                _body.isKinematic = true;
                _body.detectCollisions = false;
                // An interpolated Rigidbody writes its own (old) physics pose to the transform every frame, overriding
                // the movement it inherits from the carton - the product was left behind when the box was carried or
                // rode the conveyor. AutoHand restores Interpolate when the hand lets go, so switch it off here.
                _body.interpolation = RigidbodyInterpolation.None;
            }

            transform.SetParent(parent, false);
            transform.localPosition = localPosition;
            transform.localRotation = localRotation;
            IsPacked = true;
        }
    }
}
