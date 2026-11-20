using UnityEngine;

namespace JbelIghoud.Game
{
    /// <summary>
    /// Humanoid IK driver used by cutscenes: head/eyes look-at + left/right hand goals with elbow hints.
    /// Lives on the same GameObject as the Animator (needs "IK Pass" on the Base Layer -
    /// JbelIntroEditorSetup turns it on automatically).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class JbelActorIK : MonoBehaviour
    {
        public float lookWeight;
        public Vector3 lookTarget;
        [Range(0f, 1f)] public float bodyLookWeight = 0.3f;

        public float leftHandWeight, rightHandWeight;
        public Vector3 leftHandTarget, rightHandTarget;

        /// <summary>True when OnAnimatorIK is actually being called (IK pass enabled).</summary>
        public bool IKActive => Time.frameCount - lastIKFrame < 3;

        private Animator anim;
        private int lastIKFrame = -100;

        private void Awake() { anim = GetComponent<Animator>(); }

        private void OnAnimatorIK(int layerIndex)
        {
            lastIKFrame = Time.frameCount;
            if (anim == null) return;

            anim.SetLookAtWeight(lookWeight, bodyLookWeight * lookWeight, 0.9f, 1f, 0.55f);
            anim.SetLookAtPosition(lookTarget);

            Transform t = transform;
            anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, leftHandWeight);
            anim.SetIKPosition(AvatarIKGoal.LeftHand, leftHandTarget);
            anim.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, leftHandWeight * 0.7f);
            anim.SetIKHintPosition(AvatarIKHint.LeftElbow, t.position + t.up * 0.95f - t.right * 0.42f - t.forward * 0.1f);

            anim.SetIKPositionWeight(AvatarIKGoal.RightHand, rightHandWeight);
            anim.SetIKPosition(AvatarIKGoal.RightHand, rightHandTarget);
            anim.SetIKHintPositionWeight(AvatarIKHint.RightElbow, rightHandWeight * 0.7f);
            anim.SetIKHintPosition(AvatarIKHint.RightElbow, t.position + t.up * 0.95f + t.right * 0.42f - t.forward * 0.1f);
        }
    }
}
