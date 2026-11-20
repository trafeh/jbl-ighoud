using System.Collections;
using UnityEngine;
using Unity.Cinemachine;
using JbelIghoud.UI;

namespace JbelIghoud.Game
{
    /// <summary>
    /// New-game intro: title card -> the survivor walks in, stops, looks right then left, spots the Elder,
    /// walks over -> Elder speaks -> name entry -> Elder welcomes you by name -> camera hands back to gameplay,
    /// HUD reveals and the first objective starts.
    /// Plays on START GAME (or whenever the intro hasn't been completed yet).
    /// Move the IntroStart / IntroStop markers in the scene to restage it.
    /// </summary>
    public class JbelIntroCutscene : MonoBehaviour
    {
        public static bool IsPlaying { get; private set; }

        [Header("--- ACTORS ---")]
        [SerializeField] private TopDownPlayerController player;
        [Tooltip("Elder root (the interactable).")]
        [SerializeField] private Transform elder;
        [Tooltip("Elder body (feet on the ground) - used for camera framing.")]
        [SerializeField] private Transform elderBody;
        [SerializeField] private JbelElderPose elderPose;

        [Header("--- STAGING (move these in the scene) ---")]
        [SerializeField] private Transform introStart;
        [SerializeField] private Transform introStop;
        [Tooltip("How far in front of the Elder the survivor stops to talk.")]
        [SerializeField] private float talkDistance = 2.1f;

        [Header("--- CAMERA ---")]
        [SerializeField] private CinemachineCamera cutsceneCamera;
        [SerializeField] private int activePriority = 100;
        [SerializeField] private float handBackBlend = 1.6f;

        [Header("--- UI ---")]
        [SerializeField] private JbelDialogueUI ui;
        [SerializeField] private CanvasGroup hudPanel;
        [SerializeField] private GameObject joystickZone;
        [SerializeField] private JbelObjectiveManager objectives;

        [Header("--- TIMING ---")]
        [SerializeField] private float walkSpeed = 2.4f;
        [SerializeField] private float walkAnimSpeed = 0.45f;
        [Tooltip("Where the Elder is when the survivor wakes up, in degrees from straight ahead (100 = behind-left).")]
        [SerializeField] private float spawnAngleFromElder = 100f;

        [Header("--- TEXT ---")]
        [SerializeField] private string titleLine = "JBEL IGHOUD";
        [SerializeField] private string subtitleLine = "MOROCCO  ·  300,000 YEARS AGO";
        [SerializeField] private string elderName = "THE ELDER";
        [Tooltip("Old flow: ask the player's name. Off = the Elder explains the world and asks you to follow him.")]
        [SerializeField] private bool askPlayerName = false;
        [SerializeField, TextArea(2, 3)] private string[] worldLines =
        {
            "Ah... you are awake. Good. The mountain did not take you.",
            "This is Jbel Ighoud. Here, nothing is given. The land feeds only those who understand it.",
            "The nights are cold, and the beasts are stronger than us. Bare hands are not enough.",
            "But stone... stone can become a tooth, a claw, even fire. If you learn to listen to it.",
            "Come. Walk with me, and I will show you.",
        };
        [SerializeField, TextArea(2, 3)] private string[] linesBeforeName =
        {
            "Ah... a new face among the stones of Jbel Ighoud.",
            "These hills remember the first of our kind. Long before memory, they walked where you stand.",
            "You look lost, child. Lost... but not without purpose.",
        };
        [SerializeField, TextArea(2, 3)] private string nameQuestion = "Tell me... what is your name?";
        [SerializeField, TextArea(2, 3)] private string[] linesAfterName =
        {
            "{name}... a strong name. The mountain will remember it.",
            "Every journey begins with the ground beneath your feet. The old ones shaped stone into tools, and so must you.",
            "Gather three stones from the hillside, then return to me.",
        };

        // runtime
        private Animator playerAnim;
        private CharacterController cc;
        private MobileFreeLookTouchController touchLook;
        private CinemachineBrain brain;
        private Transform headBone, neckBone, chestBone;
        // performance (IK)
        private enum Act { None, HeadDown, LookForward, HandsLook, FaceTouch, LookRight, LookLeft, LookElder }
        private Act act = Act.None;
        private float actSpeed = 3f, actTime;
        private JbelActorIK ik;
        private Transform hipsBone, spineBone;
        private float dizzy, dizzyShown;
        private float lookW, handLW, handRW;
        private Vector3 lookPos, handLPos, handRPos;
        private bool skipRequested;
        private float vy;

