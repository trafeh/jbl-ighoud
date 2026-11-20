using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using JbelIghoud.UI;
using JbelIghoud.Audio;

namespace JbelIghoud.Game
{
    /// <summary>
    /// Objective chain + XP rewards. Starts tracking once the intro is over (Begin()).
    /// Progress, XP and level are stored in JbelPlayerProfile so CONTINUE resumes where you left.
    /// </summary>
    public class JbelObjectiveManager : MonoBehaviour
    {
        public static JbelObjectiveManager Instance { get; private set; }

        public enum ObjectiveType { CollectStones, ReachTransform, ReachPoint, Scripted }

        [Serializable]
        public class Objective
        {
            [Tooltip("Optional id so scripts (e.g. the Elder guide) can recognise this step.")]
            public string id;
            public string title = "OBJECTIVE";
            public ObjectiveType type;
            [Tooltip("Stones to collect (CollectStones).")]
            public int goal = 1;
            public int xpReward = 25;
            [Tooltip("Target for ReachTransform.")]
            public Transform target;
            [Tooltip("Target for ReachPoint.")]
            public Vector3 point;
            public float reachRadius = 4f;
            [Tooltip("Scripted steps without a target: point the on-screen marker at the nearest stone.")]
            public bool markNearestStone;
            [Tooltip("Extra height for the on-screen marker above the target.")]
            public float markerHeight = 2.4f;
            [Tooltip("Elder lines played (tap to continue) when this objective is completed.")]
            [TextArea(2, 3)] public string[] completionDialogue;
        }

        [Header("--- REFERENCES ---")]
        [SerializeField] private TopDownPlayerController player;
        [SerializeField] private Transform elder;
        [SerializeField] private string elderSpeakerName = "THE ELDER";

        [Header("--- OBJECTIVE CHAIN ---")]
        [SerializeField] private List<Objective> objectives = new List<Objective>();
        [SerializeField] private string allDoneTitle = "THE JOURNEY CONTINUES...";

        [Header("--- SAVE ---")]
        [SerializeField] private float positionSaveInterval = 10f;

        public event Action<Objective, bool> OnObjectiveStarted;        // (objective, isFreshlyStarted)
        public event Action<Objective, int, int, string> OnProgress;      // (objective, progress, goal, label)
        public event Action<Objective, int, int> OnObjectiveCompleted;  // (objective, xp, levelsGained)
        public event Action OnAllCompleted;

        public bool IsActive { get; private set; }
        public Objective Current => (index >= 0 && index < objectives.Count) ? objectives[index] : null;
        public int CurrentIndex => index;
        public string AllDoneTitle => allDoneTitle;

        private int index;
        private int progress;
        private int lastInventoryCount = -1;
        private bool completing;
        private float saveTimer;
        private InventoryManager inventory;

        private void Awake()
        {
            Instance = this;
            if (player == null) player = FindAnyObjectByType<TopDownPlayerController>();
        }

        private void Start()
        {
            // Restore last position when continuing a saved journey
            if (!JbelPlayerProfile.PendingNewGame && JbelPlayerProfile.IntroDone &&
                JbelPlayerProfile.TryGetPosition(out Vector3 pos, out float yaw) && player != null)
            {
                player.TeleportTo(pos + Vector3.up * 0.1f, Quaternion.Euler(0f, yaw, 0f));
            }
        }

        private void OnDestroy()
        {
            if (inventory != null) inventory.OnInventoryChanged -= OnInventoryChanged;
            SavePosition();
            if (Instance == this) Instance = null;
        }

        private void OnApplicationPause(bool paused) { if (paused) SavePosition(); }
        private void OnApplicationQuit() { SavePosition(); }

