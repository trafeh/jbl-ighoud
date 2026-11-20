using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JbelIghoud.Lithic;
using JbelIghoud.Audio;
using JbelIghoud.UI;

namespace JbelIghoud.Game
{
    /// <summary>
    /// Mission 1 opening, right after the intro cutscene:
    ///  1. "FOLLOW THE ELDER": the Elder walks the route and talks while you walk beside him (speed capped to his pace,
    ///     no sprint/jump). If you get more than <see cref="waitDistance"/> m away he stops, turns and waits for you.
    ///  2. Stones of every type spawn at random spots near the route and gleam when you are within 50 m.
    ///     At the first stone the Elder stops and explains the gleam.
    ///  3. "COLLECT THE SHINING STONES": he keeps walking to the end of the route while you gather them.
    /// Drives two Scripted objectives in <see cref="JbelObjectiveManager"/> (ids "follow_elder" and "collect_stones").
    /// Move the waypoints / rock spots under [ElderGuide] in the scene to restage it.
    /// </summary>
    public class JbelElderGuide : MonoBehaviour
    {
        public const string FollowId = "follow_elder";
        public const string CollectId = "collect_stones";
        public const string OpenBagId = "open_bag";
        public const string ChooseId = "choose_stone";
        public const string StrikeId = "strike_stones";
        public const string FollowCaveId = "follow_cave";

        [Header("--- ACTORS ---")]
        [SerializeField] private TopDownPlayerController player;
        [Tooltip("Elder root (Person_X_OldMan) - this is what moves.")]
        [SerializeField] private Transform elderRoot;
        [SerializeField] private Animator elderAnimator;
        [SerializeField] private JbelElderPose elderPose;
        [SerializeField] private JbelObjectiveManager objectives;

        [Header("--- ROUTE (move these in the scene) ---")]
        [Tooltip("Waypoints the Elder walks, in order.")]
        [SerializeField] private Transform[] path;
        [Tooltip("Candidate stone areas near the route. One stone per type is placed on a random pick of these.")]
        [SerializeField] private Transform[] rockSpots;
        [SerializeField] private float rockSpotJitter = 2.5f;

        [Header("--- STONES ---")]
        [Tooltip("Inactive scene rock used as the clone template.")]
        [SerializeField] private RockItem rockTemplate;
        [Tooltip("One stone of each of these is always spawned (so the strike test always has every type).")]
        [SerializeField] private RockData[] stoneTypes;
        [Tooltip("Lit material with Emission enabled (keeps the glow shader variant in builds).")]
        [SerializeField] private Material shineMaterial;
        [SerializeField] private float shineRadius = 50f;
        [Tooltip("Approximate size of a spawned stone, in metres.")]
        [SerializeField] private float stoneSize = 0.55f;

        [Header("--- PACING ---")]
        [SerializeField] private float elderWalkSpeed = 2.6f;
        [SerializeField] private float elderAnimSpeed = 0.62f;
        [Tooltip("Player max speed while walking beside the Elder.")]
        [SerializeField] private float playerSpeedNearElder = 3.0f;
        [Tooltip("Within this distance the player is held to the Elder's pace (no sprint / jump).")]
        [SerializeField] private float paceRadius = 9f;
        [SerializeField] private float waitDistance = 50f;
        [SerializeField] private float resumeDistance = 8f;
        [SerializeField] private float firstStoneTalkRadius = 4f;

        [Header("--- LINES (placeholder text - final lines from the game designer) ---")]
        [SerializeField] private string speaker = "THE ELDER";
        [SerializeField, TextArea(1, 2)] private string followLine = "Follow me. Stay close.";
        [SerializeField, TextArea(2, 3)] private string[] walkLines =
        {
            "Our people have lived on this mountain for longer than anyone can count.",
            "Down there, the river. Water, fish... and the beasts that come to drink.",
            "The sun warms the stone by day. By night, only fire keeps the cold away.",
            "Watch the ground as we walk. The mountain hides its gifts among the pebbles.",
        };
        [SerializeField, TextArea(2, 3)] private string[] waitLines =
        {
            "Do not wander off, child. Come back to me.",
            "This way. I am waiting.",
        };
        [SerializeField, TextArea(2, 3)] private string[] firstStoneLines =
        {
            "There. Do you see it? That gleam on the ground.",
            "The things you need will often catch the light like this. When something shines, go to it.",
            "Not every stone is the same. Some are soft, some are hard, and one of them can cut.",
            "Gather the shining stones. Each kind, one by one.",
        };
        [SerializeField, TextArea(2, 3)] private string[] collectWalkLines =
        {
            "Good. Keep your eyes on the ground.",
            "Feel their weight in your hand. Each one is different.",
        };
        [SerializeField, TextArea(2, 3)] private string[] bagLines =
        {
            "Your stones are in your bag. Open it, choose one, and break it with the hammerstone.",
        };
        [Header("--- HAMMERSTONE (the Elder's gift) ---")]
        [SerializeField] private RockData hammerstoneData;
        [SerializeField, TextArea(2, 3)] private string[] giveLinesBefore =
        {
            "You have found them all. But a stone keeps its secrets... until you strike it.",
            "Take this. A quartzite cobble from the river. Harder than anything on this mountain.",
        };
        [SerializeField, TextArea(2, 3)] private string[] giveLinesAfter =
        {
            "It will not break. Keep it with you, always. With it, you can open any stone.",
        };
        [Header("--- TO THE CAVE ---")]
        [Tooltip("Waypoints from where the Elder stands after the stones to his cave entrance.")]
        [SerializeField] private Transform[] cavePath;
        [SerializeField, TextArea(2, 3)] private string[] caveIntroLines =
        {
            "You strike well. Your hands already remember what your head forgets.",
            "The sun is going down. Nights on this mountain are cold, and the beasts come out.",
            "Up there is my cave. It is where we sleep, where we keep the fire alive... and where the old ones left their marks on the walls.",
            "Come. Follow me.",
        };
        [SerializeField, TextArea(2, 3)] private string[] caveWalkLines =
        {
            "Stay close. The path climbs here.",
            "From the cave you can see the whole valley. Nothing comes up without us knowing.",
            "My father lived in this cave, and his father before him.",
        };
        [SerializeField, TextArea(2, 3)] private string[] caveArriveLines =
        {
            "Here it is. My home.",
        };
        [Header("--- THE TORCH (lighting the cave) ---")]
        [SerializeField] private Mesh torchMesh;
        [SerializeField] private Material torchMaterial;
        [Tooltip("Where the burning torch stands at the entrance (the Elder picks it up here).")]
        [SerializeField] private Transform torchPickup;
        [Tooltip("Where the Elder stands inside to fix the torch.")]
        [SerializeField] private Transform torchStandSpot;
        [Tooltip("The wall holder: the torch ends up here (pivot at the bottom of the torch).")]
        [SerializeField] private Transform torchMount;
        [Tooltip("Unlit torch already in the cave at the mount (hidden when the lit one is placed).")]
        [SerializeField] private GameObject unlitTorchAtMount;
        [SerializeField, TextArea(1, 2)] private string torchGoLine = "It is dark inside. Let me bring the light.";
        [SerializeField, TextArea(1, 2)] private string torchDoneLine = "There. Now the cave is awake. Come inside.";
        [Tooltip("Strikes needed to finish the first percussion test.")]
        [SerializeField] private int strikeGoal = 3;
        [SerializeField, TextArea(2, 3)] private string[] hintLines =
        {
            "Look around. Some stones are still shining.",
            "You are missing a stone. Follow the gleam.",
        };

