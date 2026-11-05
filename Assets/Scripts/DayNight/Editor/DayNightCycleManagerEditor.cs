using UnityEditor;
using UnityEngine;

namespace JbelIghoud.DayNight
{
    [CustomEditor(typeof(DayNightCycleManager))]
    public class DayNightCycleManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DayNightCycleManager manager = (DayNightCycleManager)target;

            // Header Banner
            EditorGUILayout.Space(6);
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter
            };

            GUIStyle timeDisplayStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            timeDisplayStyle.normal.textColor = new Color(1.0f, 0.85f, 0.4f);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("☀️ ALLSKY 24-HOUR DAY/NIGHT SYSTEM 🌙", headerStyle);
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField($"{manager.GetFormattedTime()} ({manager.GetFormattedDay()}) - {manager.CurrentPhase}", timeDisplayStyle);
            EditorGUILayout.Space(4);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // Time Scrubber
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Time of Day Quick Controls", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            float newTime = EditorGUILayout.Slider("Time (0h - 24h)", manager.CurrentTime, 0f, 24f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(manager, "Change Time Of Day");
                manager.SetTime(newTime);
                EditorUtility.SetDirty(manager);
            }

            // Quick Jump Buttons
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("07:00 (Start)", GUILayout.Height(24))) { SetTimeTo(manager, 7.0f); }
            if (GUILayout.Button("10:00 (Day)", GUILayout.Height(24))) { SetTimeTo(manager, 10.0f); }
            if (GUILayout.Button("14:30 (Pink)", GUILayout.Height(24))) { SetTimeTo(manager, 14.5f); }
            if (GUILayout.Button("17:00 (Sunset)", GUILayout.Height(24))) { SetTimeTo(manager, 17.0f); }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("19:30 (Dusk)", GUILayout.Height(24))) { SetTimeTo(manager, 19.5f); }
            if (GUILayout.Button("21:30 (Moon)", GUILayout.Height(24))) { SetTimeTo(manager, 21.5f); }
            if (GUILayout.Button("00:00 (Mid)", GUILayout.Height(24))) { SetTimeTo(manager, 0.0f); }
            if (GUILayout.Button("05:00 (Dawn)", GUILayout.Height(24))) { SetTimeTo(manager, 5.0f); }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // Auto-setup button
            GUI.backgroundColor = new Color(0.4f, 0.85f, 0.5f);
            if (GUILayout.Button("⚡ Auto-Populate 10 AllSkyFree Skyboxes", GUILayout.Height(32)))
            {
                Undo.RecordObject(manager, "Auto-Populate 10 AllSkyFree Skyboxes");
                manager.AutoPopulateAllSkyFree();
                EditorUtility.SetDirty(manager);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(8);

            // Default Inspector Fields
            DrawDefaultInspector();
        }

        private void SetTimeTo(DayNightCycleManager manager, float hour)
        {
            Undo.RecordObject(manager, "Set Time of Day");
            manager.SetTime(hour);
            EditorUtility.SetDirty(manager);
        }
    }
}
