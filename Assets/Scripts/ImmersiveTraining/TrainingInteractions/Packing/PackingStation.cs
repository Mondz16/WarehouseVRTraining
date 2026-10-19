using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Valari.Managers;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>One customer order packed at the station.</summary>
    [Serializable]
    public class PackingOrder
    {
        public string Id = "ORD-0000";

        [Tooltip("Shown on the order panel above the bench.")]
        [TextArea(3, 8)] public string PanelText;

        [Tooltip("Size of all the order's products packed together, in centimetres (any orientation).")]
        public Vector3 PackedSizeCm = new Vector3(20f, 10f, 10f);

        [Tooltip("Products spawned on the bench's display slots when the order starts.")]
        public PackableItem[] ItemPrefabs = new PackableItem[0];
    }

    /// <summary>A rack slot that always holds one carton of its size.</summary>
    [Serializable]
    public class CartonSupply
    {
        public CartonSize Size;
        public PackingCarton Prefab;
        public Transform Slot;
        [NonSerialized] public PackingCarton Current;
    }

    /// <summary>
    /// The packing bench (M2_03 - M2_08). Runs the orders, keeps the carton rack stocked, judges a carton put on the
    /// packing spot with the minimum-fit rule, locks the right one there and turns its progress into challenge
    /// completions: order 1 - box selected (M2_03), base fill / top fill (M2_04), sealed (M2_05), label applied
    /// (M2_06); order 2 - multi-item packed (M2_08). The labelled order-1 box is released so it can be carried to the
    /// conveyor; when the conveyor dispatches it the station starts order 2. Every completion is arm-aware.
    /// When the Packing and Dispatch module (re)starts the whole station resets, including the label, conveyor,
    /// zone signs and QC box.
    /// </summary>
    public class PackingStation : MonoBehaviour
    {
        [SerializeField] private TrainingID _trainingId = TrainingID.PackingAndDispatch;
        [SerializeField] private PackingOrder[] _orders = new PackingOrder[0];

        [Tooltip("Where each order's products are spawned (first product on the first slot).")]
        [SerializeField] private Transform[] _displaySlots = new Transform[0];

        [SerializeField] private CartonSupply[] _supplies = new CartonSupply[0];

        [Header("Packing spot")]
        [Tooltip("Pose a chosen carton is locked to. A carton counts as placed when its base centre is inside the box below (spot-local, centred 0.1 m above the spot).")]
        [SerializeField] private Transform _spot;

        [SerializeField] private Vector3 _spotHalfExtents = new Vector3(0.35f, 0.25f, 0.4f);
        [SerializeField] private float _maxRestSpeed = 0.4f;
        [SerializeField] private float _rejectReturnDelay = 1.5f;

        [Header("UI")]
        [SerializeField] private TMP_Text _orderText;
        [SerializeField] private PackingFeedback _feedback;

        [Header("Station parts reset with the module")]
        [SerializeField] private ShippingLabel _label;
        [SerializeField] private ConveyorLoadZone _conveyor;
        [SerializeField] private MultiCounterGate _layoutGate;
        [SerializeField] private GazeDwellTarget[] _signs = new GazeDwellTarget[0];
        [SerializeField] private QcReview _qcReview;

        [Header("Challenges (direct wiring)")]
        [SerializeField] private ChallengeTutorialTrigger _boxSelectedTrigger;
        [SerializeField] private ChallengeTutorialTrigger _baseFillTrigger;
        [SerializeField] private ChallengeTutorialTrigger _topFillTrigger;
        [SerializeField] private ChallengeTutorialTrigger _sealedTrigger;
        [SerializeField] private ChallengeTutorialTrigger _labelAppliedTrigger;
        [SerializeField] private ChallengeTutorialTrigger _multiItemTrigger;

        private readonly ArmedLatch _boxSelectedLatch = new ArmedLatch();
        private readonly ArmedLatch _baseFillLatch = new ArmedLatch();
        private readonly ArmedLatch _topFillLatch = new ArmedLatch();
        private readonly ArmedLatch _sealedLatch = new ArmedLatch();
        private readonly ArmedLatch _labelAppliedLatch = new ArmedLatch();
        private readonly ArmedLatch _multiItemLatch = new ArmedLatch();
        private readonly List<PackingCarton> _cartons = new List<PackingCarton>();
        private readonly List<PackableItem> _items = new List<PackableItem>();
        private readonly HashSet<PackingCarton> _returning = new HashSet<PackingCarton>();

        // The carton locked for each order; kept after dispatch so its completions can still be offered.
        private PackingCarton[] _orderCartons = new PackingCarton[0];
        private PackingCarton _active;
        private int _orderIndex = -1;

        public int OrderIndex => _orderIndex;
        public PackingCarton ActiveCarton => _active;
        public Transform Spot => _spot;
        public PackingFeedback Feedback => _feedback;
        public PackingOrder CurrentOrder => _orderIndex >= 0 && _orderIndex < _orders.Length ? _orders[_orderIndex] : null;

        public PackingCarton SupplyCarton(CartonSize size)
        {
            foreach (CartonSupply supply in _supplies)
                if (supply.Size == size) return supply.Current;
            return null;
        }

        public Transform SupplySlot(CartonSize size)
        {
            foreach (CartonSupply supply in _supplies)
                if (supply.Size == size) return supply.Slot;
            return null;
        }

        /// <summary>Products of the current order that are not packed yet.</summary>
        public List<PackableItem> LooseOrderItems()
        {
            var loose = new List<PackableItem>();
            string id = CurrentOrder != null ? CurrentOrder.Id : null;
            foreach (PackableItem item in _items)
                if (item != null && !item.IsPacked && item.OrderId == id) loose.Add(item);
            return loose;
        }

        private void Awake()
        {
            _orderCartons = new PackingCarton[_orders.Length];
            TutorialManager.OnTrainingStartedEvent -= HandleTrainingStarted;
            TutorialManager.OnTrainingStartedEvent += HandleTrainingStarted;
            foreach (ChallengeTutorialTrigger trigger in Triggers())
                if (trigger != null) trigger.Armed += HandleArmed;
        }

        private void OnDestroy()
        {
            TutorialManager.OnTrainingStartedEvent -= HandleTrainingStarted;
            foreach (ChallengeTutorialTrigger trigger in Triggers())
                if (trigger != null) trigger.Armed -= HandleArmed;
        }

        private IEnumerable<ChallengeTutorialTrigger> Triggers()
        {
            yield return _boxSelectedTrigger;
            yield return _baseFillTrigger;
            yield return _topFillTrigger;
            yield return _sealedTrigger;
            yield return _labelAppliedTrigger;
            yield return _multiItemTrigger;
        }

        private IEnumerable<ArmedLatch> Latches()
        {
            yield return _boxSelectedLatch;
            yield return _baseFillLatch;
            yield return _topFillLatch;
            yield return _sealedLatch;
            yield return _labelAppliedLatch;
            yield return _multiItemLatch;
        }

        private void Start()
        {
            StockSupplies(); // the rack looks stocked before the module starts
            ShowOrderText();
        }

        private void HandleTrainingStarted(TrainingID id)
        {
            if (id != _trainingId) return;
            ResetStation();
            BeginOrder(0);
        }

        private void ResetStation()
        {
            StopAllCoroutines();
            _returning.Clear();
            if (_active != null) _active.Changed -= HandleCartonChanged;
            _active = null;
            for (int i = 0; i < _orderCartons.Length; i++) _orderCartons[i] = null;

            foreach (PackingCarton carton in _cartons)
                if (carton != null) Destroy(carton.gameObject);
            _cartons.Clear();
            foreach (CartonSupply supply in _supplies) supply.Current = null;

            foreach (PackableItem item in _items)
                if (item != null) Destroy(item.gameObject);
            _items.Clear();

            foreach (ArmedLatch latch in Latches()) latch.Reset();
            StockSupplies();

            if (_label != null) _label.ResetLabel();
            if (_conveyor != null) _conveyor.ResetZone();
            if (_layoutGate != null) _layoutGate.ResetGate();
            foreach (GazeDwellTarget sign in _signs)
                if (sign != null) sign.Activate();
            if (_qcReview != null) _qcReview.ResetReview();
        }

        private void StockSupplies()
        {
            foreach (CartonSupply supply in _supplies)
                if (supply.Current == null) Spawn(supply);
        }

        private void Spawn(CartonSupply supply)
        {
            if (supply.Prefab == null || supply.Slot == null) return;
            PackingCarton carton = Instantiate(supply.Prefab, supply.Slot.position, supply.Slot.rotation, transform);
            carton.name = supply.Prefab.name;
            supply.Current = carton;
            _cartons.Add(carton);
        }

        private CartonSupply SupplyOf(PackingCarton carton)
        {
            foreach (CartonSupply supply in _supplies)
                if (supply.Current == carton) return supply;
            return null;
        }

        private void BeginOrder(int index)
        {
            _orderIndex = index;
            PackingOrder order = CurrentOrder;
            if (order != null)
            {
                for (int i = 0; i < order.ItemPrefabs.Length; i++)
                {
                    PackableItem prefab = order.ItemPrefabs[i];
                    if (prefab == null) continue;
                    Transform slot = i < _displaySlots.Length && _displaySlots[i] != null ? _displaySlots[i] : transform;
                    PackableItem item = Instantiate(prefab, slot.position, slot.rotation, transform);
                    item.name = prefab.name;
                    item.OrderId = order.Id;
                    _items.Add(item);
                }
            }

            ShowOrderText();
            Offer();
        }

        private void ShowOrderText()
        {
            if (_orderText == null) return;
            PackingOrder order = CurrentOrder;
            if (order != null) _orderText.text = order.PanelText;
            else if (_orderIndex < 0) _orderText.text = "PACKING STATION\nWaiting for the next order.";
            else _orderText.text = "All orders packed.\nStation clear.";
        }

        private void Update()
        {
            if (_active != null || CurrentOrder == null) return;
            foreach (PackingCarton carton in _cartons)
            {
                if (!IsOnSpot(carton)) continue;
                Judge(carton); // may restock the rack (changes _cartons): stop iterating
                break;
            }
        }

        private bool IsOnSpot(PackingCarton carton)
        {
            if (carton == null || !carton.isActiveAndEnabled || carton.IsLocked || carton.IsOnConveyor || carton.IsHeld) return false;
            if (_returning.Contains(carton) || _spot == null) return false;

            Rigidbody body = carton.Body;
            if (body != null && !body.isKinematic && body.linearVelocity.magnitude > _maxRestSpeed) return false;

            Vector3 p = _spot.InverseTransformPoint(carton.transform.position);
            var region = new BoxRegion(new Vec3(0f, 0.1f, 0f), new Vec3(_spotHalfExtents.x, _spotHalfExtents.y, _spotHalfExtents.z));
            return region.Contains(new Vec3(p.x, p.y, p.z));
        }

        private void Judge(PackingCarton carton)
        {
            PackingOrder order = CurrentOrder;
            var packed = new Size3(order.PackedSizeCm.x, order.PackedSizeCm.y, order.PackedSizeCm.z);
            switch (CartonFit.Judge(carton.Size, packed))
            {
                case CartonVerdict.Correct:
                    Lock(carton);
                    return;
                case CartonVerdict.TooSmall:
                    Say($"{carton.Size} carton is too small: this order packs to {packed} cm and needs 5 cm of void fill on every side.", false);
                    break;
                default:
                    Say($"{carton.Size} carton is too big: use the smallest box that fits with 5 cm clearance. Oversized boxes waste material, cost dimensional weight and let items shift.", false);
                    break;
            }

            StartCoroutine(ReturnToRack(carton));
        }

        private void Lock(PackingCarton carton)
        {
            CartonSupply supply = SupplyOf(carton);
            carton.LockAt(_spot.position, _spot.rotation);
            carton.Configure(CurrentOrder.Id, ItemCount(CurrentOrder), _feedback);
            _active = carton;
            if (_orderIndex < _orderCartons.Length) _orderCartons[_orderIndex] = carton;
            carton.Changed += HandleCartonChanged;

            if (supply != null)
            {
                supply.Current = null;
                Spawn(supply); // restock the rack
            }

            Say($"{carton.Size} carton - correct: the smallest box with 5 cm clearance all round.", true);
            Offer();
        }

        private static int ItemCount(PackingOrder order)
        {
            int count = 0;
            foreach (PackableItem prefab in order.ItemPrefabs)
                if (prefab != null) count++;
            return count;
        }

        private IEnumerator ReturnToRack(PackingCarton carton)
        {
            _returning.Add(carton);
            yield return new WaitForSeconds(_rejectReturnDelay);
            _returning.Remove(carton);

            CartonSupply supply = SupplyOf(carton);
            if (carton == null || carton.IsHeld || carton.IsLocked || supply == null || supply.Slot == null) yield break;

            carton.transform.SetPositionAndRotation(supply.Slot.position, supply.Slot.rotation);
            Rigidbody body = carton.Body;
            if (body == null || body.isKinematic) yield break;
            body.position = supply.Slot.position;
            body.rotation = supply.Slot.rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        private void HandleCartonChanged(PackingCarton carton)
        {
            // The labelled first order is carried to the conveyor by hand.
            if (_orderCartons.Length > 0 && carton == _orderCartons[0] && carton.IsLabelled && carton.IsLocked)
                carton.Unlock();
            Offer();
        }

        /// <summary>Called by the conveyor once it has taken a carton out to the dock.</summary>
        public void HandleDispatched(PackingCarton carton)
        {
            if (carton == null || carton != _active) return;

            carton.Changed -= HandleCartonChanged;
            _active = null;
            if (_orderIndex + 1 < _orders.Length)
            {
                BeginOrder(_orderIndex + 1);
                Say("Box dispatched. The next order is on the panel.", true);
            }
            else
            {
                _orderIndex = _orders.Length;
                ShowOrderText();
            }
        }

        private void HandleArmed(ChallengeTutorialTrigger trigger) => Offer();

        private void Offer()
        {
            PackingCarton first = _orderCartons.Length > 0 ? _orderCartons[0] : null;
            PackingCarton second = _orderCartons.Length > 1 ? _orderCartons[1] : null;
            Complete(_boxSelectedLatch, _boxSelectedTrigger, first != null);
            Complete(_baseFillLatch, _baseFillTrigger, first != null && first.IsBaseFilled);
            Complete(_topFillLatch, _topFillTrigger, first != null && first.IsFull);
            Complete(_sealedLatch, _sealedTrigger, first != null && first.IsSealed);
            Complete(_labelAppliedLatch, _labelAppliedTrigger, first != null && first.IsLabelled);
            Complete(_multiItemLatch, _multiItemTrigger, second != null && second.IsFull);
        }

        private static void Complete(ArmedLatch latch, ChallengeTutorialTrigger trigger, bool condition) =>
            latch.Offer(condition, () => trigger == null || trigger.TryComplete());

        private void Say(string message, bool good)
        {
            if (_feedback != null) _feedback.Show(message, good);
        }

        private void OnDrawGizmosSelected()
        {
            if (_spot == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.matrix = _spot.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, 0.1f, 0f), _spotHalfExtents * 2f);
        }
    }
}
