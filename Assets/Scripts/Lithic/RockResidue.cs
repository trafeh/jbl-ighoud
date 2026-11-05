using UnityEngine;

namespace JbelIghoud.Lithic
{
    public class RockResidue : MonoBehaviour
    {
        [Header("Residue Info")]
        public string itemName = "Lithic Residue";
        public InteractionOutcomeType yieldType = InteractionOutcomeType.ProduceFlake;

        [Header("Visuals")]
        public Color itemColor = Color.white;

        private void Start()
        {
            CleanUpLegacyLabel();
        }

        private void CleanUpLegacyLabel()
        {
            Transform existing = transform.Find("ResidueLabel");
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }
        }
    }
}
