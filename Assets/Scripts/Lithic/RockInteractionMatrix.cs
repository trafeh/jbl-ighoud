using System;
using System.Collections.Generic;
using UnityEngine;

namespace JbelIghoud.Lithic
{
    public enum InteractionOutcomeType
    {
        NoEffect,         // Insufficient force or elastic bounce
        ProduceFlake,     // Knapping: detaches a sharp Levallois blade/flake
        ProducePowder,    // Abrasive grinding/crushing produces pigment or chalk dust
        ProduceClayPaste, // Argile crumbles into malleable paste/clump
        Shatter,          // Rock breaks into fractured debris chunks
        ExtractFossil,    // Phosphate breaks open, revealing fossilized tooth/bone
        StrikeSparks,     // Pyrite struck by flint creates fire-starting sparks
        AbrasionGrind     // Sandstone friction polishes or shapes material
    }

    [Serializable]
    public struct InteractionResult
    {
        public InteractionOutcomeType outcomeType;
        public int damageToStriker;
        public int damageToReceiver;
        public GameObject spawnedYieldPrefab;
        public GameObject spawnedVFXPrefab;
        public AudioClip sfxClip;
        public string feedbackMessage;

        public static InteractionResult None => new InteractionResult
        {
            outcomeType = InteractionOutcomeType.NoEffect,
            damageToStriker = 0,
            damageToReceiver = 0,
            feedbackMessage = "Dull strike, no effect."
        };
    }

    [Serializable]
    public class RockInteractionRecipe
    {
        [Tooltip("Recipe label for quick identification in the Inspector.")]
        public string recipeName;

        [Header("Inputs")]
        [Tooltip("The rock performing the strike (Hammer / Moving stone).")]
        public RockType strikerType = RockType.Gres;

        [Tooltip("The rock receiving the strike (Anvil / Core / Target stone).")]
        public RockType receiverType = RockType.Silex;

        [Range(1f, 15f)]
        [Tooltip("Minimum impact velocity/force required to trigger this recipe.")]
        public float minImpactForce = 3.0f;

        [Header("Outcome & Durability Costs")]
        public InteractionOutcomeType outcome = InteractionOutcomeType.ProduceFlake;

        [Tooltip("Damage dealt to striker rock durability.")]
        public int damageToStriker = 0;

        [Tooltip("Damage dealt to receiver rock durability.")]
        public int damageToReceiver = 1;

        [Header("Yields & Spawns")]
        [Tooltip("Custom item prefab spawned upon successful interaction (e.g. Flint Flake, Fossil, Clay Clump).")]
        public GameObject yieldPrefab;

        [Tooltip("Visual effect prefab (e.g. spark bursts, chalk dust puff).")]
        public GameObject vfxPrefab;

        [Tooltip("Audio clip override for this specific combination.")]
        public AudioClip sfxClip;

        [TextArea(1, 2)]
        [Tooltip("Message displayed in greybox HUD or console.")]
        public string feedbackText = "Knapped a sharp flake!";

        /// <summary>
        /// Checks if this recipe matches the pair. If allowBiDirectional is true, order doesn't matter.
        /// </summary>
        public bool Matches(RockType striker, RockType receiver, float impactForce, bool allowBiDirectional, out bool isInverted)
        {
            isInverted = false;
            if (impactForce < minImpactForce) return false;

            if (striker == strikerType && receiver == receiverType) return true;

            if (allowBiDirectional && striker == receiverType && receiver == strikerType)
            {
                isInverted = true;
                return true;
            }

            return false;
        }

        public bool Matches(RockType striker, RockType receiver, float impactForce, bool allowBiDirectional = true)
        {
            return Matches(striker, receiver, impactForce, allowBiDirectional, out _);
        }
    }

    [CreateAssetMenu(fileName = "RockInteractionMatrix", menuName = "Jbel Ighoud/Rock Interaction Matrix", order = 11)]
    public class RockInteractionMatrix : ScriptableObject
    {
        [Header("Impact Thresholds")]
        [Tooltip("Global minimum velocity/force for any rock collision to register as an impact.")]
        public float minimumHitForce = 2.0f;

