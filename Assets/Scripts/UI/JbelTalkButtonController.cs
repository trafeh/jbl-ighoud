using UnityEngine;
using UnityEngine.UI;
using JbelIghoud.Audio;

namespace JbelIghoud.UI
{
    /// <summary>
    /// Mobile 'TALK' button: only visible/clickable while the player is close to the Elder
    /// (PersonXInteractable.CanInteract). Tapping it starts the Elder's dialogue.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class JbelTalkButtonController : MonoBehaviour
    {
        [SerializeField] private Button button;
        private CanvasGroup cg;
        private PersonXInteractable elder;

        private void Awake()
        {
            cg = GetComponent<CanvasGroup>();
            if (button == null) button = GetComponent<Button>();
            SetVisible(false);
        }

        private void OnEnable()
        {
            if (button == null) button = GetComponent<Button>();
            if (button != null) { button.onClick.RemoveListener(OnClicked); button.onClick.AddListener(OnClicked); }
        }

        private void OnDisable()
        {
            if (button != null) button.onClick.RemoveListener(OnClicked);
        }

        private void Update()
        {
            if (elder == null) elder = Object.FindAnyObjectByType<PersonXInteractable>();
            bool show = elder != null && elder.CanInteract;
            SetVisible(show);
        }

        private void SetVisible(bool v)
        {
            if (cg == null) return;
            float target = v ? 1f : 0f;
            cg.alpha = Mathf.MoveTowards(cg.alpha, target, Time.unscaledDeltaTime * 8f);
            cg.blocksRaycasts = v;
            cg.interactable = v;
        }

        private void OnClicked()
        {
            if (elder != null && elder.CanInteract) elder.Interact();
        }
    }
}