        // ------------------------------------------------------------ runtime
        private readonly List<RockItem> stones = new List<RockItem>();
        private readonly HashSet<RockItem> collected = new HashSet<RockItem>();
        private RockItem firstStone;
        private int firstStopIndex;
        private int pathIndex;
        private float footOffset;
        private bool waiting;
        private int waitLineIndex;
        private PersonXInteractable elderInteractable;
        private Quaternion elderFacing;

        // walk-and-talk subtitle (non-blocking)
        private CanvasGroup subGroup;
        private TextMeshProUGUI subSpeaker, subBody;
        private float subHideAt;
        private Coroutine walkTalk;

        public static bool IsGuiding { get; private set; }

        private void Awake()
        {
            if (player == null) player = FindAnyObjectByType<TopDownPlayerController>();
            if (objectives == null) objectives = FindAnyObjectByType<JbelObjectiveManager>();
            if (elderRoot != null)
            {
                if (elderAnimator == null) elderAnimator = elderRoot.GetComponentInChildren<Animator>();
                if (elderPose == null) elderPose = elderRoot.GetComponentInChildren<JbelElderPose>();
                elderInteractable = elderRoot.GetComponent<PersonXInteractable>();
            }
            if (rockTemplate != null) rockTemplate.gameObject.SetActive(false);
        }

        private IEnumerator Start()
        {
            if (elderRoot == null || path == null || path.Length == 0 || objectives == null) yield break;

            // wait for the intro to hand over and the chain to reach one of our steps
            while (!objectives.IsActive) yield return null;
            string id = objectives.CurrentId;
            if (id == OpenBagId || id == ChooseId || id == StrikeId) { EnsureHammerstone(); yield return BagTutorial(); yield return CavePhase(); yield break; }
            if (id == FollowCaveId) { EnsureHammerstone(); yield return CavePhase(); yield break; }
            if (id != FollowId && id != CollectId) { EnsureHammerstone(); yield break; }
            if (id != FollowId && id != CollectId) yield break;

            footOffset = elderRoot.position.y - Ground(elderRoot.position);
            IsGuiding = true;
            if (elderInteractable != null) { elderInteractable.HidePrompt(); elderInteractable.enabled = false; }
            BuildSubtitle();
            SpawnStones();

            if (id == FollowId) yield return FollowPhase();
            else
            {
                // continuing a save mid-collection: Elder waits at the first stop, no first-stone speech
                TeleportElder(path[firstStopIndex].position);
                pathIndex = firstStopIndex + 1;
            }

            yield return CollectPhase();
            Finish();
            yield return BagTutorial();
            yield return CavePhase();
        }

        // ================================================================== PHASES

        private IEnumerator FollowPhase()
        {
            yield return new WaitForSeconds(0.6f);
            FaceElderTowards(player.transform.position);
            if (elderPose != null) elderPose.Wave(1.8f);
            ShowLine(followLine, 3.2f);
            yield return new WaitForSeconds(1.2f);

            walkTalk = StartCoroutine(TalkWhileWalking(walkLines, 2.5f));

            // walk to the waypoint next to the first stone
            while (pathIndex <= firstStopIndex)
            {
                yield return StepElder();
                if (ReachedWaypoint()) pathIndex++;
            }
            SetElderMoving(false);
            if (walkTalk != null) { StopCoroutine(walkTalk); walkTalk = null; }

            // wait by the stone until the player arrives, then explain the gleam
            while (true)
            {
                UpdatePlayerPace();
                Vector3 look = firstStone != null ? firstStone.transform.position : player.transform.position;
                FaceElderTowards(Distance(player.transform.position, elderRoot.position) > 12f ? player.transform.position : look);
                bool nearStone = firstStone == null || Distance(player.transform.position, firstStone.transform.position) <= firstStoneTalkRadius;
                bool nearElder = Distance(player.transform.position, elderRoot.position) <= firstStoneTalkRadius + 1.5f;
                if (nearStone || nearElder) break;
                yield return null;
            }

            HideLine();
            yield return BlockingTalk(firstStoneLines);
            objectives.CompleteScripted(FollowId);

            // the manager plays its own completion beat; wait for the next step
            float timeout = 10f;
            while (objectives.CurrentId != CollectId && timeout > 0f) { timeout -= Time.deltaTime; yield return null; }
        }

