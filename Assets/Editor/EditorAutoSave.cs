using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyGame.EditorTools
{
    [InitializeOnLoad]
    public static class EditorAutoSave
    {
        private const string PrefsPrefix = "EditorAutoSave_";
        private const string KeyEnabled = PrefsPrefix + "Enabled";
        private const string KeyInterval = PrefsPrefix + "IntervalMinutes";
        private const string KeySaveAssets = PrefsPrefix + "SaveAssets";
        private const string KeySaveBeforePlay = PrefsPrefix + "SaveBeforePlay";
        private const string KeyLogConfirmations = PrefsPrefix + "LogConfirmations";

        private static double nextSaveTimestamp = 0;
        private static bool isInitialized = false;

        // Settings getters and setters backed by EditorPrefs
        public static bool Enabled
        {
            get => EditorPrefs.GetBool(KeyEnabled, true);
            set
            {
                EditorPrefs.SetBool(KeyEnabled, value);
                ResetTimer();
            }
        }

        public static float IntervalMinutes
        {
            get => EditorPrefs.GetFloat(KeyInterval, 1.0f); // Default 1 minute
            set
            {
                EditorPrefs.SetFloat(KeyInterval, Mathf.Max(0.2f, value));
                ResetTimer();
            }
        }

        public static bool SaveAssets
        {
            get => EditorPrefs.GetBool(KeySaveAssets, true);
            set => EditorPrefs.SetBool(KeySaveAssets, value);
        }

        public static bool SaveBeforePlay
        {
            get => EditorPrefs.GetBool(KeySaveBeforePlay, true);
            set => EditorPrefs.SetBool(KeySaveBeforePlay, value);
        }

        public static bool LogConfirmations
        {
            get => EditorPrefs.GetBool(KeyLogConfirmations, true);
            set => EditorPrefs.SetBool(KeyLogConfirmations, value);
        }

        public static double SecondsUntilNextSave
        {
            get
            {
                if (!Enabled || EditorApplication.isPlayingOrWillChangePlaymode) return 0;
                return Math.Max(0, nextSaveTimestamp - EditorApplication.timeSinceStartup);
            }
        }

        public static double TotalIntervalSeconds => IntervalMinutes * 60.0;

        static EditorAutoSave()
        {
            Initialize();
        }

        public static void Initialize()
        {
            if (isInitialized) return;
            isInitialized = true;

            ResetTimer();

            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static void ResetTimer()
        {
            nextSaveTimestamp = EditorApplication.timeSinceStartup + TotalIntervalSeconds;
        }

        private static void OnEditorUpdate()
        {
            if (!Enabled) return;

            // Do not save while playing, pausing, or transitioning playmode
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying) return;

            // Do not save while compiling
            if (EditorApplication.isCompiling) return;

            if (EditorApplication.timeSinceStartup >= nextSaveTimestamp)
            {
                PerformAutoSave("Interval (1 min)");
                ResetTimer();
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                if (Enabled && SaveBeforePlay)
                {
                    PerformAutoSave("Pre-Play Mode");
                }
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                ResetTimer();
            }
        }

        public static int PerformAutoSave(string reason = "Manual")
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[AutoSave] AutoSave is skipped during Play Mode to protect scene state.");
                return 0;
            }

            int savedScenes = 0;

            // Check and save dirty scenes
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.isDirty && !string.IsNullOrEmpty(scene.path))
                {
                    if (EditorSceneManager.SaveScene(scene))
                    {
                        savedScenes++;
                    }
                }
            }

            // Save dirty assets
            if (SaveAssets)
            {
                AssetDatabase.SaveAssets();
            }

            ResetTimer();

            if (LogConfirmations)
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                if (savedScenes > 0)
                {
                    Debug.Log($"<color=#4CD964><b>[AutoSave]</b></color> Saved <b>{savedScenes}</b> dirty scene(s) & project assets ({reason}) at {timestamp}.");
                }
                else
                {
                    Debug.Log($"<color=#64D2FF><b>[AutoSave]</b></color> AutoSave check completed at {timestamp}. Scenes were clean; assets saved.");
                }
            }

            return savedScenes;
        }

        // --- Menu Items ---

        [MenuItem("Tools/AutoSave/Save Now (Scenes & Assets) %#s", false, 1)]
        public static void MenuSaveNow()
        {
            PerformAutoSave("Manual Trigger");
        }

        [MenuItem("Tools/AutoSave/Toggle AutoSave", false, 2)]
        public static void MenuToggleAutoSave()
        {
            Enabled = !Enabled;
            Debug.Log($"[AutoSave] AutoSave is now <b>{(Enabled ? "ENABLED (every 1 min)" : "DISABLED")}</b>.");
        }

        [MenuItem("Tools/AutoSave/Settings & Status Window", false, 20)]
        [MenuItem("Window/AutoSave Settings", false, 100)]
        public static void OpenSettingsWindow()
        {
            EditorAutoSaveWindow.Open();
        }
    }

    public class EditorAutoSaveWindow : EditorWindow
    {
        public static void Open()
        {
            var window = GetWindow<EditorAutoSaveWindow>("AutoSave Settings");
            window.minSize = new Vector2(340, 320);
            window.Show();
        }

        private void OnEnable()
        {
            EditorAutoSave.Initialize();
        }

        private void OnInspectorUpdate()
        {
            // Repaint to smoothly update the countdown timer bar
            Repaint();
        }

        private void OnGUI()
        {
            GUILayout.Space(10);

            // Title
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Unity Editor AutoSave", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                string statusText = EditorAutoSave.Enabled ? "● ACTIVE" : "○ DISABLED";
                Color statusColor = EditorAutoSave.Enabled ? new Color(0.3f, 0.9f, 0.4f) : new Color(0.8f, 0.3f, 0.3f);
                GUIStyle statusStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = statusColor } };
                GUILayout.Label(statusText, statusStyle);
            }

            EditorGUILayout.Space(8);

            // Countdown Bar
            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Play Mode active: Editor AutoSave is currently paused.", MessageType.Info);
            }
            else if (EditorAutoSave.Enabled)
            {
                double secondsLeft = EditorAutoSave.SecondsUntilNextSave;
                double totalSeconds = EditorAutoSave.TotalIntervalSeconds;
                float progress = totalSeconds > 0 ? (float)(1.0 - (secondsLeft / totalSeconds)) : 1f;

                Rect progressRect = GUILayoutUtility.GetRect(18, 22, "TextField");
                EditorGUI.ProgressBar(progressRect, Mathf.Clamp01(progress), $"Next save in: {Mathf.CeilToInt((float)secondsLeft)}s");
            }
            else
            {
                EditorGUILayout.HelpBox("AutoSave is currently disabled.", MessageType.Warning);
            }

            EditorGUILayout.Space(12);

            // Controls
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");

            bool enabled = EditorGUILayout.Toggle("Enable AutoSave", EditorAutoSave.Enabled);
            if (enabled != EditorAutoSave.Enabled)
            {
                EditorAutoSave.Enabled = enabled;
            }

            float interval = EditorGUILayout.Slider("Interval (Minutes)", EditorAutoSave.IntervalMinutes, 0.5f, 15f);
            if (Math.Abs(interval - EditorAutoSave.IntervalMinutes) > 0.01f)
            {
                EditorAutoSave.IntervalMinutes = (float)Math.Round(interval, 1);
            }

            EditorAutoSave.SaveAssets = EditorGUILayout.Toggle("Save Project Assets", EditorAutoSave.SaveAssets);
            EditorAutoSave.SaveBeforePlay = EditorGUILayout.Toggle("Save Before Play Mode", EditorAutoSave.SaveBeforePlay);
            EditorAutoSave.LogConfirmations = EditorGUILayout.Toggle("Log Saves to Console", EditorAutoSave.LogConfirmations);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(12);

            // Actions
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save Now", GUILayout.Height(28)))
                {
                    EditorAutoSave.PerformAutoSave("Manual Button");
                }

                if (GUILayout.Button("Reset Timer (1 min)", GUILayout.Height(28)))
                {
                    EditorAutoSave.ResetTimer();
                }
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox("Editor AutoSave automatically tracks unsaved open scenes and assets every minute, ensuring you never lose scene edits if Unity closes unexpectedly.", MessageType.None);
        }
    }
}
