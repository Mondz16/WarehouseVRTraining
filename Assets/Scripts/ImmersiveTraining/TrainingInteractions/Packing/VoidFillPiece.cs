using System.Collections.Generic;
using Autohand;
using UnityEngine;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// One air pillow (M2 void fill). A <see cref="PackingCarton"/> absorbs it into its fill level, or refuses it;
    /// either way it goes back to its <see cref="VoidFillDispenser"/> pool.
    /// </summary>
    public class VoidFillPiece : MonoBehaviour
    {
        public static readonly List<VoidFillPiece> All = new List<VoidFillPiece>();

        [SerializeField] private Grabbable _grabbable;
        [SerializeField] private Rigidbody _body;

        public VoidFillDispenser Dispenser { get; set; }
        public bool IsHeld => _grabbable != null && _grabbable.IsHeld();
        public Rigidbody Body => _body;

        private void Awake()
        {
            if (_grabbable == null) _grabbable = GetComponent<Grabbable>();
            if (_body == null) _body = GetComponent<Rigidbody>();
        }

        private void OnEnable() => All.Add(this);

        private void OnDisable() => All.Remove(this);

        public void Recycle()
        {
            if (_grabbable != null) _grabbable.ForceHandsRelease();
            if (Dispenser != null) Dispenser.Return(this);
            else gameObject.SetActive(false);
        }
    }
}