        private IEnumerator CollectPhase()
        {
            PushCollectProgress();
            walkTalk = StartCoroutine(TalkWhileWalking(collectWalkLines, 6f));
            float idleAtEnd = 0f;
            int hint = 0;

            while (CountCollected() < stones.Count)
            {
                if (pathIndex < path.Length)
                {
                    yield return StepElder();
                    if (ReachedWaypoint()) pathIndex++;
                }
                else
                {
                    SetElderMoving(false);
                    UpdatePlayerPace();
                    FaceElderTowards(player.transform.position);
                    idleAtEnd += Time.deltaTime;
                    if (idleAtEnd > 25f && hintLines.Length > 0)
                    {
                        idleAtEnd = 0f;
                        ShowLine(hintLines[hint++ % hintLines.Length], 4f);
                    }
                    yield return null;
                }
                PushCollectProgress();
            }

            if (walkTalk != null) { StopCoroutine(walkTalk); walkTalk = null; }
            SetElderMoving(false);
            FaceElderTowards(player.transform.position);
            HideLine();
            ReleasePlayerPace();
            yield return GiveHammerstone();
            yield return BlockingTalk(bagLines);
            objectives.CompleteScripted(CollectId);
        }

        // ================================================================== BAG TUTORIAL

        /// <summary>OPEN YOUR BAG -> TAKE TWO STONES -> STRIKE THE STONES TOGETHER (instructions live in the objective banner).</summary>
        private IEnumerator BagTutorial()
        {
            var modal = JbelSamplesInventoryModal.Instance;
            Button bag = null;
            yield return FindBagButton(b => bag = b);
            if (player.MaxHeldRocks < 2) player.MaxHeldRocks = 2;

            // ---- 1. open the bag (BAG button pulses until pressed)
            yield return WaitForStep(OpenBagId);
            if (objectives.CurrentId == OpenBagId)
            {
                while (modal == null || !modal.IsOpen)
                {
                    if (modal == null) modal = JbelSamplesInventoryModal.Instance;
                    PulseButton(bag, true);
                    yield return null;
                }
                PulseButton(bag, false);
                objectives.CompleteScripted(OpenBagId);
            }

            var drag = RockDragManager.Instance;

            // ---- 2. choose a stone and press BREAK (stones pulse, then BREAK pulses)
            yield return WaitForStep(ChooseId);
            if (objectives.CurrentId == ChooseId)
            {
                while (drag == null || !drag.IsInSmashMode)
                {
                    if (drag == null) drag = RockDragManager.Instance;
                    if (modal == null) modal = JbelSamplesInventoryModal.Instance;
                    if (modal != null) modal.TutorialPulse = true;
                    PulseButton(bag, modal == null || !modal.IsOpen); // bag closed again? point at it
                    yield return null;
                }
                if (modal != null) modal.TutorialPulse = false;
                PulseButton(bag, false);
                objectives.CompleteScripted(ChooseId);
            }

            // ---- 3. break it with the hammerstone: the step ends when a stone actually breaks
            yield return WaitForStep(StrikeId);
            if (objectives.CurrentId == StrikeId)
            {
                if (drag == null) { objectives.CompleteScripted(StrikeId); yield break; }
                drag.showInstructionBanner = false;
                RockItem target = null; bool hadTarget = false; int broken = 0; string shown = null;
                while (broken < 1)
                {
                    if (drag.IsInSmashMode)
                    {
                        if (modal != null) modal.TutorialPulse = false;
                        PulseButton(bag, false);
                        if (!hadTarget && drag.LeftRock != null) { target = drag.LeftRock; hadTarget = true; }
                    }
                    if (hadTarget && target == null) { broken++; hadTarget = false; continue; } // destroyed = broken
                    if (hadTarget && !drag.IsInSmashMode) hadTarget = false;              // put away before it broke

                    // banner: how much of the stone is left
                    string label = (hadTarget && target != null) ? $"{Mathf.CeilToInt(100f * target.HealthLeft / Mathf.Max(1, target.MaxHits))}%" : "0 / 1";
                    if (label != shown) { shown = label; objectives.SetScriptedProgress(0, label); }

                    if (!drag.IsInSmashMode)
                    {
                        if (StonesOwned() > 0)
                        {
                            if (modal == null) modal = JbelSamplesInventoryModal.Instance;
                            if (modal != null) modal.TutorialPulse = true;
                            PulseButton(bag, modal == null || !modal.IsOpen);
                        }
                        else GiveSpareStone(); // never get stuck without a stone to break
                    }
                    yield return null;
                }
                objectives.SetScriptedProgress(1, "1 / 1");
                drag.showInstructionBanner = true;
                if (modal != null) modal.TutorialPulse = false;
                PulseButton(bag, false);
                // let the break effects and the return-to-bag play before the Elder speaks
                yield return new WaitForSeconds(1.2f);
                objectives.CompleteScripted(StrikeId);
            }
        }

        // ================================================================== TO THE CAVE

