using UnityEngine;

namespace JbelIghoud.Game
{
    /// <summary>
    /// Procedural pose layer for the Elder (runs after the Animator):
    /// hunched back, slow head nods while talking, and a raised-hand greeting.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class JbelElderPose : MonoBehaviour
    {
        [SerializeField, Range(0f, 40f)] private float hunchDegrees = 20f;
        [SerializeField, Range(0f, 25f)] private float headLiftDegrees = 12f;
        [SerializeField] private float talkNodDegrees = 5f;

        public bool Talking { get; set; }

        private Animator anim;
        private Transform spine, chest, neck, head, armR, foreArmR;
        private float waveT = -1f, waveDur;
        private float talkW;

        private void Awake()
        {
            anim = GetComponent<Animator>();
            if (!anim.isHuman) { enabled = false; return; }
            spine = anim.GetBoneTransform(HumanBodyBones.Spine);
            chest = anim.GetBoneTransform(HumanBodyBones.Chest);
            neck = anim.GetBoneTransform(HumanBodyBones.Neck);
            head = anim.GetBoneTransform(HumanBodyBones.Head);
            armR = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            foreArmR = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
        }

        /// <summary>Raise the right hand in greeting.</summary>
        public void Wave(float duration = 2.2f)
        {
            waveT = 0f;
            waveDur = duration;
        }

        private void LateUpdate()
        {
            Vector3 right = transform.right;

            if (spine != null) spine.rotation = Quaternion.AngleAxis(hunchDegrees * 0.55f, right) * spine.rotation;
            if (chest != null) chest.rotation = Quaternion.AngleAxis(hunchDegrees * 0.45f, right) * chest.rotation;

            talkW = Mathf.MoveTowards(talkW, Talking ? 1f : 0f, Time.deltaTime * 3f);
            float nod = Mathf.Sin(Time.time * 4.2f) * talkNodDegrees * talkW;
            if (neck != null) neck.rotation = Quaternion.AngleAxis(-headLiftDegrees * 0.5f, right) * neck.rotation;
            if (head != null) head.rotation = Quaternion.AngleAxis(-headLiftDegrees * 0.5f + nod, right) * head.rotation;

            if (waveT >= 0f && armR != null)
            {
                waveT += Time.deltaTime;
                float p = waveT / waveDur;
                float w = p < 0.2f ? p / 0.2f : (p > 0.8f ? (1f - p) / 0.2f : 1f);
                w = w * w * (3f - 2f * w);
                // lift arm up and forward, bend elbow, small side-to-side wave
                armR.rotation = Quaternion.AngleAxis(-115f * w, transform.forward) * Quaternion.AngleAxis(-20f * w, right) * armR.rotation;
                if (foreArmR != null)
                    foreArmR.rotation = Quaternion.AngleAxis((25f + Mathf.Sin(waveT * 9f) * 18f) * w, transform.forward) * foreArmR.rotation;
                if (p >= 1f) waveT = -1f;
            }
        }
    }
}
