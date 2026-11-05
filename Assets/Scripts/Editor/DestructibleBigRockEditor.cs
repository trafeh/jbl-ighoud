using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(DestructibleBigRock))]
public class DestructibleBigRockEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DestructibleBigRock rock = (DestructibleBigRock)target;

        // Custom degradation bar
        EditorGUILayout.Space(6);
        Rect rect = EditorGUILayout.GetControlRect(false, 22);
        float fraction = rock.HealthFraction;
        Color barColor = Color.Lerp(new Color(1f, 0.3f, 0.3f), new Color(0.3f, 0.9f, 0.4f), fraction);
        EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.16f));
        Rect fillRect = new Rect(rect.x, rect.y, rect.width * fraction, rect.height);
        EditorGUI.DrawRect(fillRect, barColor);
        EditorGUI.LabelField(rect, $"  Degradation Health: {rock.HitsRemaining} / {rock.MaxHits} hits ({(fraction * 100f):F0}%)", EditorStyles.boldLabel);

        EditorGUILayout.Space(6);

        if (Application.isPlaying)
        {
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(1f, 0.75f, 0.3f);
            if (GUILayout.Button("💥 Click / Hit Rock (1 Damage)", GUILayout.Height(32)))
            {
                rock.DamageRock(1);
            }

            GUI.backgroundColor = new Color(1f, 0.35f, 0.35f);
            if (GUILayout.Button("💣 Crash & Shatter Now", GUILayout.Height(32)))
            {
                rock.CrashAndShatter();
            }
            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.Space(8);
        DrawDefaultInspector();
    }
}