        /// <summary>Begin tracking. Called by the intro cutscene when it ends, or right away when continuing.</summary>
        public void Begin()
        {
            if (IsActive) return;
            IsActive = true;

            inventory = InventoryManager.Instance != null ? InventoryManager.Instance : FindAnyObjectByType<InventoryManager>();
            if (inventory != null)
            {
                lastInventoryCount = inventory.ItemCount;
                inventory.OnInventoryChanged -= OnInventoryChanged;
                inventory.OnInventoryChanged += OnInventoryChanged;
            }

            index = JbelPlayerProfile.ObjectiveIndex;
            progress = JbelPlayerProfile.ObjectiveProgress;
            bool fresh = progress == 0;
            if (Current != null) OnObjectiveStarted?.Invoke(Current, fresh);
            else OnAllCompleted?.Invoke();
            PushProgress();
        }

        private void Update()
        {
            if (!IsActive || completing || Current == null || player == null) return;

            var o = Current;
            if (o.type == ObjectiveType.ReachTransform || o.type == ObjectiveType.ReachPoint)
            {
                float d = DistanceToTarget();
                PushProgress();
                if (d <= o.reachRadius && !IsBusy()) StartCoroutine(Complete());
            }

            saveTimer += Time.unscaledDeltaTime;
            if (saveTimer >= positionSaveInterval) { saveTimer = 0f; SavePosition(); }
        }

        private bool IsBusy()
        {
            return (JbelDialogueUI.Instance != null && JbelDialogueUI.Instance.IsOpen) || JbelIntroCutscene.IsPlaying;
        }

        private void OnInventoryChanged()
        {
            if (inventory == null) return;
            int count = inventory.ItemCount;
            int delta = count - lastInventoryCount;
            lastInventoryCount = count;

            if (!IsActive || completing || Current == null || Current.type != ObjectiveType.CollectStones || delta <= 0) return;

            progress = Mathf.Min(Current.goal, progress + delta);
            JbelPlayerProfile.SetObjective(index, progress);
            PushProgress();
            if (progress >= Current.goal) StartCoroutine(Complete());
        }

        private void PushProgress()
        {
            var o = Current;
            if (o == null) return;
            if (o.type == ObjectiveType.CollectStones)
                OnProgress?.Invoke(o, progress, o.goal, $"{progress} / {o.goal}");
            else if (o.type == ObjectiveType.Scripted)
                OnProgress?.Invoke(o, progress, Mathf.Max(1, o.goal), scriptedLabel ?? "");
            else
            {
                int m = Mathf.Max(0, Mathf.RoundToInt(DistanceToTarget()));
                OnProgress?.Invoke(o, 0, 1, $"{m} M");
            }
        }

        private float DistanceToTarget()
        {
            var o = Current;
            if (o == null || player == null) return 0f;
            Vector3 t = o.type == ObjectiveType.ReachTransform && o.target != null ? o.target.position : o.point;
            Vector3 p = player.transform.position;
            t.y = p.y = 0f;
            return Vector3.Distance(p, t);
        }

        /// <summary>World position for the on-screen waypoint marker (null = none).</summary>
        public bool TryGetMarkerPosition(out Vector3 world)
        {
            world = Vector3.zero;
            var o = Current;
            if (!IsActive || o == null || player == null) return false;

            switch (o.type)
            {
                case ObjectiveType.ReachTransform:
                    if (o.target == null) return false;
                    world = o.target.position + Vector3.up * o.markerHeight;
                    return true;
                case ObjectiveType.ReachPoint:
                    world = o.point + Vector3.up * o.markerHeight;
                    return true;
                case ObjectiveType.Scripted:
                    if (o.target != null) { world = o.target.position + Vector3.up * o.markerHeight; return true; }
                    if (!o.markNearestStone) return false;
                    var srock = NearestRock();
                    if (srock == null) return false;
                    world = srock.transform.position + Vector3.up * 1.2f;
                    return true;
                case ObjectiveType.CollectStones:
                    var rock = NearestRock();
                    if (rock == null) return false;
                    world = rock.transform.position + Vector3.up * 1.2f;
                    return true;
            }
            return false;
        }

