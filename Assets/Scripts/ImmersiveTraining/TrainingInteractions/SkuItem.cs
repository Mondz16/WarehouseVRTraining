using UnityEngine;

namespace ImmersiveTraining.TrainingInteractions
{
    /// <summary>
    /// Product identity carried by a pickable order-picking item.
    /// The scanner (M1_04) compares <see cref="Sku"/> against the active pick-list line;
    /// the tote (M1_05 / M1_07) counts and validates items by the same code.
    /// Keep <see cref="Sku"/> equal to the sibling <see cref="TriggerIdentity"/> Id so
    /// physical zone matching and logical SKU checks stay in sync.
    /// </summary>
    public class SkuItem : MonoBehaviour
    {
        [SerializeField] private string _sku;
        [SerializeField] [TextArea] private string _description;

        public string Sku => _sku;
        public string Description => _description;
    }
}