        private enum Shot { None, WakeWide, HandsClose, FaceClose, RevealOTS, WideTwo, OverPlayerOnElder, OverElderOnPlayer }
        private Shot shot = Shot.None;
        private bool snapNext;
        private float shotTime;
        private bool willPlay;

        private void Awake()
        {
            if (player == null) player = FindAnyObjectByType<TopDownPlayerController>();
            willPlay = JbelPlayerProfile.PendingNewGame || !JbelPlayerProfile.IntroDone;
            if (willPlay && ui != null) ui.SetBlack(1f);
            if (cutsceneCamera != null) cutsceneCamera.Priority = 0;
        }

        private IEnumerator Start()
        {
            yield return null; // let every other Start() run first (HUD forces itself visible there)

            if (!willPlay)
            {
                if (objectives != null) objectives.Begin();
                yield break;
            }
            yield return Run();
        }

        // ================================================================== SEQUENCE

        private IEnumerator Run()
        {
            IsPlaying = true;
            skipRequested = false;
            if (!JbelPlayerProfile.HasProfile) JbelPlayerProfile.StartNewGame();
            JbelPlayerProfile.SetObjective(0, 0); // the intro always restarts the first mission chain
            JbelPlayerProfile.PendingNewGame = false;

            playerAnim = player.GetComponentInChildren<Animator>();
            cc = player.GetComponent<CharacterController>();
            touchLook = FindAnyObjectByType<MobileFreeLookTouchController>();
            brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
            if (playerAnim != null && playerAnim.isHuman)
            {
                headBone = playerAnim.GetBoneTransform(HumanBodyBones.Head);
                neckBone = playerAnim.GetBoneTransform(HumanBodyBones.Neck);
                chestBone = playerAnim.GetBoneTransform(HumanBodyBones.Chest);
            }

            // ---- lock everything
            player.SetControlsLocked(true);
            if (touchLook != null) touchLook.isInputDisabled = true;
            SetHUD(false, true);
            if (ui != null) { ui.SetBlack(1f); ui.OnSkipPressed += OnSkip; }

            CinemachineBlendDefinition originalBlend = brain != null ? brain.DefaultBlend : default;
            if (brain != null) brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);

            // ---- stage: the survivor wakes up at IntroStop, the Elder behind-left (out of view)
            Vector3 elderFeet = ElderFeet();
            Vector3 stop = Ground(introStop != null ? introStop.position : elderFeet + new Vector3(-12f, 0f, -10f));
            Vector3 toElder = Flat(elderFeet - stop);
            Vector3 facing = Quaternion.AngleAxis(spawnAngleFromElder, Vector3.up) * toElder;
            player.TeleportTo(stop + Vector3.up * 0.05f, Quaternion.LookRotation(facing));
            SetAnimMoving(false, 0f);
            SetupIK();

            cutsceneCamera.Priority = activePriority;
            SetAct(Act.HeadDown, 99f);
            dizzy = 1f;
            SetShot(Shot.WakeWide, true);
            if (ui != null) StartCoroutine(ui.Letterbox(true, 0.01f));

            // ---- title card over black
            if (ui != null) yield return ui.ShowTitle(titleLine, subtitleLine, 1.6f);
            if (ui != null) ui.SetSkipVisible(true);