        private IEnumerator CavePhase()
        {
            yield return WaitForStep(FollowCaveId);
            if (objectives.CurrentId != FollowCaveId || cavePath == null || cavePath.Length == 0) yield break;
            IsGuiding = true;
            if (elderInteractable != null) elderInteractable.enabled = false;
            if (Mathf.Abs(footOffset) < 0.0001f) footOffset = elderRoot.position.y - Ground(elderRoot.position);
            BuildSubtitle();

            // he comes to you and explains
            player.SetControlsLocked(true);
            yield return ApproachPlayer(1.9f);
            yield return BlockingTalk(caveIntroLines);

            // follow him (same rules as the first walk: his pace near him, he waits if you wander off)
            path = cavePath; pathIndex = 0; waiting = false;
            walkTalk = StartCoroutine(TalkWhileWalking(caveWalkLines, 2.5f));
            while (pathIndex < path.Length)
            {
                yield return StepElder();
                if (ReachedWaypoint()) pathIndex++;
            }
            SetElderMoving(false);
            if (walkTalk != null) { StopCoroutine(walkTalk); walkTalk = null; }

            // wait for the player at the entrance
            while (Distance(player.transform.position, elderRoot.position) > 6f)
            {
                UpdatePlayerPace();
                FaceElderTowards(player.transform.position);
                yield return null;
            }
            HideLine();
            ReleasePlayerPace();
            yield return BlockingTalk(caveArriveLines);
            yield return TorchScene();
            objectives.CompleteScripted(FollowCaveId);
            if (elderInteractable != null) { elderInteractable.SetHome(elderRoot.position, elderRoot.rotation); elderInteractable.enabled = true; }
            IsGuiding = false;
        }

        /// <summary>The Elder walks up to the player and stops at the given distance (controls should be locked).</summary>
        private IEnumerator ApproachPlayer(float stopDistance)
        {
            // enough time to cover the whole distance at his walking pace (+ margin)
            float timeout = Distance(player.transform.position, elderRoot.position) / Mathf.Max(0.5f, elderWalkSpeed) + 5f;
            while (Distance(player.transform.position, elderRoot.position) > stopDistance + 0.05f && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                Vector3 to = player.transform.position - elderRoot.position; to.y = 0f;
                Vector3 dir = to.normalized;
                Vector3 next = elderRoot.position + dir * Mathf.Min(elderWalkSpeed * Time.deltaTime, Mathf.Max(0f, to.magnitude - stopDistance));
                next.y = Ground(next) + footOffset;
                elderRoot.position = next;
                elderRoot.rotation = Quaternion.Slerp(elderRoot.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 6f);
                SetElderMoving(true);
                yield return null;
            }
            SetElderMoving(false);
            for (float t = 0f; t < 0.4f; t += Time.deltaTime)
            {
                FaceElderTowards(player.transform.position);
                Vector3 d = elderRoot.position - player.transform.position; d.y = 0f;
                if (d.sqrMagnitude > 0.01f) player.transform.rotation = Quaternion.Slerp(player.transform.rotation, Quaternion.LookRotation(d.normalized), Time.deltaTime * 8f);
                yield return null;
            }
        }

        // ================================================================== THE TORCH

        /// <summary>The Elder takes the burning torch at the entrance, carries it in and fixes it on the wall: the cave lights up.</summary>
        private IEnumerator TorchScene()
        {
            if (torchMesh == null || torchPickup == null || torchStandSpot == null || torchMount == null) yield break;

            // a torch burning at the entrance
            var torch = new GameObject("ElderTorch");
            // the torch mesh is modelled along +Z at 1/100 scale (Blender export): wrap it so the root stands upright, pivot at the bottom
            var meshGo = new GameObject("Mesh");
            meshGo.transform.SetParent(torch.transform, false);
            meshGo.transform.localRotation = Quaternion.Euler(270f, 0f, 0f);
            meshGo.transform.localScale = Vector3.one * 100f;
            meshGo.AddComponent<MeshFilter>().sharedMesh = torchMesh;
            meshGo.AddComponent<MeshRenderer>().sharedMaterial = torchMaterial;
            torch.transform.position = new Vector3(torchPickup.position.x, Ground(torchPickup.position), torchPickup.position.z);
            torch.transform.rotation = Quaternion.Euler(6f, Random.Range(0f, 360f), 0f);
            var flame = torch.AddComponent<JbelTorchFlame>();
            flame.tipHeight = torchMesh.bounds.max.z * 100f - 0.05f;
            flame.intensity01 = 0.55f;   // small while carried
            flame.lightIntensity = 3f;

            ShowLine(torchGoLine, 4f);
            var reach = JbelReachPose.For(elderAnimator);

            // walk to it and pick it up
            yield return WalkElderTo(torchPickup.position, 0.6f, null, reach);
            if (reach != null)
            {
                reach.target = torch.transform.position + Vector3.up * 0.45f;
                for (float t = 0f; t < 0.5f; t += Time.deltaTime) { reach.weight = t / 0.5f; yield return null; }
            }
            // carry it inside, held up in front
            yield return WalkElderTo(torchStandSpot.position, 0.25f, torch.transform, reach);

            // fix it on the wall
            FaceElderTowards(torchMount.position);
            for (float t = 0f; t < 0.4f; t += Time.deltaTime) { FaceElderTowards(torchMount.position); HoldTorch(torch.transform, reach); yield return null; }
            Vector3 from = torch.transform.position; Quaternion fromRot = torch.transform.rotation;
            if (reach != null) reach.target = torchMount.position + Vector3.up * 0.5f;
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 0.6f);
                torch.transform.position = Vector3.Lerp(from, torchMount.position, k);
                torch.transform.rotation = Quaternion.Slerp(fromRot, torchMount.rotation, k);
                yield return null;
            }
            torch.transform.SetPositionAndRotation(torchMount.position, torchMount.rotation);
            if (unlitTorchAtMount != null) unlitTorchAtMount.SetActive(false);
            JbelImpactFX.Haptics(0.3f, 0.5f);

