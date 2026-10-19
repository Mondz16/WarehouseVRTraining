using System.Collections.Generic;
using Autohand;
using UnityEngine;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// Holds products in the pushable cart so they ride along with it. Attach to the tote: it shares the
    /// tote's trigger Collider with the <see cref="ToteCounter"/>s and <see cref="MisPickRecovery"/>.
    ///
    /// A <see cref="SkuItem"/> that is inside the trigger, not held and still relative to the cart for the
    /// settle time is locked: a FixedJoint ties it to the cart's kinematic body, so it moves with the cart
    /// in the same physics step and the tote counts never see it leave. The item stays a dynamic body and
    /// the joint keeps collision on, so nothing about its physics pairs (including its contact with the
    /// tote trigger) changes. Grabbing it removes the joint before the hand closes; dropped back in, it
    /// locks again once it settles.
    ///
    /// Products must not be children of the cart: a dynamic body under a moving parent gets dragged by the
    /// hierarchy as well as by physics.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CartCargo : MonoBehaviour
    {
        [Tooltip("The cart's kinematic Rigidbody (on the cart root). Settled items are jointed to it.")]
        [SerializeField] private Rigidbody _cart;

        [Tooltip("Seconds an item must rest in the cart before it locks.")]
        [SerializeField] private float _settleTime = 0.3f;

        private sealed class Item
        {
            public Grabbable Grabbable;
            public Rigidbody Body;
            public int Contacts;                  // colliders of this item currently inside the trigger
            public FixedJoint Joint;
            public RigidbodyInterpolation Interpolation;
            public Vector3 LastLocalPosition;
            public Quaternion LastLocalRotation = Quaternion.identity;
            public readonly SettleTimer Timer = new SettleTimer();

            public bool Locked => Joint != null;
        }

        private readonly Dictionary<Grabbable, Item> _items = new Dictionary<Grabbable, Item>();
        private Collider _trigger;

        /// <summary>Number of items currently locked in the cart.</summary>
        public int LockedCount
        {
            get
            {
                int count = 0;
                foreach (Item item in _items.Values)
                    if (item.Locked) count++;
                return count;
            }
        }

        private void Awake() => _trigger = GetComponent<Collider>();

        private void OnDestroy()
        {
            foreach (Item item in _items.Values)
                if (item.Grabbable != null) item.Grabbable.OnBeforeGrabEvent -= HandleBeforeGrab;
        }

        private void OnTriggerEnter(Collider other)
        {
            Item item = FindItem(other, true);
            if (item != null) item.Contacts++;
        }

        private void OnTriggerExit(Collider other)
        {
            Item item = FindItem(other, false);
            if (item == null || item.Contacts == 0) return;

            item.Contacts--;
            if (item.Contacts == 0 && item.Locked)
                Debug.LogWarning($"#{GetType().Name}# {item.Body.name} left the tote trigger while locked to the cart.", this);
        }

        private void FixedUpdate()
        {
            if (_cart == null) return;

            Quaternion toCart = Quaternion.Inverse(_cart.rotation);
            foreach (Item item in _items.Values)
            {
                if (item.Locked || item.Contacts == 0 || item.Body == null || item.Grabbable == null) continue;

                // Motion relative to the cart, so items dropped into a moving cart still settle.
                Vector3 localPosition = toCart * (item.Body.position - _cart.position);
                Quaternion localRotation = toCart * item.Body.rotation;
                float moved = Vector3.Distance(localPosition, item.LastLocalPosition);
                float turned = Quaternion.Angle(localRotation, item.LastLocalRotation);
                item.LastLocalPosition = localPosition;
                item.LastLocalRotation = localRotation;

                // A hand closing in on the item (grab coroutine running) counts as holding it, so a slow
                // reach can't re-lock the item just before the hand joints it.
                bool held = item.Grabbable.IsHeld() || item.Grabbable.BeingGrabbed();
                if (!item.Timer.Tick(held, moved, turned, Time.fixedDeltaTime)) continue;

                // Contacts can be stale (Unity sends no trigger exit for an item deactivated inside the tote), so
                // only lock what really is inside; anything else keeps waiting.
                if (IsInside(item)) Lock(item);
                else item.Timer.Reset();
            }
        }

        private bool IsInside(Item item)
        {
            Vector3 centre = item.Body.worldCenterOfMass;
            return _trigger == null || _trigger.ClosestPoint(centre) == centre;
        }

        private Item FindItem(Collider other, bool create)
        {
            SkuItem sku = other.GetComponentInParent<SkuItem>();
            if (sku == null) return null;
            Grabbable grabbable = sku.GetComponent<Grabbable>();
            if (grabbable == null) return null;

            if (_items.TryGetValue(grabbable, out Item item)) return item;
            if (!create) return null;

            Rigidbody body = sku.GetComponent<Rigidbody>();
            if (body == null) return null;

            item = new Item { Grabbable = grabbable, Body = body };
            item.Timer.SettleTime = _settleTime;
            _items.Add(grabbable, item);
            grabbable.OnBeforeGrabEvent += HandleBeforeGrab;
            return item;
        }

        private void Lock(Item item)
        {
            item.Interpolation = item.Body.interpolation;
            item.Body.interpolation = RigidbodyInterpolation.Interpolate;   // render as smoothly as the cart

            item.Joint = item.Body.gameObject.AddComponent<FixedJoint>();
            item.Joint.connectedBody = _cart;
            item.Joint.enableCollision = true;   // keep every physics pair exactly as it was

            // Anchor from physics poses: inside FixedUpdate the cart's interpolated Transform can trail its body,
            // which would bake that lag into the joint when an item locks in a moving cart.
            item.Joint.autoConfigureConnectedAnchor = false;
            item.Joint.anchor = Vector3.zero;
            item.Joint.connectedAnchor = Quaternion.Inverse(_cart.rotation) * (item.Body.position - _cart.position);
        }

        // Fires before the hand moves in or joints the item, so the hand never fights the cart joint.
        private void HandleBeforeGrab(Hand hand, Grabbable grabbable)
        {
            if (!_items.TryGetValue(grabbable, out Item item) || !item.Locked) return;

            Destroy(item.Joint);
            item.Joint = null;
            item.Body.interpolation = item.Interpolation;
            item.Timer.Reset();
        }
    }
}