            if (!skipRequested)
            {
                // ---- wakes up: head hanging, swaying... slowly lifts his head (where am I?)
                if (ui != null) StartCoroutine(ui.Fade(0f, 2.6f));
                yield return Wait(3.0f);
                SetAct(Act.LookForward, 1.2f);
                yield return Wait(1.4f);

                // ---- looks at his hands, turning them over (am I... alive?)
                SetShot(Shot.HandsClose, true);
                SetAct(Act.HandsLook, 2.5f);
                yield return Wait(3.4f);

                // ---- touches his face
                SetShot(Shot.FaceClose, true);
                SetAct(Act.FaceTouch, 2.2f);
                yield return Wait(2.6f);

                // ---- looks around, confused: right... left...
                dizzy = 0.35f;
                SetAct(Act.LookRight, 1.6f);
                yield return Wait(1.4f);
                SetAct(Act.LookLeft, 1.6f);
                yield return Wait(1.3f);

                // ---- ...and notices the old man
                dizzy = 0f;
                SetAct(Act.LookElder, 7f);
                yield return Wait(0.45f);
                SetShot(Shot.RevealOTS, true);
                if (elderPose != null) elderPose.Wave(2.6f);
                yield return Wait(2.6f);

                yield return TurnTowards(Flat(elderFeet - player.transform.position), 0.7f);
                SetAct(Act.None, 3f);

                // ---- walks over to him
                SetShot(Shot.WideTwo, true);
                yield return WalkTo(TalkSpot(), walkSpeed);
            }

            if (skipRequested) yield return DoSkip();
            if (ui != null) ui.SetSkipVisible(false);
            if (ui != null) ui.OnSkipPressed -= OnSkip;

            yield return TurnTowards(Flat(elderFeet - player.transform.position), 0.35f);
            SetAct(Act.LookElder, 3f); // keep eye contact while they talk

            // ---- conversation
            SetShot(Shot.OverPlayerOnElder, true);
            if (elderPose != null) elderPose.Talking = true;
            if (askPlayerName)
            {
                if (!skipRequested)
                    foreach (var line in linesBeforeName) yield return Say(line);
                yield return Say(nameQuestion);
                if (elderPose != null) elderPose.Talking = false;

                SetShot(Shot.OverElderOnPlayer, true);
                ui.HideBox();
                string chosen = JbelPlayerProfile.DefaultName;
                yield return ui.AskName(n => chosen = n);
                JbelPlayerProfile.PlayerName = chosen;
                var hud = FindAnyObjectByType<JbelObjectiveHUD>();
                if (hud != null) hud.RefreshProfile(false);

                SetShot(Shot.OverPlayerOnElder, true);
                if (elderPose != null) elderPose.Talking = true;
                foreach (var line in linesAfterName) yield return Say(line);
            }
            else
            {
                // No name entry: the Elder explains the world, then the guided walk (JbelElderGuide) takes over.
                foreach (var line in worldLines) yield return Say(line);
            }
            if (elderPose != null) elderPose.Talking = false;
            ui.HideBox();

            // ---- hand back to gameplay
            EndPerformance();
            JbelPlayerProfile.MarkIntroDone();
            if (brain != null) brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, handBackBlend);
            cutsceneCamera.Priority = 0;
            shot = Shot.None;
            StartCoroutine(ui.Letterbox(false, 0.8f));
            yield return new WaitForSecondsRealtime(handBackBlend * 0.6f);

            SetHUD(true, false);
            player.SetControlsLocked(false);
            if (touchLook != null) touchLook.isInputDisabled = false;
            IsPlaying = false;
            if (objectives != null) objectives.Begin();

