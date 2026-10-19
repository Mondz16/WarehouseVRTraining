using System.Collections.Generic;
using Autohand;
using UnityEngine;

namespace ImmersiveTraining.TrainingInteractions
{
    public enum SeamToolKind { Tape, Cutter }

    /// <summary>
    /// The working tip of a tape gun (tapes a closed carton's seams, M2_05 / M2_09) or box cutter (cuts the QC box's
    /// centre seam open, M2_09). Cartons find tools through <see cref="All"/> and only react while the tool is held.
    /// </summary>
    public class SeamTool : MonoBehaviour
    {
        public static readonly List<SeamTool> All = new List<SeamTool>();

        [SerializeField] private SeamToolKind _kind;

        [Tooltip("Point that touches the seam (tape roller / blade tip). Defaults to this transform.")]
        [SerializeField] private Transform _head;

        [SerializeField] private Grabbable _grabbable;

        /// <summary>Lets the Play-mode test driver use the tool without a hand holding it.</summary>
        public bool ForceActive { get; set; }

        public SeamToolKind Kind => _kind;
        public Vector3 HeadPosition => (_head != null ? _head : transform).position;
        public bool IsActive => ForceActive || (_grabbable != null && _grabbable.IsHeld());

        private void Awake()
        {
            if (_grabbable == null) _grabbable = GetComponent<Grabbable>();
        }

        private void OnEnable() => All.Add(this);

        private void OnDisable() => All.Remove(this);
    }
}