        [Tooltip("Threshold where force alone causes softer brittle rocks to shatter completely.")]
        public float hardShatterForce = 7.0f;

        [Header("Specific Cultural & Lithic Recipes")]
        [Tooltip("Authored stone-on-stone recipes (takes precedence over generic hardness comparisons).")]
        public List<RockInteractionRecipe> recipes = new List<RockInteractionRecipe>();

        [Header("Generic Hardness Fallback Rules")]
        [Tooltip("If no explicit recipe matches, outcomes are calculated using relative Mohs hardness.")]
        public bool useHardnessFallback = true;

        [Tooltip("Difference in hardness required for the harder rock to chip/crush the softer rock without receiving damage.")]
        public float highHardnessDelta = 2.5f;

        /// <summary>
        /// Evaluates a strike between two rocks and returns the resulting interaction.
        /// </summary>
        public InteractionResult ResolveCollision(Rock striker, Rock receiver, float impactForce)
        {
            if (striker == null || receiver == null || striker.Data == null || receiver.Data == null)
            {
                return InteractionResult.None;
            }

            return ResolveCollision(striker.Data, receiver.Data, impactForce);
        }

        /// <summary>
        /// Evaluates a strike directly using RockData references.
        /// </summary>
        public InteractionResult ResolveCollision(RockData strikerData, RockData receiverData, float impactForce)
        {
            if (strikerData == null || receiverData == null)
            {
                return InteractionResult.None;
            }

            if (impactForce < minimumHitForce)
            {
                return InteractionResult.None;
            }

            // 1. Check Explicit Authored Recipes first (bidirectional)
            RockType strikerType = strikerData.rockType;
            RockType receiverType = receiverData.rockType;

            for (int i = 0; i < recipes.Count; i++)
            {
                RockInteractionRecipe recipe = recipes[i];
                if (recipe != null && recipe.Matches(strikerType, receiverType, impactForce, true, out bool isInverted))
                {
                    int dmgStriker = isInverted ? recipe.damageToReceiver : recipe.damageToStriker;
                    int dmgReceiver = isInverted ? recipe.damageToStriker : recipe.damageToReceiver;
                    RockData yieldSource = isInverted ? strikerData : receiverData;

                    return new InteractionResult
                    {
                        outcomeType = recipe.outcome,
                        damageToStriker = dmgStriker,
                        damageToReceiver = dmgReceiver,
                        spawnedYieldPrefab = recipe.yieldPrefab != null ? recipe.yieldPrefab : GetFallbackPrefab(recipe.outcome, yieldSource),
                        spawnedVFXPrefab = recipe.vfxPrefab,
                        sfxClip = recipe.sfxClip,
                        feedbackMessage = recipe.feedbackText
                    };
                }
            }

            // 2. Generic Physics & Mohs Hardness Comparison Fallback
            if (useHardnessFallback)
            {
                return ResolveGenericHardnessInteraction(strikerData, receiverData, impactForce);
            }

            return InteractionResult.None;
        }

