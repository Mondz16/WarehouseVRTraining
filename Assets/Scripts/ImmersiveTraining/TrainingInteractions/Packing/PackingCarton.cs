using System;
using System.Collections.Generic;
using Autohand;
using UnityEngine;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// A shipping carton and everything that happens to it at the packing station (M2): void fill and products going
    /// in, the flaps closing once it is full, the three H-pattern seams being taped (or the QC box's centre seam being
    /// cut open) and the shipping label. The rules live in <see cref="VoidFillState"/> and <see cref="SeamCoverage"/>;
    /// this component polls the loose pillows, products and tools near it and raises <see cref="Changed"/> on every
    /// state change, which the station / label / conveyor / QC bridges turn into challenge completions.
    ///
    /// Local axes: length along X, height along Y (pivot at the outside bottom centre), width along Z. Contents are
    /// only taken while the carton is packable (locked on the packing spot or the QC table) and open.
    /// </summary>
    public class PackingCarton : MonoBehaviour
    {
        public static readonly List<PackingCarton> All = new List<PackingCarton>();

        /// <summary>Wall and lid board thickness in metres (the builder uses the same value).</summary>
        public const float WallThickness = 0.008f;

        public const int SeamCentre = 0, SeamEndA = 1, SeamEndB = 2, SeamCount = 3;

        private const float BaseShare = 0.15f, FullShare = 0.9f;

        public enum LabelResult { Applied, NotSealed, AlreadyLabelled }

        [Header("Carton")]
        [SerializeField] private CartonSize _size = CartonSize.Medium;

        [Tooltip("Inner size in metres: length (X), height (Y), width (Z).")]
        [SerializeField] private Vector3 _innerSize = new Vector3(0.45f, 0.25f, 0.35f);

        [SerializeField] private Grabbable _grabbable;
        [SerializeField] private Rigidbody _body;

        [Header("Rules")]
        [SerializeField] private int _baseFill = 2;
        [SerializeField] private int _topFill = 3;

        [Tooltip("How close (metres) a tool head must pass to a seam line to work on it.")]
        [SerializeField] private float _seamRadius = 0.07f;

        [Tooltip("If true, the box cutter can cut the taped centre seam open (the QC box, M2_09).")]
        [SerializeField] private bool _cuttable;

        [Header("Visuals")]
        [SerializeField] private GameObject _openFlaps;

        [Tooltip("Closed top, including its collider.")]
        [SerializeField] private GameObject _lid;

        [SerializeField] private Transform _fillVisual;

        [Tooltip("Tape strips in seam order: centre, end A (-X), end B (+X). Each is scaled along its local X while taping.")]
        [SerializeField] private Transform[] _tapeStrips = new Transform[SeamCount];

        [Tooltip("Guides showing where the tape goes; shown on untaped seams while the box is closed.")]
        [SerializeField] private GameObject[] _seamGuides = new GameObject[SeamCount];

        [SerializeField] private GameObject _labelVisual;
        [SerializeField] private Transform _contentsRoot;

        [Header("Pre-packed state (QC box)")]
        [SerializeField] private bool _prePacked;
        [SerializeField] private string _prePackedOrderId = "ORD-1874";
        [SerializeField] private int _prePackedFill = 1;
        [SerializeField] private int _prePackedItems = 1;

        [Header("Feedback (optional; the station sets it on the cartons it spawns)")]
        [SerializeField] private PackingFeedback _feedback;

        private readonly SeamCoverage[] _coverage = { new SeamCoverage(), new SeamCoverage(), new SeamCoverage() };
        private readonly bool[] _taped = new bool[SeamCount];
        private readonly SeamCoverage _cut = new SeamCoverage();
        private readonly List<PackableItem> _items = new List<PackableItem>();
        private readonly List<PackableItem> _itemScratch = new List<PackableItem>();
        private readonly List<VoidFillPiece> _pieceScratch = new List<VoidFillPiece>();
        private VoidFillState _fill;
        private string _orderId;
        private bool _closed, _labelled, _locked, _packable, _onConveyor;

        /// <summary>Raised on every state change (fill, item, flaps, seam, label, lock).</summary>
        public event Action<PackingCarton> Changed;

        public CartonSize Size => _size;
        public Vector3 InnerSize => _innerSize;
        public string OrderId => _orderId;
        public VoidFillState Fill => _fill;
        public bool IsFull => _fill.IsFull;
        public bool IsBaseFilled => _fill.IsBaseMet;
        public int ItemCount => _items.Count;
        public bool IsClosed => _closed;
        public bool HasBeenOpened { get; private set; }
        public bool IsSealed => _closed && TapedCount == SeamCount;
        public bool IsLabelled => _labelled;
        public bool IsLocked => _locked;
        public bool IsPackable => _packable;
        public bool IsOnConveyor => _onConveyor;
        public bool IsHeld => _grabbable != null && _grabbable.IsHeld();
        public Rigidbody Body => _body;

        public int TapedCount
        {
            get
            {
                int taped = 0;
                foreach (bool seam in _taped)
                    if (seam) taped++;
                return taped;
            }
        }

        public bool IsTaped(int seam) => _taped[seam];

        private float TopY => _innerSize.y + 2f * WallThickness;

        /// <summary>A point above the middle of the opening, for dropping things in (Play-mode test driver).</summary>
        public Vector3 DropPoint => transform.TransformPoint(new Vector3(0f, WallThickness + _innerSize.y * 0.7f, 0f));

        /// <summary>A point just above the top face (Play-mode test driver).</summary>
        public Vector3 TopPoint => transform.TransformPoint(new Vector3(_innerSize.x * 0.22f, TopY + 0.02f, 0f));

        private void Awake()
        {
            if (_grabbable == null) _grabbable = GetComponent<Grabbable>();
            if (_body == null) _body = GetComponent<Rigidbody>();

            if (_prePacked) ResetToPrePacked();
            else Configure(null, 1, _feedback);
        }

        private void OnEnable() => All.Add(this);

        private void OnDisable() => All.Remove(this);

        /// <summary>Sets the order this carton packs and how many products it takes (when the station locks it).</summary>
        public void Configure(string orderId, int itemsRequired, PackingFeedback feedback)
        {
            _orderId = orderId;
            _fill = new VoidFillState(_baseFill, _topFill, itemsRequired);
            if (feedback != null) _feedback = feedback;
            RefreshVisuals();
            RaiseChanged();
        }

        /// <summary>Back to the pre-packed QC state: closed, H-taped, labelled, product inside, under-filled, locked.</summary>
        public void ResetToPrePacked()
        {
            _orderId = _prePackedOrderId;
            _fill = new VoidFillState(_baseFill, _topFill, _prePackedItems);
            _fill.Set(_prePackedFill, _prePackedItems);
            _closed = true;
            _labelled = true;
            HasBeenOpened = false;
            for (int i = 0; i < SeamCount; i++)
            {
                _taped[i] = true;
                _coverage[i].Reset();
            }

            _cut.Reset();
            _locked = true;
            _packable = true;
            if (_grabbable != null) _grabbable.isGrabbable = false;
            if (_body != null) _body.isKinematic = true;
            RefreshVisuals();
            RaiseChanged();
        }

        /// <summary>Holds the carton still at a pose (packing spot) and starts taking contents.</summary>
        public void LockAt(Vector3 position, Quaternion rotation)
        {
            if (_grabbable != null)
            {
                _grabbable.ForceHandsRelease();
                _grabbable.isGrabbable = false;
            }

            if (_body != null) _body.isKinematic = true;
            transform.SetPositionAndRotation(position, rotation);
            _locked = true;
            _packable = true;
            RaiseChanged();
        }

        /// <summary>Releases the carton so it can be carried (after labelling).</summary>
        public void Unlock()
        {
            _locked = false;
            _packable = false;
            if (_grabbable != null) _grabbable.isGrabbable = true;
            if (_body != null) _body.isKinematic = false;
            RaiseChanged();
        }

        /// <summary>Taken over by the conveyor: no longer grabbable, moved by script.</summary>
        public void PutOnConveyor()
        {
            if (_grabbable != null)
            {
                _grabbable.ForceHandsRelease();
                _grabbable.isGrabbable = false;
            }

            if (_body != null)
            {
                _body.isKinematic = true;
                // The belt moves the box through its transform; interpolation would keep dragging it back.
                _body.interpolation = RigidbodyInterpolation.None;
            }

            _onConveyor = true;
            _packable = false;
            RaiseChanged();
        }

        private void Update()
        {
            if (_packable && !_closed) TakeContents();
            if (_closed) SweepSeams();
        }

        // ---- contents ----

        private void TakeContents()
        {
            _pieceScratch.Clear();
            _pieceScratch.AddRange(VoidFillPiece.All);
            foreach (VoidFillPiece piece in _pieceScratch)
            {
                if (_closed) return; // the last pillow filled the box
                if (piece == null || piece.IsHeld || !IsInside(piece.transform.position)) continue;
                AddPillow(piece);
            }

            _itemScratch.Clear();
            _itemScratch.AddRange(PackableItem.All);
            foreach (PackableItem item in _itemScratch)
            {
                if (_closed) return;
                if (item == null || item.IsPacked || item.IsHeld || !IsInside(item.transform.position)) continue;
                AddItem(item);
            }
        }

        private void AddPillow(VoidFillPiece piece)
        {
            FillResult result = _fill.TryAddFill();
            piece.Recycle();
            if (result != FillResult.Added)
            {
                Say(result == FillResult.AlreadyFull
                    ? "The box is already full - no more void fill needed."
                    : "Place the item before adding more fill.", false);
                return;
            }

            RefreshVisuals();
            if (_fill.IsFull)
            {
                Close();
                return;
            }

            if (_fill.Phase == FillPhase.Items)
            {
                int remaining = _fill.ItemsRequired - _fill.ItemsPlaced;
                Say(remaining == 1
                    ? "Base layer done - now place the item in the centre."
                    : $"Base layer done - now place the {remaining} items.", true);
            }
            else
            {
                Say($"Void fill {_fill.Fill}/{_fill.FullLevel}.", true);
            }

            RaiseChanged();
        }

        private void AddItem(PackableItem item)
        {
            if (item.OrderId != _orderId)
            {
                item.ReturnHome();
                Say($"The {item.DisplayName} is not part of this order.", false);
                return;
            }

            ItemResult result = _fill.TryAddItem();
            if (result != ItemResult.Added)
            {
                item.ReturnHome();
                Say(result == ItemResult.NeedsBaseFill
                    ? "Add void fill to the base of the box first."
                    : "This box already holds its items.", false);
                return;
            }

            PackItem(item);
            if (_fill.IsFull)
            {
                Close();
                return;
            }

            int remaining = _fill.ItemsRequired - _fill.ItemsPlaced;
            Say(remaining > 0
                ? $"{item.DisplayName} packed - {remaining} more to go."
                : "Item in - now fill the space on all sides and over the top.", true);
            RaiseChanged();
        }

        private void PackItem(PackableItem item)
        {
            int index = _items.Count;
            int count = Mathf.Max(1, _fill.ItemsRequired);
            float usable = _innerSize.x - 0.06f;
            float x = -usable * 0.5f + usable / count * (index + 0.5f);
            float y = WallThickness + _innerSize.y * BaseShare + item.SizeMetres.y * 0.5f;

            // Several products stand side by side with their long side across the box.
            Quaternion rotation = count > 1 ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
            item.PackInto(_contentsRoot != null ? _contentsRoot : transform, new Vector3(x, y, 0f), rotation);
            _items.Add(item);
        }

        private bool IsInside(Vector3 world)
        {
            Vector3 p = transform.InverseTransformPoint(world);
            float minY = WallThickness - 0.02f, maxY = TopY + 0.1f;
            var interior = new BoxRegion(
                new Vec3(0f, (minY + maxY) * 0.5f, 0f),
                new Vec3(_innerSize.x * 0.5f, (maxY - minY) * 0.5f, _innerSize.z * 0.5f));
            return interior.Contains(new Vec3(p.x, p.y, p.z));
        }

        private void Close()
        {
            _closed = true;
            RefreshVisuals();
            Say("Box full - nothing can move. Flaps closed: seal it with the H-tape pattern.", true);
            RaiseChanged();
        }

        private void Open()
        {
            _closed = false;
            HasBeenOpened = true;
            RefreshVisuals();
            Say("Tape cut - box open. Check the void fill: is there enough to stop the item moving?", true);
            RaiseChanged();
        }

        // ---- seams ----

        private void SweepSeams()
        {
            foreach (SeamTool tool in SeamTool.All)
            {
                if (tool == null || !tool.IsActive) continue;

                Vector3 local = transform.InverseTransformPoint(tool.HeadPosition);
                var head = new Vec3(local.x, local.y, local.z);
                for (int seam = 0; seam < SeamCount; seam++)
                {
                    SeamEnds(seam, out Vec3 a, out Vec3 b);
                    float t = SegmentMath.Closest(head, a, b, out float distance);
                    if (distance > _seamRadius) continue;

                    if (tool.Kind == SeamToolKind.Tape && !_taped[seam]) TapeAt(seam, t);
                    else if (tool.Kind == SeamToolKind.Cutter && _cuttable && seam == SeamCentre && _taped[seam]) CutAt(t);
                    if (!_closed) return; // cut open 
                }
            }
        }

        private void SeamEnds(int seam, out Vec3 a, out Vec3 b)
        {
            float hx = _innerSize.x * 0.5f, hz = _innerSize.z * 0.5f, top = TopY;
            switch (seam)
            {
                case SeamCentre:
                    a = new Vec3(-hx, top, 0f);
                    b = new Vec3(hx, top, 0f);
                    break;
                case SeamEndA:
                    a = new Vec3(-hx, top, -hz);
                    b = new Vec3(-hx, top, hz);
                    break;
                default:
                    a = new Vec3(hx, top, -hz);
                    b = new Vec3(hx, top, hz);
                    break;
            }
        }

        /// <summary>World-space ends of a seam (Play-mode test driver).</summary>
        public void SeamWorldEnds(int seam, out Vector3 start, out Vector3 end)
        {
            SeamEnds(seam, out Vec3 a, out Vec3 b);
            start = transform.TransformPoint(new Vector3(a.X, a.Y, a.Z));
            end = transform.TransformPoint(new Vector3(b.X, b.Y, b.Z));
        }

        private void TapeAt(int seam, float t)
        {
            if (!_coverage[seam].Mark(t)) return;
            if (!_coverage[seam].IsComplete)
            {
                RefreshTape(seam);
                return;
            }

            _taped[seam] = true;
            RefreshTape(seam);
            Say(TapedCount == SeamCount
                ? "H-tape complete - the box is sealed."
                : $"Seam taped ({TapedCount}/{SeamCount}). A single strip is not enough - tape all three.", true);
            RaiseChanged();
        }

        private void CutAt(float t)
        {
            if (!_cut.Mark(t) || !_cut.IsComplete) return;

            _cut.Reset();
            _taped[SeamCentre] = false;
            _coverage[SeamCentre].Reset();
            Open();
        }

        // ---- label ----

        /// <summary>True if <paramref name="world"/> is on (just above) the top face - the largest flat face.</summary>
        public bool IsOnTopFace(Vector3 world)
        {
            Vector3 p = transform.InverseTransformPoint(world);
            return Mathf.Abs(p.x) <= _innerSize.x * 0.5f + 0.02f &&
                   Mathf.Abs(p.z) <= _innerSize.z * 0.5f + 0.02f &&
                   p.y >= TopY - 0.03f && p.y <= TopY + 0.1f;
        }

        public LabelResult TryApplyLabel()
        {
            if (_labelled) return LabelResult.AlreadyLabelled;
            if (!IsSealed) return LabelResult.NotSealed;

            _labelled = true;
            RefreshVisuals();
            Say("Label applied to the top face. Carry the box to the outbound conveyor.", true);
            RaiseChanged();
            return LabelResult.Applied;
        }

        // ---- visuals ----

        private void RefreshVisuals()
        {
            if (_openFlaps != null) _openFlaps.SetActive(!_closed);
            if (_lid != null) _lid.SetActive(_closed);
            // The label and the tape sit on the closed top, so they go with it while the box is open (QC box, M2_09).
            if (_labelVisual != null) _labelVisual.SetActive(_labelled && _closed);

            if (_fillVisual != null && _fill != null)
            {
                float height = _innerSize.y * _fill.FillHeight(BaseShare, FullShare);
                _fillVisual.gameObject.SetActive(height > 0.001f);
                _fillVisual.localScale = new Vector3(_innerSize.x - 0.01f, Mathf.Max(0.001f, height), _innerSize.z - 0.01f);
                _fillVisual.localPosition = new Vector3(0f, WallThickness + height * 0.5f, 0f);
            }

            for (int i = 0; i < SeamCount; i++) RefreshTape(i);
        }

        private void RefreshTape(int seam)
        {
            Transform strip = seam < _tapeStrips.Length ? _tapeStrips[seam] : null;
            if (strip != null)
            {
                float fraction = !_closed ? 0f : _taped[seam] ? 1f : _coverage[seam].Fraction;
                strip.gameObject.SetActive(fraction > 0f);
                Vector3 scale = strip.localScale;
                scale.x = Mathf.Max(0.001f, fraction * (seam == SeamCentre ? _innerSize.x : _innerSize.z));
                strip.localScale = scale;
            }

            GameObject guide = seam < _seamGuides.Length ? _seamGuides[seam] : null;
            if (guide != null) guide.SetActive(_closed && !_taped[seam]);
        }

        private void Say(string message, bool good)
        {
            if (_feedback != null) _feedback.Show(message, good);
        }

        private void RaiseChanged() => Changed?.Invoke(this);
    }
}