        private RockItem cachedRock;
        private float nextRockSearch;
        private RockItem NearestRock()
        {
            if (Time.unscaledTime < nextRockSearch && cachedRock != null && cachedRock.gameObject.activeInHierarchy && !cachedRock.isHeld) return cachedRock;
            nextRockSearch = Time.unscaledTime + 0.5f;
            cachedRock = null;
            float best = 60f;
            Vector3 p = player.transform.position;
            foreach (var r in FindObjectsByType<RockItem>(FindObjectsSortMode.None))
            {
                if (r == null || !r.gameObject.activeInHierarchy || r.isHeld) continue;
                if (inventory != null && ContainsItem(r)) continue;
                float d = Vector3.Distance(p, r.transform.position);
                if (d < best) { best = d; cachedRock = r; }
            }
            return cachedRock;
        }

        private bool ContainsItem(RockItem r)
        {
            var items = inventory.Items;
            for (int i = 0; i < items.Count; i++) if (items[i] == r) return true;
            return false;
        }

        private IEnumerator Complete()
        {
            if (completing) yield break;
            completing = true;
            var o = Current;

            // Elder reaction (tap to continue) before the reward
            if (o.completionDialogue != null && o.completionDialogue.Length > 0 && JbelDialogueUI.Instance != null)
            {
                if (player != null) player.SetControlsLocked(true);
                var pose = elder != null ? elder.GetComponentInChildren<JbelElderPose>() : null;
                if (pose != null) pose.Talking = true;
                foreach (var line in o.completionDialogue)
                    yield return JbelDialogueUI.Instance.Say(elderSpeakerName, line.Replace("{name}", JbelPlayerProfile.PlayerName));
                JbelDialogueUI.Instance.HideBox();
                if (pose != null) pose.Talking = false;
                if (player != null) player.SetControlsLocked(false);
            }

            int levels = JbelPlayerProfile.AddXP(o.xpReward);
            OnObjectiveCompleted?.Invoke(o, o.xpReward, levels);
            if (GameAudioManager.Instance != null) GameAudioManager.Instance.PlayProceduralClick();

            index++;
            progress = 0;
            JbelPlayerProfile.SetObjective(index, 0);
            if (inventory != null) lastInventoryCount = inventory.ItemCount;

            yield return new WaitForSecondsRealtime(0.25f);
            if (Current != null) OnObjectiveStarted?.Invoke(Current, true);
            else OnAllCompleted?.Invoke();
            PushProgress();
            completing = false;
        }

        // ================================================================== SCRIPTED STEPS
        private string scriptedLabel;

        /// <summary>Id of the current objective (null when none).</summary>
        public string CurrentId => Current != null ? Current.id : null;

        /// <summary>Update the banner for a Scripted objective (progress + free label, e.g. \"2 / 4\").</summary>
        public void SetScriptedProgress(int value, string label)
        {
            var o = Current;
            if (!IsActive || o == null || o.type != ObjectiveType.Scripted) return;
            progress = value;
            scriptedLabel = label;
            PushProgress();
        }

        /// <summary>Complete the current Scripted objective (only if its id matches).</summary>
        public void CompleteScripted(string id)
        {
            var o = Current;
            if (!IsActive || completing || o == null || o.type != ObjectiveType.Scripted || o.id != id) return;
            scriptedLabel = null;
            StartCoroutine(Complete());
        }

        /// <summary>Jump the chain to a given step (used when a scripted sequence restarts).</summary>
        public void ForceIndex(int newIndex)
        {
            index = Mathf.Clamp(newIndex, 0, objectives.Count);
            progress = 0;
            JbelPlayerProfile.SetObjective(index, 0);
        }

        private void SavePosition()
        {
            if (!IsActive || player == null) return;
            JbelPlayerProfile.SavePosition(player.transform.position, player.transform.eulerAngles.y);
        }
    }
}