            yield return new WaitForSecondsRealtime(handBackBlend);
            if (brain != null) brain.DefaultBlend = originalBlend;
        }

        private IEnumerator Say(string line)
        {
            yield return ui.Say(elderName, line.Replace("{name}", JbelPlayerProfile.PlayerName));
        }

        private void OnSkip() { skipRequested = true; }

        private IEnumerator DoSkip()
        {
            if (ui != null) ui.SetSkipVisible(false);
            if (ui != null) yield return ui.Fade(1f, 0.3f);
            Vector3 spot = TalkSpot();
            player.TeleportTo(spot + Vector3.up * 0.05f, Quaternion.LookRotation(Flat(ElderFeet() - spot)));
            SetAnimMoving(false, 0f);
            SetAct(Act.None, 99f); dizzy = 0f;
            SetShot(Shot.OverPlayerOnElder, true);
            yield return null;
            if (ui != null) yield return ui.Fade(0f, 0.45f);
        }

        // ================================================================== MOVEMENT

        private IEnumerator WalkTo(Vector3 target, float speed)
        {
            float timeout = 20f, stuckTimer = 0f;
            Vector3 lastPos = player.transform.position;
            while (!skipRequested && timeout > 0f)
            {
                Vector3 to = Flat(target - player.transform.position, false);
                if (to.magnitude < 0.2f) break;

                float dt = Time.deltaTime;
                timeout -= dt;
                Vector3 dir = to.normalized;
                player.transform.rotation = Quaternion.Slerp(player.transform.rotation, Quaternion.LookRotation(dir), dt * 6f);

                vy = (cc != null && cc.isGrounded) ? -2f : vy - 20f * dt;
                Vector3 motion = (player.transform.forward * Mathf.Min(speed, to.magnitude / dt) + Vector3.up * vy) * dt;
                if (cc != null && cc.enabled) cc.Move(motion); else player.transform.position += motion;
                SetAnimMoving(true, speed);

                stuckTimer += dt;
                if (stuckTimer > 1f)
                {
                    if (Vector3.Distance(lastPos, player.transform.position) < 0.15f)
                    {
                        player.TeleportTo(Ground(target) + Vector3.up * 0.05f, Quaternion.LookRotation(dir));
                        break;
                    }
                    lastPos = player.transform.position; stuckTimer = 0f;
                }
                yield return null;
            }
            SetAnimMoving(false, 0f);
        }

        private IEnumerator TurnTowards(Vector3 dir, float duration)
        {
            if (dir.sqrMagnitude < 0.0001f) yield break;
            Quaternion from = player.transform.rotation, to = Quaternion.LookRotation(dir);
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                player.transform.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
                yield return null;
            }
            player.transform.rotation = to;
        }

        private void SetAnimMoving(bool moving, float speed)
        {
            if (playerAnim == null) return;
            playerAnim.SetBool("IsMoving", moving);
            playerAnim.SetFloat("Speed", speed);
            playerAnim.SetBool("IsGrounded", true);
            playerAnim.speed = moving ? walkAnimSpeed : 1f;
        }

        private IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds && !skipRequested) { t += Time.deltaTime; yield return null; }
        }

        // ================================================================== CAMERA + LOOK

        private void SetShot(Shot s, bool cut)
        {
            shot = s;
            snapNext = cut;
            shotTime = 0f;
        }

        private void LateUpdate()
        {
            if (!IsPlaying) return;

            UpdatePerformance();

            if (shot == Shot.None || cutsceneCamera == null) return;
            shotTime += Time.deltaTime;
            GetShot(out Vector3 pos, out Vector3 look);
            Transform cam = cutsceneCamera.transform;
            Quaternion rot = Quaternion.LookRotation(look - pos);
            if (snapNext)
            {
                cutsceneCamera.ForceCameraPosition(pos, rot);
                snapNext = false;
            }
            else
            {
                float k = 1f - Mathf.Exp(-5f * Time.deltaTime);
                cam.position = Vector3.Lerp(cam.position, pos, k);
                cam.rotation = Quaternion.Slerp(cam.rotation, rot, k);
            }
        }

        private void GetShot(out Vector3 pos, out Vector3 look)
        {
            Vector3 p = player.transform.position;
            Vector3 f = Flat(player.transform.forward);
            Vector3 r = Vector3.Cross(Vector3.up, f);
            Vector3 e = ElderFeet();
            Vector3 toE = Flat(e - p);
            Vector3 rE = Vector3.Cross(Vector3.up, toE);
            Vector3 head = p + Vector3.up * 1.6f;

            switch (shot)
            {
                case Shot.WakeWide:
                    float push = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(shotTime / 6f));
                    pos = p + f * Mathf.Lerp(4.6f, 3.0f, push) + r * 0.9f + Vector3.up * Mathf.Lerp(1.9f, 1.5f, push);
                    look = p + Vector3.up * 1.15f;
                    break;
                case Shot.HandsClose:
                    // medium close: face + hands in frame, slightly from the side
                    pos = p + f * 1.75f + r * 0.55f + Vector3.up * 1.5f;
                    look = p + f * 0.2f + Vector3.up * 1.38f;
                    break;
                case Shot.FaceClose:
                    pos = p + f * 2.0f - r * 0.35f + Vector3.up * 1.6f;
                    look = head;
                    break;
                case Shot.RevealOTS:
                    float push2 = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(shotTime / 2.4f));
                    pos = p - toE * (1.8f - push2 * 0.6f) + rE * 0.7f + Vector3.up * 1.85f;
                    look = Vector3.Lerp(head, e + Vector3.up * 1.4f, 0.85f);
                    break;
                case Shot.WideTwo:
                    Vector3 mid = (p + e) * 0.5f;
                    float span = Vector3.Distance(Flat(p, false), Flat(e, false));
                    pos = mid + rE * Mathf.Max(5f, span * 0.8f) + Vector3.up * 2.2f - toE * 1.5f;
                    look = mid + Vector3.up * 1.1f;
                    break;
                case Shot.OverPlayerOnElder:
                    pos = p - toE * 1.3f + rE * 0.6f + Vector3.up * 1.8f;
                    look = e + Vector3.up * 1.35f;
                    break;
                default: // OverElderOnPlayer
                    Vector3 toP = -toE;
                    Vector3 rP = Vector3.Cross(Vector3.up, toP);
                    pos = e - toP * 1.2f + rP * 0.6f + Vector3.up * 1.7f;
                    look = head - Vector3.up * 0.1f;
                    break;
            }
        }

        // ================================================================== PERFORMANCE (IK)

        private void SetupIK()
        {
            if (playerAnim == null) return;
            ik = playerAnim.GetComponent<JbelActorIK>();
            if (ik == null) ik = playerAnim.gameObject.AddComponent<JbelActorIK>();
            ik.enabled = true;
            hipsBone = playerAnim.isHuman ? playerAnim.GetBoneTransform(HumanBodyBones.Hips) : null;
            spineBone = playerAnim.isHuman ? playerAnim.GetBoneTransform(HumanBodyBones.Spine) : null;
            lookPos = player.transform.position + player.transform.forward * 2f + Vector3.up * 1.5f;
            handLPos = handRPos = player.transform.position + Vector3.up * 0.9f;
        }

        private void SetAct(Act a, float speed)
        {
            act = a;
            actSpeed = speed;
            actTime = 0f;
        }

        /// <summary>Drives look-at / hand IK targets for the current act (runs every frame).</summary>
        private void UpdatePerformance()
        {
            if (ik == null) return;
            float dt = Time.deltaTime;
            actTime += dt;

            Vector3 p = player.transform.position;
            Vector3 f = player.transform.forward;
            Vector3 r = player.transform.right;
            Vector3 up = Vector3.up;
            Vector3 headPos = headBone != null ? headBone.position : p + up * 1.6f;
            Vector3 chestFront = p + up * 1.3f + f * 0.32f;

            float wLook = 1f, wL = 0f, wR = 0f;
            Vector3 tLook = p + f * 3f + up * 1.6f, tL = handLPos, tR = handRPos;

            switch (act)
            {
                case Act.None:
                    wLook = 0f;
                    break;
                case Act.HeadDown:
                    tLook = p + f * 1.1f + up * 0.05f;
                    break;
                case Act.LookForward:
                    tLook = p + f * 4f + r * 0.6f + up * 1.45f;
                    break;
                case Act.HandsLook:
                {
                    // both hands in front of the chest, slowly turning over / rising and falling
                    float t = actTime;
                    tL = chestFront - r * 0.14f + up * (Mathf.Sin(t * 1.9f) * 0.05f) + f * (Mathf.Sin(t * 1.3f + 1f) * 0.04f);
                    tR = chestFront + r * 0.14f + up * (Mathf.Sin(t * 1.9f + 2.2f) * 0.05f) + f * (Mathf.Sin(t * 1.3f + 2.6f) * 0.04f);
                    tLook = (tL + tR) * 0.5f + up * 0.02f;
                    wL = wR = 1f;
                    break;
                }
                case Act.FaceTouch:
                {
                    float t = actTime;
                    tR = headPos + f * 0.12f + r * 0.075f - up * (0.05f + Mathf.Sin(t * 2.2f) * 0.015f);
                    tLook = p + f * 3f - r * 0.4f + up * 1.35f;
                    wR = 1f;
                    break;
                }
                case Act.LookRight:
                    tLook = p + (f * 0.5f + r).normalized * 4f + up * 1.6f;
                    break;
                case Act.LookLeft:
                    tLook = p + (f * 0.6f - r).normalized * 4f + up * 1.6f;
                    break;
                case Act.LookElder:
                    tLook = ElderFeet() + up * 1.5f;
                    break;
            }

            float k = 1f - Mathf.Exp(-actSpeed * dt);
            lookW = Mathf.Lerp(lookW, wLook, k);
            handLW = Mathf.Lerp(handLW, wL, k);
            handRW = Mathf.Lerp(handRW, wR, k);
            lookPos = Vector3.Lerp(lookPos, tLook, k);
            handLPos = Vector3.Lerp(handLPos, tL, k);
            handRPos = Vector3.Lerp(handRPos, tR, k);

            ik.lookWeight = lookW;
            ik.lookTarget = lookPos;
            ik.bodyLookWeight = act == Act.HeadDown ? 0.6f : 0.3f;
            ik.leftHandWeight = handLW;
            ik.rightHandWeight = handRW;
            ik.leftHandTarget = handLPos;
            ik.rightHandTarget = handRPos;

            // dizzy sway + slumped posture (on top of the animation, after IK)
            dizzyShown = Mathf.MoveTowards(dizzyShown, dizzy, dt * 0.6f);
            if (dizzyShown > 0.001f)
            {
                float t = Time.time;
                if (hipsBone != null)
                    hipsBone.rotation = Quaternion.AngleAxis(Mathf.Sin(t * 0.9f) * 3.5f * dizzyShown, f)
                                      * Quaternion.AngleAxis(Mathf.Sin(t * 0.6f + 1f) * 2f * dizzyShown, r) * hipsBone.rotation;
                if (spineBone != null && act == Act.HeadDown)
                    spineBone.rotation = Quaternion.AngleAxis(12f * dizzyShown, r) * spineBone.rotation;
            }

            // Fallback when the Animator has no IK pass: turn the head bone toward the look target
            if (!ik.IKActive && headBone != null && lookW > 0.01f)
            {
                Vector3 want = (lookPos - headBone.position).normalized;
                Quaternion delta = Quaternion.FromToRotation(f, want);
                headBone.rotation = Quaternion.Slerp(Quaternion.identity, delta, lookW * 0.7f) * headBone.rotation;
            }
        }

        private void EndPerformance()
        {
            if (ik != null) { ik.lookWeight = 0f; ik.leftHandWeight = 0f; ik.rightHandWeight = 0f; ik.enabled = false; }
            act = Act.None; dizzy = dizzyShown = 0f;
        }

        // ================================================================== HELPERS

        private void SetHUD(bool visible, bool instant)
        {
            if (joystickZone != null) joystickZone.SetActive(visible);
            if (hudPanel == null) return;
            hudPanel.blocksRaycasts = visible;
            hudPanel.interactable = visible;
            if (instant) hudPanel.alpha = visible ? 1f : 0f;
            else StartCoroutine(FadeHUD(visible ? 1f : 0f, 0.6f));
        }

        private IEnumerator FadeHUD(float to, float d)
        {
            float from = hudPanel.alpha, t = 0f;
            while (t < d) { t += Time.unscaledDeltaTime; hudPanel.alpha = Mathf.Lerp(from, to, t / d); yield return null; }
            hudPanel.alpha = to;
        }

        private Vector3 ElderFeet() => elderBody != null ? elderBody.position : (elder != null ? Ground(elder.position) : Vector3.zero);

        private Vector3 TalkSpot()
        {
            Vector3 e = ElderFeet();
            Vector3 away = Flat(player.transform.position - e);
            if (away.sqrMagnitude < 0.01f) away = elder != null ? Flat(elder.forward) : Vector3.back;
            return Ground(e + away * talkDistance);
        }

        private Vector3 Ground(Vector3 p)
        {
            var hits = Physics.RaycastAll(p + Vector3.up * 15f, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; Vector3 result = p;
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(player.transform)) continue;
                if (elder != null && h.collider.transform.IsChildOf(elder)) continue;
                if (h.distance < best) { best = h.distance; result = h.point; }
            }
            return result;
        }

        private static Vector3 Flat(Vector3 v, bool normalize = true)
        {
            v.y = 0f;
            return normalize ? (v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward) : v;
        }

        private void OnDisable()
        {
            if (IsPlaying)
            {
                IsPlaying = false;
                if (player != null) player.SetControlsLocked(false);
            }
        }
    }
}