            // the fire takes: the cave lights up
            flame.lightIntensity = 6f;
            for (float t = 0f; t < 2.2f; t += Time.deltaTime)
            {
                flame.intensity01 = Mathf.Lerp(0.55f, 1f, Mathf.SmoothStep(0f, 1f, t / 2.2f));
                if (reach != null) reach.weight = Mathf.MoveTowards(reach.weight, 0f, Time.deltaTime * 2f);
                yield return null;
            }
            if (reach != null) reach.weight = 0f;
            for (float t = 0f; t < 0.5f; t += Time.deltaTime) { FaceElderTowards(player.transform.position); yield return null; }
            ShowLine(torchDoneLine, 5f);
        }

        /// <summary>Simple walk to a point (no waiting logic); optionally carrying a torch in the right hand.</summary>
        private IEnumerator WalkElderTo(Vector3 target, float stopDistance, Transform carried, JbelReachPose reach)
        {
            float timeout = Distance(target, elderRoot.position) / Mathf.Max(0.5f, elderWalkSpeed) + 6f;
            while (Distance(target, elderRoot.position) > stopDistance && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                Vector3 to = target - elderRoot.position; to.y = 0f;
                Vector3 dir = to.normalized;
                Vector3 next = elderRoot.position + dir * Mathf.Min(elderWalkSpeed * 0.8f * Time.deltaTime, to.magnitude);
                next.y = Ground(next) + footOffset;
                elderRoot.position = next;
                elderRoot.rotation = Quaternion.Slerp(elderRoot.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 6f);
                SetElderMoving(true);
                if (carried != null) HoldTorch(carried, reach);
                yield return null;
            }
            SetElderMoving(false);
        }

        private void HoldTorch(Transform torch, JbelReachPose reach)
        {
            if (reach != null)
            {
                reach.weight = Mathf.MoveTowards(reach.weight, 0.7f, Time.deltaTime * 3f);
                reach.target = elderRoot.position + elderRoot.forward * 0.55f + elderRoot.right * 0.2f + Vector3.up * 1.45f;
                torch.position = reach.PalmPosition() - Vector3.up * 0.35f; // held a third of the way up the handle
            }
            else torch.position = elderRoot.position + elderRoot.forward * 0.5f + Vector3.up * 1f;
            torch.rotation = Quaternion.LookRotation(elderRoot.forward) * Quaternion.Euler(-12f, 0f, 0f) * Quaternion.Euler(0f, 0f, 0f);
        }

        // ================================================================== HAMMERSTONE GIFT

        private RockItem SpawnHammerstone(Vector3 pos)
        {
            if (hammerstoneData == null || rockTemplate == null) return null;
            var r = SpawnStone(hammerstoneData, pos);
            var sh = r.GetComponent<JbelStoneShine>();
            if (sh != null) Destroy(sh);
            r.isHammerstone = true;
            r.name = "Hammerstone_Quartzite";
            var mf = r.GetComponent<MeshFilter>();
            float meshSize = (mf != null && mf.sharedMesh != null) ? mf.sharedMesh.bounds.size.magnitude : 1f;
            r.transform.localScale = new Vector3(1.3f, 0.82f, 1f) * (0.26f / Mathf.Max(0.0001f, meshSize)); // river cobble, big enough to read on a phone
            return r;
        }

        /// <summary>Gives the hammerstone silently (e.g. continuing a save after the gift scene).</summary>
        private void EnsureHammerstone()
        {
            if (JbelSamplesInventoryModal.FindHammerstone() != null || InventoryManager.Instance == null) return;
            var h = SpawnHammerstone(player.transform.position + Vector3.up * 2f);
            if (h != null) InventoryManager.Instance.AddItem(h);
        }

        /// <summary>The Elder walks up, explains, and hands over the quartzite hammerstone (hand to hand, then into the bag).</summary>
        private IEnumerator GiveHammerstone()
        {
            if (JbelSamplesInventoryModal.FindHammerstone() != null) yield break;
            if (walkTalk != null) { StopCoroutine(walkTalk); walkTalk = null; }
            HideLine();
            ReleasePlayerPace();
            player.SetControlsLocked(true);

            // the Elder comes to the player
            float timeout = 10f;
            while (Distance(player.transform.position, elderRoot.position) > 1.25f && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                Vector3 to = player.transform.position - elderRoot.position; to.y = 0f;
                Vector3 dir = to.normalized;
                Vector3 next = elderRoot.position + dir * Mathf.Min(elderWalkSpeed * Time.deltaTime, Mathf.Max(0f, to.magnitude - 1.2f));
                next.y = Ground(next) + footOffset;
                elderRoot.position = next;
                elderRoot.rotation = Quaternion.Slerp(elderRoot.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 6f);
                SetElderMoving(true);
                yield return null;
            }
            SetElderMoving(false);

            // face each other
            for (float t = 0f; t < 0.45f; t += Time.deltaTime)
            {
                FaceElderTowards(player.transform.position);
                Vector3 d = elderRoot.position - player.transform.position; d.y = 0f;
                if (d.sqrMagnitude > 0.01f) player.transform.rotation = Quaternion.Slerp(player.transform.rotation, Quaternion.LookRotation(d.normalized), Time.deltaTime * 8f);
                yield return null;
            }

            yield return BlockingTalk(giveLinesBefore, true);

            var elderReach = JbelReachPose.For(elderAnimator);
            var playerReach = JbelReachPose.For(player.GetComponentInChildren<Animator>());
            var stone = SpawnHammerstone(elderRoot.position + Vector3.up * 1.2f);
            if (stone == null || elderReach == null || playerReach == null)
            {
                if (stone != null) InventoryManager.Instance.AddItem(stone);
                yield return BlockingTalk(giveLinesAfter);
                yield break;
            }
            var rb = stone.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
            foreach (var c in stone.GetComponentsInChildren<Collider>()) c.enabled = false;

            Vector3 ep = elderRoot.position, pp = player.transform.position;
            float groundY = Mathf.Min(Ground(ep), Ground(pp));
            Vector3 meet = (ep + pp) * 0.5f; meet.y = groundY + 1.15f;
            elderReach.target = meet;
            playerReach.target = meet;
            Transform holder = elderReach.transform;

            // Elder holds it out
            yield return Reach(elderReach, 0f, 1f, 0.8f, stone, elderReach);
            // player reaches for it
            yield return Reach(playerReach, 0f, 1f, 0.6f, stone, elderReach);
            yield return HoldFor(0.25f, stone, playerReach);
            // hand-over: Elder lets go
            yield return Reach(elderReach, 1f, 0f, 0.5f, stone, playerReach);
            // player brings it in, then it goes into the bag
            playerReach.target = player.transform.position + player.transform.forward * 0.25f + Vector3.up * 1.0f;
            yield return Reach(playerReach, 1f, 0.6f, 0.35f, stone, playerReach);
            Vector3 baseScale = stone.transform.localScale;
            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                stone.transform.position = playerReach.PalmPosition();
                stone.transform.localScale = baseScale * (1f - t / 0.25f);
                playerReach.weight = Mathf.Lerp(0.6f, 0f, t / 0.25f);
                yield return null;
            }
            playerReach.weight = 0f;
            stone.transform.localScale = baseScale;
            foreach (var c in stone.GetComponentsInChildren<Collider>()) c.enabled = true;
            InventoryManager.Instance.AddItem(stone);
            JbelImpactFX.Haptics(0.5f, 0.6f);

            yield return BlockingTalk(giveLinesAfter);
        }

        /// <summary>Animate a reach weight while the stone follows the given hand.</summary>
        private IEnumerator Reach(JbelReachPose arm, float from, float to, float seconds, RockItem stone, JbelReachPose holdingHand)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                arm.weight = Mathf.Lerp(from, to, k);
                if (stone != null) stone.transform.position = holdingHand.PalmPosition();
                FaceElderTowards(player.transform.position);
                yield return null;
            }
            arm.weight = to;
            if (stone != null) stone.transform.position = holdingHand.PalmPosition();
        }

        private IEnumerator HoldFor(float seconds, RockItem stone, JbelReachPose hand)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (stone != null) stone.transform.position = hand.PalmPosition();
                yield return null;
            }
        }

        private static int WholeStonesInBag()
        {
            var inv = InventoryManager.Instance;
            if (inv == null) return 0;
            int c = 0;
            foreach (var it in inv.Items) if (it != null && !it.IsFragment && !it.isHammerstone) c++;
            return c;
        }

        /// <summary>Whole stones the player can still break: in the bag or in hand (not fragments, not the hammerstone).</summary>
        private int StonesOwned()
        {
            int c = WholeStonesInBag();
            foreach (var h in player.HeldRocks) if (h != null && !h.IsFragment && !h.isHammerstone) c++;
            return c;
        }

        private float spareCooldown;
        private void GiveSpareStone()
        {
            if (Time.time < spareCooldown || stoneTypes == null || stoneTypes.Length == 0 || InventoryManager.Instance == null) return;
            spareCooldown = Time.time + 2f;
            var r = SpawnStone(stoneTypes[Random.Range(0, stoneTypes.Length)], player.transform.position + Vector3.up * 2f);
            var sh = r.GetComponent<JbelStoneShine>(); if (sh != null) Destroy(sh);
            InventoryManager.Instance.AddItem(r);
        }

        private IEnumerator WaitForStep(string id)
        {
            float timeout = 12f;
            while (objectives.CurrentId != id && timeout > 0f) { timeout -= Time.unscaledDeltaTime; yield return null; }
        }

        private static IEnumerator FindBagButton(System.Action<Button> found)
        {
            float t = 3f;
            while (t > 0f)
            {
                var hud = FindAnyObjectByType<JbelHUDController>();
                if (hud != null && hud.BagButton != null) { found(hud.BagButton); yield break; }
                t -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private float bagPulseT;
        private Vector3 bagBaseScale = Vector3.zero;
        /// <summary>BAG button scales in/out like \"press me\".</summary>
        private void PulseButton(Button b, bool on)
        {
            if (b == null) return;
            if (bagBaseScale == Vector3.zero) bagBaseScale = b.transform.localScale;
            if (!on) { bagPulseT = 0f; b.transform.localScale = bagBaseScale; return; }
            bagPulseT += Time.unscaledDeltaTime;
            float beat = Mathf.Abs(Mathf.Sin(bagPulseT * 3.6f));
            b.transform.localScale = bagBaseScale * (1f + 0.18f * beat * beat);
        }

        private void Finish()
        {
            IsGuiding = false;
            ReleasePlayerPace();
            SetElderMoving(false);
            if (elderInteractable != null)
            {
                elderInteractable.SetHome(elderRoot.position, elderRoot.rotation); // he stays where the walk ended
                elderInteractable.enabled = true;
            }
            foreach (var s in stones)
                if (s != null) { var sh = s.GetComponent<JbelStoneShine>(); if (sh != null) Destroy(sh); }
        }

        // ================================================================== ELDER MOVEMENT

        /// <summary>One frame of the Elder walking toward the current waypoint (or waiting for the player).</summary>
        private IEnumerator StepElder()
        {
            UpdatePlayerPace();
            float dist = Distance(player.transform.position, elderRoot.position);

            if (!waiting && dist > waitDistance)
            {
                waiting = true;
                SetElderMoving(false);
                if (waitLines.Length > 0) ShowLine(waitLines[waitLineIndex++ % waitLines.Length], 4f);
                if (elderPose != null) elderPose.Wave(1.6f);
            }
            else if (waiting && dist <= resumeDistance)
            {
                waiting = false;
            }

            if (waiting)
            {
                FaceElderTowards(player.transform.position);
                yield return null;
                yield break;
            }

            Vector3 target = path[Mathf.Clamp(pathIndex, 0, path.Length - 1)].position;
            Vector3 to = target - elderRoot.position; to.y = 0f;
            if (to.sqrMagnitude > 0.0001f)
            {
                Vector3 dir = to.normalized;
                float step = Mathf.Min(elderWalkSpeed * Time.deltaTime, to.magnitude);
                Vector3 next = elderRoot.position + dir * step;
                next.y = Ground(next) + footOffset;
                elderRoot.position = next;
                elderFacing = Quaternion.LookRotation(dir);
                elderRoot.rotation = Quaternion.Slerp(elderRoot.rotation, elderFacing, Time.deltaTime * 5f);
                SetElderMoving(true);
            }
            yield return null;
        }

        private bool ReachedWaypoint()
        {
            if (waiting || pathIndex >= path.Length) return false;
            return Distance(elderRoot.position, path[pathIndex].position) < 0.35f;
        }

        private void SetElderMoving(bool moving)
        {
            if (elderAnimator == null) return;
            elderAnimator.SetBool("IsMoving", moving);
            elderAnimator.SetFloat("Speed", moving ? elderWalkSpeed : 0f);
            elderAnimator.SetBool("IsGrounded", true);
            elderAnimator.speed = moving ? elderAnimSpeed : 1f;
        }

        private void FaceElderTowards(Vector3 worldPos)
        {
            Vector3 d = worldPos - elderRoot.position; d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return;
            elderRoot.rotation = Quaternion.Slerp(elderRoot.rotation, Quaternion.LookRotation(d.normalized), Time.deltaTime * 3f);
        }

        private void TeleportElder(Vector3 pos)
        {
            pos.y = Ground(pos) + footOffset;
            elderRoot.position = pos;
        }

        // ================================================================== PLAYER PACE

        private void UpdatePlayerPace()
        {
            if (player == null) return;
            bool near = Distance(player.transform.position, elderRoot.position) <= paceRadius && !waiting;
            player.ExternalSpeedCap = near ? playerSpeedNearElder : -1f;
            player.ExternalJumpLock = near;
            if (near) player.SetSprinting(false);
        }

        private void ReleasePlayerPace()
        {
            if (player == null) return;
            player.ExternalSpeedCap = -1f;
            player.ExternalJumpLock = false;
        }

        // ================================================================== STONES

        private void SpawnStones()
        {
            stones.Clear();
            collected.Clear();
            if (rockTemplate == null || stoneTypes == null || stoneTypes.Length == 0 || rockSpots == null || rockSpots.Length == 0) return;

            // pick random distinct spots, one per stone type
            var spots = new List<Transform>(rockSpots);
            for (int i = spots.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (spots[i], spots[j]) = (spots[j], spots[i]); }
            var types = new List<RockData>(stoneTypes);
            for (int i = types.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (types[i], types[j]) = (types[j], types[i]); }

            int count = Mathf.Min(types.Count, spots.Count);
            for (int i = 0; i < count; i++)
            {
                Vector2 jitter = Random.insideUnitCircle * rockSpotJitter;
                Vector3 p = spots[i].position + new Vector3(jitter.x, 0f, jitter.y);
                p.y = Ground(p) + 0.25f;
                stones.Add(SpawnStone(types[i], p));
            }

            // the "first" stone is the one the route reaches first
            float best = float.MaxValue;
            foreach (var s in stones)
            {
                int wp = NearestWaypoint(s.transform.position);
                float score = wp * 1000f + Distance(path[wp].position, s.transform.position);
                if (score < best) { best = score; firstStone = s; firstStopIndex = wp; }
            }
        }

        private RockItem SpawnStone(RockData data, Vector3 pos)
        {
            var go = Instantiate(rockTemplate.gameObject, pos, Quaternion.Euler(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f)));
            go.name = "GuideStone_" + data.name;
            var rock = go.GetComponent<RockItem>();
            rock.rockData = data;
            var lithic = go.GetComponent<Rock>();
            if (lithic != null) lithic.Data = data; // the Rock component keeps its own copy and would re-apply the template's mesh
            // hand-sized stone (~0.55 m across) whatever the source mesh scale is
            if (data.rockMesh != null)
            {
                float meshSize = Mathf.Max(0.0001f, data.rockMesh.bounds.size.magnitude);
                go.transform.localScale = Vector3.one * (stoneSize / meshSize) * Random.Range(0.85f, 1.15f);
            }
            var rend = go.GetComponent<Renderer>();
            if (rend != null && shineMaterial != null && data.rockMaterial == null) rend.sharedMaterial = shineMaterial;
            go.SetActive(true); // Awake/Start of RockItem apply the data (mesh, colour, durability)
            var shine = go.AddComponent<JbelStoneShine>();
            shine.radius = shineRadius;
            return rock;
        }

        private int CountCollected()
        {
            var inv = InventoryManager.Instance;
            foreach (var s in stones)
            {
                if (collected.Contains(s)) continue;
                bool got = s == null || !s.gameObject.activeInHierarchy || s.isHeld || (inv != null && Contains(inv, s));
                if (got)
                {
                    collected.Add(s);
                    if (s != null) { var sh = s.GetComponent<JbelStoneShine>(); if (sh != null) Destroy(sh); }
                }
            }
            return collected.Count;
        }

        private static bool Contains(InventoryManager inv, RockItem r)
        {
            var items = inv.Items;
            for (int i = 0; i < items.Count; i++) if (items[i] == r) return true;
            return false;
        }

        private void PushCollectProgress()
        {
            int c = CountCollected();
            objectives.SetScriptedProgress(c, $"{c} / {stones.Count}");
        }

        private int NearestWaypoint(Vector3 p)
        {
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < path.Length; i++)
            {
                float d = Distance(path[i].position, p);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        // ================================================================== TALK

        private IEnumerator TalkWhileWalking(string[] lines, float startDelay)
        {
            yield return new WaitForSeconds(startDelay);
            foreach (var line in lines)
            {
                while (waiting || (JbelDialogueUI.Instance != null && JbelDialogueUI.Instance.IsOpen)) yield return null;
                float hold = Mathf.Clamp(2.2f + line.Length * 0.055f, 3f, 8f);
                ShowLine(line, hold);
                if (elderPose != null) elderPose.Talking = true;
                yield return new WaitForSeconds(hold);
                if (elderPose != null) elderPose.Talking = false;
                yield return new WaitForSeconds(1.6f);
            }
        }

        private IEnumerator BlockingTalk(string[] lines) { yield return BlockingTalk(lines, false); }

        private IEnumerator BlockingTalk(string[] lines, bool keepLocked)
        {
            var ui = JbelDialogueUI.Instance;
            if (ui == null || lines == null || lines.Length == 0) yield break;
            ReleasePlayerPace();
            player.SetControlsLocked(true);
            SetElderMoving(false);
            if (elderPose != null) elderPose.Talking = true;
            foreach (var l in lines)
            {
                FaceElderTowards(player.transform.position);
                yield return ui.Say(speaker, l.Replace("{name}", JbelPlayerProfile.PlayerName));
            }
            ui.HideBox();
            if (elderPose != null) elderPose.Talking = false;
            if (!keepLocked) player.SetControlsLocked(false);
        }

        private void ShowLine(string text, float seconds)
        {
            if (subGroup == null) return;
            subSpeaker.text = speaker;
            subBody.text = text.Replace("{name}", JbelPlayerProfile.PlayerName);
            subHideAt = Time.time + seconds;
        }

        private void HideLine() { subHideAt = 0f; }

        private void Update()
        {
            if (subGroup == null) return;
            bool dialogueOpen = JbelDialogueUI.Instance != null && JbelDialogueUI.Instance.IsOpen;
            float target = (Time.time < subHideAt && !dialogueOpen) ? 1f : 0f;
            subGroup.alpha = Mathf.MoveTowards(subGroup.alpha, target, Time.unscaledDeltaTime * 4f);
        }

        private void BuildSubtitle()
        {
            if (subGroup != null) return;
            TMP_FontAsset font = null;
            if (JbelDialogueUI.Instance != null)
            {
                var t = JbelDialogueUI.Instance.GetComponentInChildren<TMP_Text>(true);
                if (t != null) font = t.font;
            }

            var canvasGo = new GameObject("ElderWalkSubtitles", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            subGroup = canvasGo.GetComponent<CanvasGroup>();
            subGroup.alpha = 0f;
            subGroup.blocksRaycasts = false;
            subGroup.interactable = false;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasGo.transform, false);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.anchoredPosition = new Vector2(0f, 150f); // above the hotbar
            prt.sizeDelta = new Vector2(980f, 150f);
            var img = panel.GetComponent<Image>();
            img.color = new Color(0.06f, 0.04f, 0.02f, 0.62f);
            img.raycastTarget = false;

            subSpeaker = MakeText(panel.transform, "Speaker", new Vector2(0f, -14f), 26f, new Color(0.95f, 0.75f, 0.35f), font, FontStyles.Bold);
            subBody = MakeText(panel.transform, "Body", new Vector2(0f, -50f), 32f, Color.white, font, FontStyles.Normal);
            subBody.rectTransform.sizeDelta = new Vector2(920f, 90f);
        }

        private static TextMeshProUGUI MakeText(Transform parent, string name, Vector2 pos, float size, Color color, TMP_FontAsset font, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(920f, 34f);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = TextAlignmentOptions.Top;
            t.enableWordWrapping = true;
            t.raycastTarget = false;
            return t;
        }

        // ================================================================== HELPERS

        private float Ground(Vector3 p)
        {
            // cast from just above the current height so a cave ceiling / overhang is never mistaken for the ground
            var hits = Physics.RaycastAll(new Vector3(p.x, p.y + 3f, p.z), Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue, y = p.y;
            int water = LayerMask.NameToLayer("Water");
            foreach (var h in hits)
            {
                Transform t = h.collider.transform;
                if (h.collider.gameObject.layer == water) continue;
                if (elderRoot != null && t.IsChildOf(elderRoot)) continue;
                if (player != null && t.IsChildOf(player.transform)) continue;
                if (h.collider.attachedRigidbody != null && !h.collider.attachedRigidbody.isKinematic) continue; // loose rocks
                if (h.distance < best) { best = h.distance; y = h.point.y; }
            }
            return y;
        }

        private static float Distance(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }

        private void OnDisable()
        {
            if (IsGuiding) { IsGuiding = false; ReleasePlayerPace(); }
        }
    }
}
