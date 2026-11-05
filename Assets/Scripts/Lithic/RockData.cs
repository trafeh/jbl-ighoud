using UnityEngine;

namespace JbelIghoud.Lithic
{
    public enum RockType
    {
        Rock1 = 0,
        Silex = 0,                  // Flint (High hardness, Mohs ~7) - conchoidal fracture, produces razor-sharp flakes/blades
        Flint = 0,

        Rock2 = 1,
        Gres = 1,                   // Sandstone (Medium/Hard abrasive, Mohs ~6) - percussive hammerstone / grinding stone
        Sandstone = 1,

        Rock3 = 2,
        Calcaire = 2,               // Limestone (Soft/Medium, Mohs ~3) - chalky, marks surfaces, white powder
        Limestone = 2,

        // Legacy / extended types
        Argile = 3,                 // Clay (Very soft, Mohs ~1.5)
        Clay = 3,
        Phosphate = 4,              // Phosphate (Brittle, Mohs ~4.5)
        Pyrite = 5,                 // Iron Disulphide (Mohs ~6-6.5)
        Magnetite = 6,              // Lodestone/Iron oxide (Mohs ~6)
        Custom = 7
    }

    [System.Flags]
    public enum RockTags
    {
        None            = 0,
        Hammerstone     = 1 << 0, // Percussion tool (used to strike other stones)
        KnappableCore   = 1 << 1, // Can yield sharp flakes/blades (Levallois technique)
        Abrasive        = 1 << 2, // Used for grinding, polishing, or sharpening
        PigmentSource   = 1 << 3, // Can be ground into powder or used to draw/mark
        Brittle         = 1 << 4, // Low structural toughness; shatters easily under force
        FossilBearing   = 1 << 5, // May drop fossilized shark teeth or bone fragments when broken
        MalleableClump  = 1 << 6, // Clay paste/clump (used for shaping/pottery/sealing)
        FireStarter     = 1 << 7, // Produces sparks when struck with high-hardness flint
        Magnetic        = 1 << 8  // Natural magnetic attraction
    }

    [CreateAssetMenu(fileName = "RockData_", menuName = "Jbel Ighoud/Rock Data", order = 10)]
    public class RockData : ScriptableObject
    {
        [Header("General Identity")]
        [Tooltip("Display name of the rock (e.g., 'Silex (Flint)').")]
        public string rockName = "Unnamed Rock";

        [Tooltip("Primary classification category.")]
        public RockType rockType = RockType.Silex;

        [Tooltip("Functional gameplay and interaction flags.")]
        public RockTags tags = RockTags.None;

        [TextArea(2, 4)]
        [Tooltip("In-game description for inventory or tooltips.")]
        public string description;

        [TextArea(2, 4)]
        [Tooltip("Historical/archaeological context regarding Jbel Ighoud lithic industry.")]
        public string historicalNotes;

        [Header("Physical Attributes")]
        [Range(1f, 20f)]
        [Tooltip("Hardness / Strength value (1-5: Very Soft, 5-10: Soft/Brittle, 10-15: Hard/Abrasive, 15-20: Tool Core/Pyrophoric).")]
        public float hardness = 10f;

        [Range(0.1f, 1f)]
        [Tooltip("Fracture toughness. Lower values shatter into debris easily; higher values resist shattering.")]
        public float toughness = 0.7f;

        [Range(1, 20)]
        [Tooltip("Base durability / hits before the core or rock is depleted or destroyed.")]
        public int baseDurability = 3;

        [Tooltip("Physical weight in kg (configures Rigidbody mass).")]
        public float mass = 1.2f;

        [Header("Visual & Color Properties")]
        [Tooltip("Representative tint for greyboxing and procedural visualization.")]
        public Color rockColor = Color.gray;

        [Tooltip("Optional custom material for textured rock rendering.")]
        public Material rockMaterial;

        [Header("3D Mesh Configuration")]
        [Tooltip("Authentic 3D mesh for the rock model (e.g. rock.001, rock.002, rock.003).")]
        public Mesh rockMesh;

        [Tooltip("Base local scale applied to the mesh when instantiated.")]
        public Vector3 meshScale = new Vector3(20f, 20f, 20f);

        [Tooltip("Color of debris, dust particles, or chalk residue spawned from this rock.")]
        public Color residueColor = new Color(0.8f, 0.8f, 0.8f);

        [Header("Crafting & Destruction Prefabs")]
        [Tooltip("Prefab spawned when flaked/knapped (e.g. sharp blade or flake).")]
        public GameObject flakePrefab;

        [Tooltip("Prefab spawned when ground/crushed (e.g. powder pile, clay paste, chalk mark).")]
        public GameObject powderResiduePrefab;

        [Tooltip("Prefab spawned when rock shatters into generic debris.")]
        public GameObject debrisPrefab;

        [Tooltip("Special loot drop (e.g. fossil shark tooth for phosphate, pyrite nugget).")]
        public GameObject specialDropPrefab;

        [Range(0f, 1f)]
        [Tooltip("Probability of dropping special drop on fracture/shatter.")]
        public float specialDropChance = 0.35f;

        [Header("Audio Feedback")]
        [Tooltip("Clips played when struck (knap clink, dull thud, grinding crunch).")]
        public AudioClip[] strikeClips;

        [Tooltip("Clip played when the rock breaks or shatters.")]
        public AudioClip shatterClip;

        /// <summary>
        /// Quick helper to check if this rock possesses specific functional tags.
        /// </summary>
        public bool HasTag(RockTags tag)
        {
            return (tags & tag) != 0;
        }

        /// <summary>
        /// Returns the 1-5, 5-10, 10-15, 15-20 tier string based on real life hardness.
        /// </summary>
        public string GetTierCategory()
        {
            if (hardness <= 5f) return "1-5 (Very Soft)";
            if (hardness <= 10f) return "5-10 (Soft/Brittle)";
            if (hardness <= 15f) return "10-15 (Hard/Abrasive)";
            return "15-20 (Extremely Hard/Tool Core)";
        }
    }
}
