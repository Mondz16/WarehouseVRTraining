using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// Keeps a few air pillows ready at the void-fill dispenser (M2_04 / M2_08 / M2_09). Pillows are pooled: a carton
    /// that absorbs or refuses one hands it back, and an empty slot is refilled shortly after its pillow is taken.
    /// </summary>
    public class VoidFillDispenser : MonoBehaviour
    {
        [SerializeField] private VoidFillPiece _piecePrefab;
        [SerializeField] private Transform[] _slots = new Transform[0];
        [SerializeField] private int _poolSize = 12;

        [Tooltip("A pillow further than this from its slot counts as taken (metres).")]
        [SerializeField] private float _takenDistance = 0.15f;

        [SerializeField] private float _refillDelay = 0.6f;

        private readonly Queue<VoidFillPiece> _pool = new Queue<VoidFillPiece>();
        private VoidFillPiece[] _inSlot;
        private float[] _refillAt;

        public int SlotCount => _slots.Length;

        /// <summary>The pillow waiting in a slot, or null.</summary>
        public VoidFillPiece PieceInSlot(int slot) =>
            _inSlot != null && slot >= 0 && slot < _inSlot.Length ? _inSlot[slot] : null;

        private void Awake()
        {
            _inSlot = new VoidFillPiece[_slots.Length];
            _refillAt = new float[_slots.Length];
            if (_piecePrefab == null)
            {
                Debug.LogWarning($"#{GetType().Name}# No pillow prefab set; the dispenser stays empty.", this);
                return;
            }

            for (int i = 0; i < _poolSize; i++)
            {
                VoidFillPiece piece = Instantiate(_piecePrefab, transform.position, transform.rotation, transform);
                piece.name = $"{_piecePrefab.name}_{i:00}";
                piece.Dispenser = this;
                piece.gameObject.SetActive(false);
                _pool.Enqueue(piece);
            }
        }

        private void Update()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                VoidFillPiece piece = _inSlot[i];
                if (piece != null)
                {
                    bool waiting = piece.gameObject.activeSelf && !piece.IsHeld &&
                                   (piece.transform.position - _slots[i].position).sqrMagnitude <= _takenDistance * _takenDistance;
                    if (waiting) continue;

                    _inSlot[i] = null; // taken
                    _refillAt[i] = Time.time + _refillDelay;
                }

                if (Time.time >= _refillAt[i]) FillSlot(i);
            }
        }

        private void FillSlot(int slot)
        {
            if (_pool.Count == 0 || _slots[slot] == null) return;

            VoidFillPiece piece = _pool.Dequeue();
            piece.transform.SetPositionAndRotation(_slots[slot].position, _slots[slot].rotation);
            piece.gameObject.SetActive(true);
            if (piece.Body != null && !piece.Body.isKinematic)
            {
                piece.Body.position = _slots[slot].position;
                piece.Body.rotation = _slots[slot].rotation;
                piece.Body.linearVelocity = Vector3.zero;
                piece.Body.angularVelocity = Vector3.zero;
            }

            _inSlot[slot] = piece;
        }

        /// <summary>Takes a pillow back into the pool.</summary>
        public void Return(VoidFillPiece piece)
        {
            if (piece == null || !piece.gameObject.activeSelf) return;

            for (int i = 0; i < _inSlot.Length; i++)
                if (_inSlot[i] == piece) _inSlot[i] = null;

            piece.gameObject.SetActive(false);
            if (piece.transform.parent != transform) piece.transform.SetParent(transform, true);
            _pool.Enqueue(piece);
        }
    }
}
