using UnityEngine;

namespace JbelIghoud.Game
{
    /// <summary>
    /// Procedural "reach out" for a humanoid arm, layered after the Animator (and after JbelElderPose):
    /// the upper arm and forearm swing so the hand points at <see cref="target"/>.
    /// Used for giving / taking objects without dedicated animation clips.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [RequireComponent(typeof(Animator))]
    public class JbelReachPose : MonoBehaviour
    {
        public Vector3 target;
        [Range(0f, 1f)] public float weight;
        public bool rightArm = true;

        private Transform upper, lower, hand;
        public Transform Hand => hand;

        private void Awake()
        {
            var anim = GetComponent<Animator>();
            if (anim == null || !anim.isHuman) { enabled = false; return; }
            upper = anim.GetBoneTransform(rightArm ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
            lower = anim.GetBoneTransform(rightArm ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
            hand = anim.GetBoneTransform(rightArm ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
        }

        /// <summary>Palm position (a little past the wrist bone).</summary>
        public Vector3 PalmPosition()
        {
            if (hand == null) return transform.position + Vector3.up * 1.2f;
            Vector3 dir = lower != null ? (hand.position - lower.position).normalized : transform.forward;
            return hand.position + dir * 0.07f;
        }

        private void LateUpdate()
        {
            if (weight <= 0.001f || upper == null || lower == null || hand == null) return;
            Aim(upper, lower.position);
            Aim(lower, hand.position);
        }

        private void Aim(Transform bone, Vector3 childPos)
        {
            Vector3 cur = childPos - bone.position;
            Vector3 want = target - bone.position;
            if (cur.sqrMagnitude < 1e-6f || want.sqrMagnitude < 1e-6f) return;
            Quaternion q = Quaternion.FromToRotation(cur, want);
            bone.rotation = Quaternion.Slerp(Quaternion.identity, q, weight) * bone.rotation;
        }

        /// <summary>Get (or add) the reach pose on an animator.</summary>
        public static JbelReachPose For(Animator anim)
        {
            if (anim == null) return null;
            var r = anim.GetComponent<JbelReachPose>();
            if (r == null) r = anim.gameObject.AddComponent<JbelReachPose>();
            return r;
        }
    }
}