        private InteractionResult ResolveGenericHardnessInteraction(RockData strikerData, RockData receiverData, float force)
        {
            float deltaHardness = strikerData.hardness - receiverData.hardness;

            // Scenario A: Striker is substantially harder than receiver (e.g. Silex (7) hitting Calcaire (3) or Argile (1.5))
            if (deltaHardness >= highHardnessDelta)
            {
                // Softer rock gets crushed, flaked, or powdered depending on its tags
                if (receiverData.HasTag(RockTags.MalleableClump) || receiverData.rockType == RockType.Argile)
                {
                    return new InteractionResult
                    {
                        outcomeType = InteractionOutcomeType.ProduceClayPaste,
                        damageToStriker = 0,
                        damageToReceiver = 2,
                        spawnedYieldPrefab = receiverData.powderResiduePrefab,
                        feedbackMessage = $"Crushed soft {receiverData.rockName} into malleable paste."
                    };
                }

                if (receiverData.HasTag(RockTags.PigmentSource) || receiverData.rockType == RockType.Calcaire)
                {
                    return new InteractionResult
                    {
                        outcomeType = InteractionOutcomeType.ProducePowder,
                        damageToStriker = 0,
                        damageToReceiver = 1,
                        spawnedYieldPrefab = receiverData.powderResiduePrefab,
                        feedbackMessage = $"Scraped {receiverData.rockName}, producing chalky pigment powder."
                    };
                }

                if (receiverData.HasTag(RockTags.FossilBearing) || receiverData.rockType == RockType.Phosphate)
                {
                    bool gotFossil = UnityEngine.Random.value <= receiverData.specialDropChance;
                    return new InteractionResult
                    {
                        outcomeType = gotFossil ? InteractionOutcomeType.ExtractFossil : InteractionOutcomeType.Shatter,
                        damageToStriker = 0,
                        damageToReceiver = receiverData.baseDurability,
                        spawnedYieldPrefab = gotFossil && receiverData.specialDropPrefab != null ? receiverData.specialDropPrefab : receiverData.debrisPrefab,
                        feedbackMessage = gotFossil ? "Fractured phosphate and discovered a fossil tooth!" : "Phosphate shattered into brittle chunks."
                    };
                }

                // Default high hardness impact: softer rock takes heavy damage
                int dmg = force >= hardShatterForce ? receiverData.baseDurability : 1;
                return new InteractionResult
                {
                    outcomeType = dmg >= receiverData.baseDurability ? InteractionOutcomeType.Shatter : InteractionOutcomeType.ProducePowder,
                    damageToStriker = 0,
                    damageToReceiver = dmg,
                    spawnedYieldPrefab = dmg >= receiverData.baseDurability ? receiverData.debrisPrefab : receiverData.powderResiduePrefab,
                    feedbackMessage = $"{strikerData.rockName} struck and damaged softer {receiverData.rockName}."
                };
            }

            // Scenario B: Striker is softer than receiver (striker takes damage)
            if (deltaHardness <= -highHardnessDelta)
            {
                int dmg = force >= hardShatterForce ? strikerData.baseDurability : 1;
                return new InteractionResult
                {
                    outcomeType = dmg >= strikerData.baseDurability ? InteractionOutcomeType.Shatter : InteractionOutcomeType.NoEffect,
                    damageToStriker = dmg,
                    damageToReceiver = 0,
                    spawnedYieldPrefab = strikerData.debrisPrefab,
                    feedbackMessage = $"{strikerData.rockName} fractured against the much harder surface of {receiverData.rockName}."
                };
            }

            // Scenario C: Similar Hardness (both rocks absorb shock)
            // If one is a knappable core (Silex) and the striker is a viable hammerstone:
            if (receiverData.HasTag(RockTags.KnappableCore) && strikerData.HasTag(RockTags.Hammerstone))
            {
                return new InteractionResult
                {
                    outcomeType = InteractionOutcomeType.ProduceFlake,
                    damageToStriker = 0,
                    damageToReceiver = 1,
                    spawnedYieldPrefab = receiverData.flakePrefab,
                    feedbackMessage = $"Percussion knapping detached a sharp flake from {receiverData.rockName}."
                };
            }

            // High impact on similar hardness: both take 1 hit
            int sharedDamage = force >= hardShatterForce ? 2 : 1;
            return new InteractionResult
            {
                outcomeType = force >= hardShatterForce ? InteractionOutcomeType.Shatter : InteractionOutcomeType.NoEffect,
                damageToStriker = sharedDamage,
                damageToReceiver = sharedDamage,
                spawnedYieldPrefab = receiverData.debrisPrefab,
                feedbackMessage = $"Heavy impact between {strikerData.rockName} and {receiverData.rockName}."
            };
        }

        private GameObject GetFallbackPrefab(InteractionOutcomeType outcome, RockData data)
        {
            if (data == null) return null;
            switch (outcome)
            {
                case InteractionOutcomeType.ProduceFlake: return data.flakePrefab;
                case InteractionOutcomeType.ProducePowder:
                case InteractionOutcomeType.ProduceClayPaste: return data.powderResiduePrefab;
                case InteractionOutcomeType.ExtractFossil: return data.specialDropPrefab != null ? data.specialDropPrefab : data.debrisPrefab;
                case InteractionOutcomeType.Shatter: return data.debrisPrefab;
                default: return null;
            }
        }
    }
}
