using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameSaveManager : MonoBehaviour
{
    public static GameSaveManager Instance { get; private set; }

    [Header("AutoSave Settings (DISABLED)")]
    [Tooltip("In-game autosave has been disabled. Only Unity's editor autosave is active.")]
    [SerializeField] private bool showToastNotification = false;

    [Header("File Settings")]
    [SerializeField] private string saveFileName = "game_autosave.json";

    private HashSet<string> destroyedRocksSet = new HashSet<string>();
    private string toastMessage = "";
    private float toastTimer = 0f;
    private const float ToastDuration = 3.0f;

    public float TimeUntilNextSave => 0f;
    public float AutoSaveInterval => 0f;
    public bool IsAutoSaveEnabled => false;

    private string SaveFilePath => Path.Combine(Application.persistentDataPath, saveFileName);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // In-game autosave and autoload are disabled. Keeping only Unity's Editor AutoSave.
    }

    private void Update()
    {
        // In-game periodic autosave timer removed per user instruction.
        // Only Unity's Editor autosave (EditorAutoSave) remains active.

        // Update toast notification timer
        if (toastTimer > 0f)
        {
            toastTimer -= Time.deltaTime;
        }
        if (toastTimer > 0f)
        {
            toastTimer -= Time.deltaTime;
        }

        // 3. Quick Hotkeys for desktop / testing
        HandleTestingHotkeys();
    }

    private void HandleTestingHotkeys()
    {
        if (Keyboard.current == null) return;

        // F5 = Quick Save
        if (Keyboard.current.f5Key.wasPressedThisFrame)
        {
            SaveGame("Manual QuickSave (F5)");
        }

        // F9 = Quick Load
        if (Keyboard.current.f9Key.wasPressedThisFrame)
        {
            LoadGame();
        }

        // F8 = Delete Save
        if (Keyboard.current.f8Key.wasPressedThisFrame)
        {
            DeleteSaveFile();
        }
    }

    public void NotifyRockDestroyed(string rockName)
    {
        if (!string.IsNullOrEmpty(rockName))
        {
            destroyedRocksSet.Add(rockName);
        }
    }

    public bool HasSaveFile()
    {
        return File.Exists(SaveFilePath);
    }

    public void DeleteSaveFile()
    {
        try
        {
            if (File.Exists(SaveFilePath))
            {
                File.Delete(SaveFilePath);
                ShowToast("🗑️ Save file deleted");
                Debug.Log("[GameSaveManager] Save file deleted.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameSaveManager] Failed to delete save: {ex.Message}");
        }
    }

    public void SaveGame(string reason = "Manual")
    {
        try
        {
            GameSaveData data = new GameSaveData
            {
                saveTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                playTime = Time.time,
                saveReason = reason
            };

            // 1. Capture Player State
            TopDownPlayerController player = FindAnyObjectByType<TopDownPlayerController>();
            if (player != null)
            {
                data.player = new PlayerSaveData
                {
                    position = player.transform.position,
                    rotationEuler = player.transform.rotation.eulerAngles
                };

                if (player.HeldRocks != null)
                {
                    foreach (var heldRock in player.HeldRocks)
                    {
                        if (heldRock != null)
                        {
                            data.player.heldRockNames.Add(heldRock.gameObject.name);
                        }
                    }
                }
            }

            // 2. Capture Rocks State
            RockItem[] allRocks = FindObjectsByType<RockItem>();
            foreach (var rock in allRocks)
            {
                if (rock == null) continue;

                RockSaveData rockData = new RockSaveData
                {
                    name = rock.gameObject.name,
                    position = rock.transform.position,
                    rotationEuler = rock.transform.rotation.eulerAngles,
                    strength = rock.strength,
                    currentHits = rock.HitsRemaining,
                    maxHits = rock.MaxHits,
                    isHeld = rock.isHeld
                };
                data.rocks.Add(rockData);
            }

            // 3. Capture Destroyed Rocks
            data.destroyedRockNames = new List<string>(destroyedRocksSet);

            // Serialize to JSON
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SaveFilePath, json);

            string timeString = DateTime.Now.ToString("HH:mm:ss");
            ShowToast($"💾 Game Auto-Saved [{timeString}]");
            Debug.Log($"<color=#4CD964><b>[GameSaveManager]</b></color> Game saved successfully ({reason}) at {timeString} -> {SaveFilePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameSaveManager] Error saving game: {ex.Message}");
        }
    }

    public bool LoadGame()
    {
        if (!HasSaveFile())
        {
            Debug.LogWarning("[GameSaveManager] No save file found to load.");
            return false;
        }

        try
        {
            string json = File.ReadAllText(SaveFilePath);
            GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);

            if (data == null)
            {
                Debug.LogError("[GameSaveManager] Failed to parse save data JSON.");
                return false;
            }

            // 1. Destroy rocks recorded as destroyed
            if (data.destroyedRockNames != null)
            {
                destroyedRocksSet = new HashSet<string>(data.destroyedRockNames);
                RockItem[] sceneRocks = FindObjectsByType<RockItem>();
                foreach (var rock in sceneRocks)
                {
                    if (rock != null && destroyedRocksSet.Contains(rock.gameObject.name))
                    {
                        Destroy(rock.gameObject);
                    }
                }
            }

            // 2. Restore active rocks state & positions
            Dictionary<string, RockItem> rockMap = new Dictionary<string, RockItem>();
            RockItem[] remainingRocks = FindObjectsByType<RockItem>();
            foreach (var r in remainingRocks)
            {
                if (r != null && !rockMap.ContainsKey(r.gameObject.name))
                {
                    rockMap.Add(r.gameObject.name, r);
                }
            }

            if (data.rocks != null)
            {
                foreach (var rockData in data.rocks)
                {
                    if (rockMap.TryGetValue(rockData.name, out RockItem rock))
                    {
                        if (rock != null)
                        {
                            rock.transform.position = rockData.position;
                            rock.transform.rotation = Quaternion.Euler(rockData.rotationEuler);
                            rock.RestoreState(rockData.strength, rockData.currentHits, rockData.maxHits);
                        }
                    }
                }
            }

            // 3. Restore Player position and held rocks
            TopDownPlayerController player = FindAnyObjectByType<TopDownPlayerController>();
            if (player != null && data.player != null)
            {
                player.TeleportTo(data.player.position, Quaternion.Euler(data.player.rotationEuler));
                player.ClearHeldRocks();

                if (data.player.heldRockNames != null)
                {
                    foreach (string heldName in data.player.heldRockNames)
                    {
                        if (rockMap.TryGetValue(heldName, out RockItem rockToHold) && rockToHold != null)
                        {
                            player.RestoreHeldRock(rockToHold);
                        }
                    }
                }
            }

            ShowToast("✓ Game Progress Loaded");
            Debug.Log($"<color=#64D2FF><b>[GameSaveManager]</b></color> Game loaded successfully from {data.saveTimestamp}.");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameSaveManager] Error loading game: {ex.Message}");
            return false;
        }
    }

    private void ShowToast(string message)
    {
        if (!showToastNotification) return;
        toastMessage = message;
        toastTimer = ToastDuration;
    }

    private void OnGUI()
    {
        if (toastTimer <= 0f || string.IsNullOrEmpty(toastMessage)) return;

        // Calculate fade opacity
        float alpha = 1f;
        if (toastTimer < 0.6f)
        {
            alpha = toastTimer / 0.6f;
        }

        GUI.color = new Color(1f, 1f, 1f, alpha);

        // Toast notification pill at top-center of the screen
        float width = 280f;
        float height = 44f;
        float x = (Screen.width - width) * 0.5f;
        float y = 24f;

        Rect boxRect = new Rect(x, y, width, height);

        // Background styling
        Texture2D bgTexture = Texture2D.whiteTexture;
        Color oldColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.08f, 0.12f, 0.18f, 0.88f * alpha);
        GUI.Box(boxRect, GUIContent.none);
        GUI.backgroundColor = oldColor;

        // Text styling
        GUIStyle textStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 15,
            fontStyle = FontStyle.Bold
        };
        textStyle.normal.textColor = new Color(0.35f, 0.95f, 0.65f, alpha);

        GUI.Label(boxRect, toastMessage, textStyle);
        GUI.color = Color.white;
    }

    // Serializable Data Containers
    [Serializable]
    public class GameSaveData
    {
        public string saveTimestamp;
        public float playTime;
        public string saveReason;
        public PlayerSaveData player = new PlayerSaveData();
        public List<RockSaveData> rocks = new List<RockSaveData>();
        public List<string> destroyedRockNames = new List<string>();
    }

    [Serializable]
    public class PlayerSaveData
    {
        public Vector3 position;
        public Vector3 rotationEuler;
        public List<string> heldRockNames = new List<string>();
    }

    [Serializable]
    public class RockSaveData
    {
        public string name;
        public Vector3 position;
        public Vector3 rotationEuler;
        public float strength;
        public int currentHits;
        public int maxHits;
        public bool isHeld;
    }
}
