using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace JbelIghoud.Game.EditorTools
{
    /// <summary>
    /// Makes sure the character Animator Controller has "IK Pass" enabled on its Base Layer,
    /// so the intro cutscene can move the hands / head (JbelActorIK). Runs after every recompile.
    /// </summary>
    [InitializeOnLoad]
    internal static class JbelIntroEditorSetup
    {
        static JbelIntroEditorSetup()
        {
            EditorApplication.delayCall += EnsureIKPass;
        }

        [MenuItem("Jbel/Fix Character IK Pass")]
        private static void EnsureIKPass()
        {
            foreach (var guid in AssetDatabase.FindAssets("\"animation character\" t:AnimatorController"))
            {
                var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GUIDToAssetPath(guid));
                if (ctrl == null || ctrl.name != "animation character" || ctrl.layers.Length == 0) continue;
                var layers = ctrl.layers;
                if (layers[0].iKPass) continue;
                layers[0].iKPass = true;
                ctrl.layers = layers;
                EditorUtility.SetDirty(ctrl);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Jbel] Enabled IK Pass on '{ctrl.name}' (needed for the intro cutscene gestures).");
            }
        }
    }
}
