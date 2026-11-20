using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using JbelIghoud.UI;

namespace JbelIghoud.Audio
{
    /// <summary>
    /// Person X (Elder NPC Capsule):
    /// - Detects player proximity and displays an authentic prehistoric interaction prompt.
    /// - On interaction (Press [E] or Click/Tap), triggers the Old Man voice dialogue.
    /// - Dispatches timed, synchronized subtitle segments to SubtitleHUDController.
    /// - Automatically faces player and exhibits subtle ambient life animations.
    /// </summary>
    [RequireComponent(typeof(CapsuleCollider))]
    public class PersonXInteractable : MonoBehaviour
    {
        [Header("========================================")]
        [Header("--- DIALOGUE ASSETS & DATA ---")]
        [Header("========================================")]
        [SerializeField] private AudioClip voiceClip;
        [SerializeField] private string characterName = "ELDER";

        [Header("========================================")]
        [Header("--- INTERACTION SETTINGS ---")]
        [Header("========================================")]
        [SerializeField] private float interactionRadius = 4.0f;
        [SerializeField] private Transform playerTransform;

        [Header("========================================")]
        [Header("--- WORLD PROMPT UI ---")]
        [Header("========================================")]
        [SerializeField] private GameObject worldPromptContainer;
        [Tooltip("Floating 'Elder / [E] Talk' label above the head. Off for mobile: the on-screen TALK button is used instead.")]
    [SerializeField] private bool showWorldPrompt = false;
    [SerializeField] private CanvasGroup promptCG;
        [SerializeField] private TextMeshPro promptText;

        private bool isPlayerInRange = false;
        [Tooltip("Old free-talk voice line on tap (TALK button). Off: the Elder only speaks through the story.")]
    [SerializeField] private bool allowTalk = false;
    public bool CanInteract => allowTalk && isPlayerInRange && !isSpeaking && !IsStoryBusy;
        private static bool IsStoryBusy => JbelIghoud.Game.JbelIntroCutscene.IsPlaying || (JbelIghoud.Game.JbelDialogueUI.Instance != null && JbelIghoud.Game.JbelDialogueUI.Instance.IsOpen);

        [Tooltip("Idle bob height. Keep 0 when the Elder has a real body (the animation already breathes).")]
        [SerializeField] private float idleBobAmount = 0.03f;
        private bool isSpeaking = false;
        private bool lastPromptVisible = true;
        private Vector3 initialPosition;
        private Quaternion initialRotation;

        private readonly List<DialogueSubtitleCue> dialogueCues = new List<DialogueSubtitleCue>()
        {
            new DialogueSubtitleCue(0.0f, 6.0f, "The world is a strange place... stranger than most folks care to admit."),
            new DialogueSubtitleCue(6.0f, 14.7f, "Beyond these mountains... lies a land full of secrets, forgotten stories, and treasures waiting to be found."),
            new DialogueSubtitleCue(14.7f, 20.4f, "Every path leads somewhere. Every choice... has a consequence."),
            new DialogueSubtitleCue(20.4f, 25.5f, "Some survive through strength... Others survive through wisdom."),
            new DialogueSubtitleCue(25.5f, 32.0f, "But the ones who truly understand this world... are the ones who never stop learning.")
        };

        private void Awake()
        {
            initialPosition = transform.position;
            initialRotation = transform.rotation;

            if (voiceClip == null)
            {
#if UNITY_EDITOR
                voiceClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/talk/ElevenLabs_2026-11-09T18_03_39_Spuds Oxley - Grandpa _pvc_s50_sim75_v4.wav");
#endif
            }
        }

        private void Start()
        {
            FindPlayer();
            SetPromptVisible(false, true);

            if (GameAudioManager.Instance != null)
            {
                GameAudioManager.Instance.OnVoiceEnded += OnVoiceEnded;
            }
        }

        private void OnDestroy()
        {
            if (GameAudioManager.Instance != null)
            {
                GameAudioManager.Instance.OnVoiceEnded -= OnVoiceEnded;
            }
        }

        private void FindPlayer()
        {
            if (playerTransform == null)
            {
                var ybot = GameObject.Find("Y Bot");
                if (ybot != null)
                {
                    playerTransform = ybot.transform;
                }
                else
                {
                    var pc = Object.FindAnyObjectByType<TopDownPlayerController>();
                    if (pc != null) playerTransform = pc.transform;
                }
            }
        }

        private void Update()
        {
            if (playerTransform == null)
            {
                FindPlayer();
                if (playerTransform == null) return;
            }

            float dist = Vector3.Distance(transform.position, playerTransform.position);
            bool inRange = dist <= interactionRadius;

            if (inRange != isPlayerInRange) isPlayerInRange = inRange;
            bool promptWanted = CanInteract;
            if (promptWanted != lastPromptVisible) { lastPromptVisible = promptWanted; SetPromptVisible(promptWanted, false); }

            // Billboard prompt towards camera
            if (worldPromptContainer != null && Camera.main != null)
            {
                worldPromptContainer.transform.rotation = Quaternion.LookRotation(worldPromptContainer.transform.position - Camera.main.transform.position);
            }

            // Idle breathing animation
            float bob = Mathf.Sin(Time.time * 2.0f) * idleBobAmount;
            transform.position = new Vector3(initialPosition.x, initialPosition.y + bob, initialPosition.z);

            // Turn toward player smoothly when talking or when nearby
            if (isPlayerInRange)
            {
                Vector3 lookDir = (playerTransform.position - transform.position);
                lookDir.y = 0;
                if (lookDir.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(lookDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 3.5f);
                }
            }
            else if (!isSpeaking)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, initialRotation, Time.deltaTime * 2.0f);
            }

            // Check input for interaction
            if (CanInteract)
            {
                bool interactPressed = false;
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                {
                    interactPressed = true;
                }
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKeyDown(KeyCode.E))
                {
                    interactPressed = true;
                }
#endif

                if (interactPressed)
                {
                    Interact();
                }
            }
        }

        public void OnMouseDown()
        {
            if (CanInteract)
            {
                Interact();
            }
        }

        /// <summary>Move the Elder's idle/bob anchor (e.g. after he guided the player somewhere).</summary>
    public void SetHome(Vector3 position, Quaternion rotation)
    {
        initialPosition = position;
        initialRotation = rotation;
    }

    /// <summary>Hide the [E] Talk prompt immediately (used while the Elder is busy guiding).</summary>
    public void HidePrompt()
    {
        lastPromptVisible = false;
        SetPromptVisible(false, true);
    }

    public void Interact()
        {
            if (voiceClip == null)
            {
                Debug.LogWarning("[PersonX] Voice clip not assigned!");
                return;
            }

            isSpeaking = true;
            SetPromptVisible(false, false);

            Debug.Log("<color=gold>[PersonX] Elder begins speaking...</color>");

            // Play voice through centralized manager
            if (GameAudioManager.Instance != null)
            {
                GameAudioManager.Instance.PlayVoice(voiceClip);
            }

            // Trigger synchronized subtitle HUD
            if (SubtitleHUDController.Instance != null)
            {
                SubtitleHUDController.Instance.StartDialogueSequence(characterName, dialogueCues);
            }
        }

        private void OnVoiceEnded()
        {
            isSpeaking = false;
        }

        private void SetPromptVisible(bool visible, bool immediate)
        {
            if (promptCG != null)
            {
                promptCG.alpha = visible ? 1f : 0f;
            }
            else if (worldPromptContainer != null)
            {
                worldPromptContainer.SetActive(visible && showWorldPrompt);
            }
        }
    }
}
