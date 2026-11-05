#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JbelIghoud.Lithic.Editor
{
    [InitializeOnLoad]
    public static class RockSystemSetupWizard
    {
        private const string ResourcesPath = "Assets/Resources/Rocks";
        private const string MatrixPath = "Assets/Resources/DefaultRockInteractionMatrix.asset";
        private const string RockFbxPath = "Assets/Envirement/rock.fbx";

        static RockSystemSetupWizard()
        {
            EditorApplication.delayCall += EnsureDefaultAssetsExist;
        }

        public static void EnsureDefaultAssetsExist()
        {
            GenerateDefaultRockDatabase(silent: true);
        }

        [MenuItem("Jbel Ighoud/Generate Prehistoric Rock Database", false, 1)]
        public static void MenuGenerateDefaultRockDatabase()
        {
            GenerateDefaultRockDatabase(silent: false);
        }

        [MenuItem("Jbel Ighoud/Put All Rock Types Next To Each Other in Scene", false, 2)]
        public static void MenuSpawnAllRocksInScene()
        {
            SpawnAllRocksInScene(silent: false);
        }

        public static void GenerateDefaultRockDatabase(bool silent = false)
        {
            if (!Directory.Exists("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            if (!Directory.Exists(ResourcesPath))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "Rocks");
            }

            // Load 3D meshes from rock.fbx
            Mesh meshRock1 = null;
            Mesh meshRock2 = null;
            Mesh meshRock3 = null;
            Object[] fbxAssets = AssetDatabase.LoadAllAssetsAtPath(RockFbxPath);
            foreach (var a in fbxAssets)
            {
                if (a is Mesh m)
                {
                    if (m.name == "rock.001") meshRock1 = m;
                    else if (m.name == "rock.002") meshRock2 = m;
                    else if (m.name == "rock.003") meshRock3 = m;
                }
            }

            // Clean up obsolete rock assets (keeping only 3 rocks in the system)
            string[] obsoleteAssets = new string[]
            {
                $"{ResourcesPath}/Argile_Clay.asset",
                $"{ResourcesPath}/Phosphate_Rock.asset",
                $"{ResourcesPath}/Magnetite.asset",
                $"{ResourcesPath}/Pyrite_IronDisulphide.asset"
            };
            foreach (var obPath in obsoleteAssets)
            {
                if (File.Exists(obPath))
                {
                    AssetDatabase.DeleteAsset(obPath);
                }
            }

            // ========================================================
            // 1. Create RockData ScriptableObjects (Exactly 3 Rocks)
            // ========================================================

            // Rock 1: Silex (Flint) -> Uses mesh rock.001
            RockData silex = CreateOrUpdateRockData("Silex_Flint", data =>
            {
                data.rockName = "Rock 1 - Silex (Flint)";
                data.rockType = RockType.Silex;
                data.hardness = 20.0f; // Tier: 15 – 20 (Max Core)
                data.toughness = 0.75f;
                data.baseDurability = 5;
                data.mass = 1.4f;
                data.tags = RockTags.KnappableCore | RockTags.Hammerstone;
                data.rockColor = new Color(0.28f, 0.28f, 0.30f); // Dark flint grey
                data.residueColor = new Color(0.4f, 0.4f, 0.42f);
                data.rockMesh = meshRock1;
                data.meshScale = new Vector3(48.88f, 48.88f, 48.88f);
                data.description = "Rock 1 - Silex (Flint): Extreme hardness with razor conchoidal cleavage. Produces sharp flakes and spear points.";
                data.historicalNotes = "Central to the Jbel Ighoud Levallois knapping industry (~300,000 BP).";
            });

            // Rock 2: Grès (Sandstone) -> Uses mesh rock.002
            RockData gres = CreateOrUpdateRockData("Gres_Sandstone", data =>
            {
                data.rockName = "Rock 2 - Grès (Sandstone)";
                data.rockType = RockType.Gres;
                data.hardness = 18.0f; // Tier: 15 – 20 (Percussion Hammerstone)
                data.toughness = 0.85f;
                data.baseDurability = 6;
                data.mass = 1.6f;
                data.tags = RockTags.Hammerstone | RockTags.Abrasive;
                data.rockColor = new Color(0.82f, 0.73f, 0.55f); // Sandy tan
                data.residueColor = new Color(0.85f, 0.78f, 0.62f);
                data.rockMesh = meshRock2;
                data.meshScale = new Vector3(35.63f, 35.63f, 35.63f);
                data.description = "Rock 2 - Grès: Hard quartz-cemented stone, highly durable and abrasive. Used as a percuteur (hammerstone).";
                data.historicalNotes = "Essential percussion hammerstone found alongside Levallois cores at Jbel Ighoud.";
            });

            // Rock 3: Calcaire (Limestone) -> Uses mesh rock.003
            RockData calcaire = CreateOrUpdateRockData("Calcaire_Limestone", data =>
            {
                data.rockName = "Rock 3 - Calcaire (Limestone)";
                data.rockType = RockType.Calcaire;
                data.hardness = 7.0f; // Tier: 5 – 10 (Soft/Medium Bedrock)
                data.toughness = 0.4f;
                data.baseDurability = 3;
                data.mass = 1.1f;
                data.tags = RockTags.PigmentSource | RockTags.Brittle;
                data.rockColor = new Color(0.92f, 0.91f, 0.85f); // Creamy chalk white
                data.residueColor = new Color(0.95f, 0.95f, 0.92f);
                data.rockMesh = meshRock3;
                data.meshScale = new Vector3(24.83f, 24.83f, 24.83f);
                data.description = "Rock 3 - Calcaire: Soft-to-medium sedimentary rock, scratches easily and yields chalk powder.";
                data.historicalNotes = "Abundantly forms the karstic cave walls of Jbel Ighoud where early humans sheltered.";
            });

            // ========================================================
            // 2. Create and populate RockInteractionMatrix (6 Pairwise Recipes)
            // ========================================================
            RockInteractionMatrix matrix = AssetDatabase.LoadAssetAtPath<RockInteractionMatrix>(MatrixPath);
            if (matrix == null)
            {
                matrix = ScriptableObject.CreateInstance<RockInteractionMatrix>();
                AssetDatabase.CreateAsset(matrix, MatrixPath);
            }

            matrix.minimumHitForce = 2.0f;
            matrix.hardShatterForce = 6.5f;
            matrix.useHardnessFallback = true;
            matrix.highHardnessDelta = 4.5f;
            matrix.recipes.Clear();

            // Recipe 1: Rock 2 (Grès) + Rock 1 (Silex) -> Levallois Knapping
            matrix.recipes.Add(new RockInteractionRecipe
            {
                recipeName = "Levallois Knapping (Rock 2 Gres + Rock 1 Silex)",
                strikerType = RockType.Gres,
                receiverType = RockType.Silex,
                minImpactForce = 2.5f,
                outcome = InteractionOutcomeType.ProduceFlake,
                damageToStriker = 0,
                damageToReceiver = 1,
                feedbackText = "Knapped a sharp Levallois blade from the Silex core!"
            });

            // Recipe 2: Rock 1 (Silex) + Rock 1 (Silex) -> Flint-on-Flint Percussion
            matrix.recipes.Add(new RockInteractionRecipe
            {
                recipeName = "Flint Knapping (Rock 1 Silex + Rock 1 Silex)",
                strikerType = RockType.Silex,
                receiverType = RockType.Silex,
                minImpactForce = 3.0f,
                outcome = InteractionOutcomeType.ProduceFlake,
                damageToStriker = 1,
                damageToReceiver = 1,
                feedbackText = "Direct flint percussion produced sharp lithic flakes."
            });

            // Recipe 3: Rock 1 (Silex) + Rock 3 (Calcaire) -> Flint Incision / Carving
            matrix.recipes.Add(new RockInteractionRecipe
            {
                recipeName = "Flint Incision (Rock 1 Silex + Rock 3 Calcaire)",
                strikerType = RockType.Silex,
                receiverType = RockType.Calcaire,
                minImpactForce = 2.0f,
                outcome = InteractionOutcomeType.ProducePowder,
                damageToStriker = 0,
                damageToReceiver = 1,
                feedbackText = "Flint sliced into limestone, generating fine white chalk residue."
            });

            // Recipe 4: Rock 2 (Grès) + Rock 3 (Calcaire) -> Sandstone Crushing Limestone
            matrix.recipes.Add(new RockInteractionRecipe
            {
                recipeName = "Limestone Crushing (Rock 2 Gres + Rock 3 Calcaire)",
                strikerType = RockType.Gres,
                receiverType = RockType.Calcaire,
                minImpactForce = 2.4f,
                outcome = InteractionOutcomeType.ProducePowder,
                damageToStriker = 0,
                damageToReceiver = 2,
                feedbackText = "Hard sandstone crushed soft limestone into powder."
            });

            // Recipe 5: Rock 2 (Grès) + Rock 2 (Grès) -> Sandstone Grinding / Abrasion
            matrix.recipes.Add(new RockInteractionRecipe
            {
                recipeName = "Sandstone Abrasion (Rock 2 Gres + Rock 2 Gres)",
                strikerType = RockType.Gres,
                receiverType = RockType.Gres,
                minImpactForce = 2.2f,
                outcome = InteractionOutcomeType.AbrasionGrind,
                damageToStriker = 1,
                damageToReceiver = 1,
                feedbackText = "Abrasive sandstone friction ground the surfaces smooth."
            });

            // Recipe 6: Rock 3 (Calcaire) + Rock 3 (Calcaire) -> Limestone Friction / Chalk
            matrix.recipes.Add(new RockInteractionRecipe
            {
                recipeName = "Limestone Chalk Friction (Rock 3 Calcaire + Rock 3 Calcaire)",
                strikerType = RockType.Calcaire,
                receiverType = RockType.Calcaire,
                minImpactForce = 1.8f,
                outcome = InteractionOutcomeType.ProducePowder,
                damageToStriker = 1,
                damageToReceiver = 1,
                feedbackText = "Limestone blocks rubbed together, yielding chalky white powder."
            });

            EditorUtility.SetDirty(matrix);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!silent)
            {
                EditorUtility.DisplayDialog(
                    "Jbel Ighoud Rock Database Generated",
                    $"Successfully updated Prehistoric Rock Database to 3 Rocks with 3D meshes (rock.001, rock.002, rock.003) and 6 pairwise interaction recipes in {MatrixPath}!\n\n" +
                    "Obsolete rock types were cleaned up.",
                    "OK"
                );
            }

            Selection.activeObject = matrix;
            Debug.Log("<color=#5CD85A><b>[Jbel Ighoud Wizard]</b></color> Generated 3 rock data assets with meshes (rock 1-2-3) and pairwise interaction recipes!");
        }

        /// <summary>
        /// Spawns the 3 rock types using authentic 3D meshes (rock.001, rock.002, rock.003) in front of the player.
        /// </summary>
        public static void SpawnAllRocksInScene(bool silent = false)
        {
            GenerateDefaultRockDatabase(silent: true);

            // Clean up existing container if present
            GameObject existing = GameObject.Find("All_Prehistoric_Rocks");
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            GameObject parent = new GameObject("All_Prehistoric_Rocks");
            Undo.RegisterCreatedObjectUndo(parent, "Spawn 3 Prehistoric Rocks");

            // Position relative to player if found, or near world origin
            Vector3 origin = new Vector3(0f, 0.45f, 2.5f);
            var player = Object.FindAnyObjectByType<TopDownPlayerController>();
            if (player != null)
            {
                Vector3 forward = player.transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
                forward.Normalize();

                origin = player.transform.position + forward * 3.5f;
                origin.y = player.transform.position.y + 0.6f;
            }

            string[] rockAssetNames = new string[]
            {
                "Silex_Flint",          // Rock 1 (mesh: rock.001)
                "Gres_Sandstone",       // Rock 2 (mesh: rock.002)
                "Calcaire_Limestone"    // Rock 3 (mesh: rock.003)
            };

            float spacing = 2.8f;
            float totalWidth = (rockAssetNames.Length - 1) * spacing;
            float startOffset = -totalWidth * 0.5f;

            Vector3 lateralDir = Vector3.right;
            if (player != null)
            {
                lateralDir = Vector3.Cross(Vector3.up, (origin - player.transform.position).normalized);
            }

            for (int i = 0; i < rockAssetNames.Length; i++)
            {
                string path = $"{ResourcesPath}/{rockAssetNames[i]}.asset";
                RockData data = AssetDatabase.LoadAssetAtPath<RockData>(path);
                if (data == null) continue;

                // Create GameObject with 3D Mesh (NOT primitive sphere!)
                GameObject rockObj = new GameObject($"Rock_{i + 1}_{data.rockType}_{data.rockName}");
                rockObj.transform.SetParent(parent.transform);

                MeshFilter mf = rockObj.AddComponent<MeshFilter>();
                if (data.rockMesh != null)
                {
                    mf.sharedMesh = data.rockMesh;
                }

                MeshRenderer rend = rockObj.AddComponent<MeshRenderer>();
                if (data.rockMaterial != null)
                {
                    rend.sharedMaterial = data.rockMaterial;
                }
                else
                {
                    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? rend.sharedMaterial.shader);
                    mat.name = $"{data.rockName}_Material";
                    mat.color = data.rockColor;
                    rend.material = mat;
                }

                // Add convex MeshCollider matching the rock geometry
                MeshCollider mc = rockObj.AddComponent<MeshCollider>();
                if (data.rockMesh != null)
                {
                    mc.sharedMesh = data.rockMesh;
                }
                mc.convex = true;

                // Scale and position
                rockObj.transform.localScale = data.meshScale;
                Vector3 rockPos = origin + lateralDir * (startOffset + i * spacing);
                rockObj.transform.position = rockPos;

                // Setup Rigidbody for physical collision
                Rigidbody rb = rockObj.AddComponent<Rigidbody>();
                rb.mass = Mathf.Max(0.5f, data.mass);
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

                // Add RockItem
                RockItem rockItem = rockObj.AddComponent<RockItem>();
                rockItem.rockData = data;
                rockItem.strength = data.hardness;
                rockItem.SetupDurability();

                // Add Rock component
                Rock lithicRock = rockObj.AddComponent<Rock>();
                lithicRock.SetRockData(data);
            }

            Selection.activeGameObject = parent;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            if (!silent)
            {
                EditorUtility.DisplayDialog(
                    "3 Prehistoric Rocks Placed",
                    $"Successfully placed the 3 prehistoric rock meshes (rock.001, rock.002, rock.003) side-by-side in your scene!\n\n" +
                    "They are grouped under '[All_Prehistoric_Rocks]' right in front of the player.",
                    "OK"
                );
            }

            Debug.Log("<color=#5CD85A><b>[Jbel Ighoud]</b></color> Placed 3 prehistoric rock meshes (rock 1-2-3) side-by-side in the scene!");
        }

        private static RockData CreateOrUpdateRockData(string assetName, System.Action<RockData> configure)
        {
            string path = $"{ResourcesPath}/{assetName}.asset";
            RockData asset = AssetDatabase.LoadAssetAtPath<RockData>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<RockData>();
                configure(asset);
                AssetDatabase.CreateAsset(asset, path);
            }
            else
            {
                configure(asset);
                EditorUtility.SetDirty(asset);
            }
            return asset;
        }
    }
}
#endif
