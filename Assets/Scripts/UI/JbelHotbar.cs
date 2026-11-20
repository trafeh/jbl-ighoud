using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace JbelIghoud.UI
{
    /// <summary>
    /// Minecraft-style equipment bar at the bottom of the screen.
    /// - Items that enter the bag fill empty slots automatically (the hotbar is a shortcut row of the bag).
    /// - Tap a slot to equip that item (it goes to the player's hand; anything else held returns to the bag).
    /// - Tap the equipped slot again to put it away.
    /// Art: Assets/UI/equip/slot.png + slot_selected.png.
    /// </summary>
    public class JbelHotbar : MonoBehaviour
    {
        [SerializeField] private Sprite slotSprite;
        [SerializeField] private Sprite selectedSprite;
        [SerializeField] private int slotCount = 5;
        [SerializeField] private float slotSize = 112f;
        [SerializeField] private float spacing = 12f;
        [SerializeField] private float bottomMargin = 22f;

        public static JbelHotbar Instance { get; private set; }

        private class Slot
        {
            public RockItem item;
            public Button button;
            public Image frame, icon;
            public TextMeshProUGUI count;
            public RectTransform rt;
        }

        private readonly List<Slot> slots = new List<Slot>();
        private RectTransform root;
        private InventoryManager inv;
        private float pop;
        private int popIndex = -1;

        private void Awake() { Instance = this; }

        private void Start()
        {
            Build();
            inv = InventoryManager.Instance;
            if (inv != null) inv.OnInventoryChanged += Sync;
            Sync();
        }

        private void OnDestroy()
        {
            if (inv != null) inv.OnInventoryChanged -= Sync;
            if (Instance == this) Instance = null;
        }

        private void Build()
        {
            var canvas = GetComponentInParent<Canvas>();
            var go = new GameObject("Hotbar", typeof(RectTransform));
            go.transform.SetParent(canvas != null ? canvas.transform : transform, false);
            root = (RectTransform)go.transform;
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0f);
            float width = slotCount * slotSize + (slotCount - 1) * spacing;
            root.sizeDelta = new Vector2(width, slotSize);
            root.anchoredPosition = new Vector2(0f, bottomMargin);

            var font = JbelHudButtonFactory.HudFont();
            for (int i = 0; i < slotCount; i++)
            {
                var s = new Slot();
                var sgo = new GameObject("HotbarSlot_" + i, typeof(RectTransform), typeof(Image), typeof(Button));
                sgo.transform.SetParent(root, false);
                s.rt = (RectTransform)sgo.transform;
                s.rt.anchorMin = s.rt.anchorMax = new Vector2(0f, 0.5f);
                s.rt.pivot = new Vector2(0.5f, 0.5f);
                s.rt.sizeDelta = new Vector2(slotSize, slotSize);
                s.rt.anchoredPosition = new Vector2(slotSize * 0.5f + i * (slotSize + spacing), 0f);
                s.frame = sgo.GetComponent<Image>();
                s.frame.sprite = slotSprite;
                if (slotSprite == null) s.frame.color = new Color(0.16f, 0.1f, 0.05f, 0.85f);
                s.button = sgo.GetComponent<Button>();
                s.button.transition = Selectable.Transition.None;
                int idx = i;
                s.button.onClick.AddListener(() => OnSlotTapped(idx));

                var igo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                igo.transform.SetParent(sgo.transform, false);
                var irt = (RectTransform)igo.transform;
                irt.anchorMin = new Vector2(0.14f, 0.14f); irt.anchorMax = new Vector2(0.86f, 0.86f);
                irt.offsetMin = irt.offsetMax = Vector2.zero;
                s.icon = igo.GetComponent<Image>();
                s.icon.preserveAspect = true;
                s.icon.raycastTarget = false;

                var cgo = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
                cgo.transform.SetParent(sgo.transform, false);
                var crt = (RectTransform)cgo.transform;
                crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1f, 0f);
                crt.anchoredPosition = new Vector2(-10f, 6f);
                crt.sizeDelta = new Vector2(70f, 34f);
                s.count = cgo.GetComponent<TextMeshProUGUI>();
                if (font != null) s.count.font = font;
                s.count.fontSize = 26f;
                s.count.fontStyle = FontStyles.Bold;
                s.count.alignment = TextAlignmentOptions.BottomRight;
                s.count.color = new Color(0.98f, 0.86f, 0.55f);
                s.count.outlineWidth = 0.2f;
                s.count.outlineColor = new Color32(25, 15, 6, 255);
                s.count.raycastTarget = false;

                var ngo = new GameObject("Key", typeof(RectTransform), typeof(TextMeshProUGUI));
                ngo.transform.SetParent(sgo.transform, false);
                var nrt = (RectTransform)ngo.transform;
                nrt.anchorMin = nrt.anchorMax = nrt.pivot = new Vector2(0f, 1f);
                nrt.anchoredPosition = new Vector2(12f, -6f);
                nrt.sizeDelta = new Vector2(30f, 28f);
                var num = ngo.GetComponent<TextMeshProUGUI>();
                if (font != null) num.font = font;
                num.text = (i + 1).ToString();
                num.fontSize = 20f;
                num.color = new Color(0.85f, 0.72f, 0.48f, 0.7f);
                num.raycastTarget = false;

                slots.Add(s);
            }
        }

        // ------------------------------------------------------------------ data

        private static bool InBag(RockItem r)
        {
            var inv = InventoryManager.Instance;
            if (inv == null || r == null) return false;
            var items = inv.Items;
            for (int i = 0; i < items.Count; i++) if (items[i] == r) return true;
            return false;
        }

        private static bool Owned(RockItem r)
        {
            if (r == null) return false;
            if (InBag(r)) return true;
            var p = TopDownPlayerController.Instance;
            return p != null && p.HeldRocks != null && Contains(p.HeldRocks, r);
        }

        private static bool Contains(IReadOnlyList<RockItem> list, RockItem r)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == r) return true;
            return false;
        }

        /// <summary>Keep slots in step with the bag: drop items that are gone, fill empty slots with new ones.</summary>
        public void Sync()
        {
            if (slots.Count == 0) return;
            foreach (var s in slots) if (s.item != null && !Owned(s.item)) s.item = null;

            Refresh();
        }

        public bool IsPinned(RockItem r) { foreach (var s in slots) if (s.item == r) return true; return false; }

        /// <summary>Put an item from the bag into the first free hotbar slot (EQUIP in the bag). False if the bar is full.</summary>
        public bool Pin(RockItem r)
        {
            if (r == null) return false;
            if (IsPinned(r)) return true;
            var empty = slots.Find(x => x.item == null);
            if (empty == null) return false;
            empty.item = r;
            popIndex = slots.IndexOf(empty); pop = 1f;
            Refresh();
            return true;
        }

        /// <summary>Remove an item from the hotbar (UNEQUIP); if it was in hand it goes back to the bag.</summary>
        public void Unpin(RockItem r)
        {
            var p = TopDownPlayerController.Instance; var bag = InventoryManager.Instance;
            if (p != null && bag != null && Contains(p.HeldRocks, r)) bag.AddItem(r);
            foreach (var s in slots) if (s.item == r) s.item = null;
            Refresh();
        }

        /// <summary>Forget a pin without touching where the item is (e.g. STORE already put it in the bag).</summary>
        public void ForgetPin(RockItem r)
        {
            foreach (var s in slots) if (s.item == r) s.item = null;
            Refresh();
        }

        /// <summary>Put this item in the player's hand (anything else held goes back to the bag).</summary>
        public void EquipItem(RockItem r)
        {
            var p = TopDownPlayerController.Instance; var bag = InventoryManager.Instance;
            if (r == null || p == null || bag == null) return;
            foreach (var h in new List<RockItem>(p.HeldRocks)) if (h != null && h != r) bag.AddItem(h);
            if (!Contains(p.HeldRocks, r)) bag.EquipToHand(r);
            Refresh();
        }

        private void Refresh()
        {
            var modal = JbelSamplesInventoryModal.Instance;
            var player = TopDownPlayerController.Instance;
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                bool has = s.item != null;
                s.icon.enabled = has;
                if (has && modal != null) s.icon.sprite = modal.GetSpriteForRock(s.item);
                // whole-stone pictures have lots of padding; rendered icons (fragments, hammerstone) don't
                bool rendered = has && (s.item.IsFragment || s.item.isHammerstone);
                s.icon.rectTransform.localScale = Vector3.one * (has ? (rendered ? 1.15f : 1.75f) : 1f);
                s.count.text = has && s.item.IsFragment ? "x" + s.item.fragmentCount : "";
                bool equipped = has && player != null && Contains(player.HeldRocks, s.item);
                s.frame.sprite = equipped && selectedSprite != null ? selectedSprite : slotSprite;
            }
        }

        // ------------------------------------------------------------------ input

        private void OnSlotTapped(int index)
        {
            var s = slots[index];
            var player = TopDownPlayerController.Instance;
            var bag = InventoryManager.Instance;
            if (s.item == null || player == null || bag == null) return;
            if (player.IsControlsLocked) return;

            bool equipped = Contains(player.HeldRocks, s.item);
            // put everything currently in hand back in the bag
            foreach (var h in new List<RockItem>(player.HeldRocks)) if (h != null) bag.AddItem(h);
            if (!equipped) bag.EquipToHand(s.item);   // equip the tapped one (tapping the equipped slot = put away)

            popIndex = index; pop = 1f;
            Refresh();
        }

        private void Update()
        {
            if (root == null) return;
            // hide while striking or when the bag / dialogue covers the screen
            var drag = RockDragManager.Instance;
            var modal = JbelSamplesInventoryModal.Instance;
            bool hide = (drag != null && drag.IsInSmashMode) || (modal != null && modal.IsOpen)
                        || (JbelIghoud.Game.JbelDialogueUI.Instance != null && JbelIghoud.Game.JbelDialogueUI.Instance.IsOpen)
                        || JbelIghoud.Game.JbelIntroCutscene.IsPlaying
                        || slots.TrueForAll(x => x.item == null);   // nothing equipped yet: no bar
            if (root.gameObject.activeSelf == hide) root.gameObject.SetActive(!hide);

            // selection pop
            for (int i = 0; i < slots.Count; i++)
            {
                float k = (i == popIndex) ? 1f + 0.15f * pop : 1f;
                slots[i].rt.localScale = Vector3.one * k;
            }
            if (pop > 0f) pop = Mathf.MoveTowards(pop, 0f, Time.unscaledDeltaTime * 4f);

            // held state can change from elsewhere (STORE button, strike mode...)
            refreshTimer += Time.unscaledDeltaTime;
            if (refreshTimer > 0.25f) { refreshTimer = 0f; Sync(); }
        }

        private float refreshTimer;
    }
}
