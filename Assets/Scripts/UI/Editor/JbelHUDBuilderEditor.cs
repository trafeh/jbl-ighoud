using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

namespace JbelIghoud.UI.Editor
{
    public static class JbelHUDBuilderEditor
    {
        [MenuItem("Tools/Jbel Ighoud/Rebuild HUD UI")]
        public static void BuildHUD()
        {
            GameObject canvas = GameObject.Find("MobileHUD_Canvas");
            if (canvas == null)
            {
                Debug.LogError("[JbelHUDBuilder] MobileHUD_Canvas not found in current scene!");
                return;
            }

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/IM_FELL_English_SC SDF.asset");
            if (font == null) font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

            // Load Sprites
            Sprite sPause = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/hud/clean_sprites/btn_pause.png");
            Sprite sPlayerCard = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/hud/clean_sprites/card_player_bg.png");
            Sprite sQuestCard = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/hud/clean_sprites/card_quest_bg.png");
            Sprite sAvatar = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/hud/clean_sprites/frame_avatar.png");
            Sprite sXpBg = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/hud/clean_sprites/bar_xp_bg.png");
            Sprite sXpFill = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/hud/clean_sprites/bar_xp_fill.png");
            Sprite sResBar = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/hud/clean_sprites/bar_resources_bg.png");
            Sprite sStone = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/hud/clean_sprites/icon_stone.png");
            Sprite sCoin = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/hud/clean_sprites/icon_coin.png");
            Sprite sMeat = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/hud/clean_sprites/icon_meat.png");
            Sprite sPlus = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/hud/clean_sprites/btn_plus.png");
            Sprite sCollectBg = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/hud/clean_sprites/btn_collect_bg.png");
            Sprite sBagBg = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/hud/clean_sprites/btn_bag_bg.png");
            Sprite sMapBg = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/hud/clean_sprites/btn_map_bg.png");
            Sprite sJoyKnob = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/hud/clean_sprites/joystick_knob.png");
            Sprite sCollectTxt = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/hud/COLLECT.png");
            Sprite sBagTxt = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/hud/BAG.png");
            Sprite sMapTxt = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/hud/MAP.png");
            Sprite s34 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/hud/34.png");
            Sprite s245 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/hud/245.png");
            Sprite s88 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/hud/88%.png");

            // Remove previous panel if present
            Transform oldPanel = canvas.transform.Find("JbelHUD_Panel");
            if (oldPanel != null)
            {
                Undo.DestroyObjectImmediate(oldPanel.gameObject);
            }

            // Create JbelHUD_Panel
            GameObject hudPanelGO = new GameObject("JbelHUD_Panel", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(hudPanelGO, "Create JbelHUD_Panel");
            hudPanelGO.transform.SetParent(canvas.transform, false);
            hudPanelGO.transform.SetSiblingIndex(4); // In front of background, behind modals

            RectTransform rtPanel = hudPanelGO.GetComponent<RectTransform>();
            rtPanel.anchorMin = Vector2.zero;
            rtPanel.anchorMax = Vector2.one;
            rtPanel.offsetMin = Vector2.zero;
            rtPanel.offsetMax = Vector2.zero;

            JbelHUDController hudCtrl = hudPanelGO.AddComponent<JbelHUDController>();

            // 1. Pause Button
            GameObject pauseGO = new GameObject("PauseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            pauseGO.transform.SetParent(rtPanel, false);
            RectTransform rtPause = pauseGO.GetComponent<RectTransform>();
            rtPause.anchorMin = new Vector2(0, 1);
            rtPause.anchorMax = new Vector2(0, 1);
            rtPause.pivot = new Vector2(0, 1);
            rtPause.anchoredPosition = new Vector2(35, -30);
            rtPause.sizeDelta = new Vector2(95, 95);
            Image imgPause = pauseGO.GetComponent<Image>();
            imgPause.sprite = sPause;
            imgPause.preserveAspect = true;

            // 2. Survivor Card
            GameObject cardGO = new GameObject("SurvivorCard", typeof(RectTransform), typeof(Image));
            cardGO.transform.SetParent(rtPanel, false);
            RectTransform rtCard = cardGO.GetComponent<RectTransform>();
            rtCard.anchorMin = new Vector2(0, 1);
            rtCard.anchorMax = new Vector2(0, 1);
            rtCard.pivot = new Vector2(0, 1);
            rtCard.anchoredPosition = new Vector2(145, -30);
            rtCard.sizeDelta = new Vector2(440, 144);
            Image imgCard = cardGO.GetComponent<Image>();
            imgCard.sprite = sPlayerCard;

            // Avatar Frame
            GameObject avatarGO = new GameObject("AvatarFrame", typeof(RectTransform), typeof(Image));
            avatarGO.transform.SetParent(rtCard, false);
            RectTransform rtAvatar = avatarGO.GetComponent<RectTransform>();
            rtAvatar.anchorMin = new Vector2(0, 0.5f);
            rtAvatar.anchorMax = new Vector2(0, 0.5f);
            rtAvatar.pivot = new Vector2(0.5f, 0.5f);
            rtAvatar.anchoredPosition = new Vector2(72, 0);
            rtAvatar.sizeDelta = new Vector2(112, 112);
            Image imgAvatar = avatarGO.GetComponent<Image>();
            imgAvatar.sprite = sAvatar;

            // Survivor Name Text
            GameObject nameGO = new GameObject("SurvivorName", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameGO.transform.SetParent(rtCard, false);
            RectTransform rtName = nameGO.GetComponent<RectTransform>();
            rtName.anchorMin = new Vector2(0, 1);
            rtName.anchorMax = new Vector2(0, 1);
            rtName.pivot = new Vector2(0, 1);
            rtName.anchoredPosition = new Vector2(145, -22);
            rtName.sizeDelta = new Vector2(280, 32);
            TextMeshProUGUI tmpName = nameGO.GetComponent<TextMeshProUGUI>();
            if (font != null) tmpName.font = font;
            tmpName.text = "SURVIVOR NAME";
            tmpName.fontSize = 24;
            tmpName.fontStyle = FontStyles.Bold;
            tmpName.color = new Color(0.866f, 0.843f, 0.753f); // #DDD7C0

            // Level Text
            GameObject lvlGO = new GameObject("LevelText", typeof(RectTransform), typeof(TextMeshProUGUI));
            lvlGO.transform.SetParent(rtCard, false);
            RectTransform rtLvl = lvlGO.GetComponent<RectTransform>();
            rtLvl.anchorMin = new Vector2(0, 1);
            rtLvl.anchorMax = new Vector2(0, 1);
            rtLvl.pivot = new Vector2(0, 1);
            rtLvl.anchoredPosition = new Vector2(145, -58);
            rtLvl.sizeDelta = new Vector2(200, 24);
            TextMeshProUGUI tmpLvl = lvlGO.GetComponent<TextMeshProUGUI>();
            if (font != null) tmpLvl.font = font;
            tmpLvl.text = "NIVEAU 1";
            tmpLvl.fontSize = 18;
            tmpLvl.color = new Color(0.627f, 0.565f, 0.490f); // #A0907D

            // XP Bar Track
            GameObject xpTrackGO = new GameObject("XPBar_Track", typeof(RectTransform), typeof(Image));
            xpTrackGO.transform.SetParent(rtCard, false);
            RectTransform rtXpTrack = xpTrackGO.GetComponent<RectTransform>();
            rtXpTrack.anchorMin = new Vector2(0, 1);
            rtXpTrack.anchorMax = new Vector2(0, 1);
            rtXpTrack.pivot = new Vector2(0, 1);
            rtXpTrack.anchoredPosition = new Vector2(145, -92);
            rtXpTrack.sizeDelta = new Vector2(270, 16);
            Image imgXpTrack = xpTrackGO.GetComponent<Image>();
            imgXpTrack.sprite = sXpBg;

            // XP Bar Fill
            GameObject xpFillGO = new GameObject("XPBar_Fill", typeof(RectTransform), typeof(Image));
            xpFillGO.transform.SetParent(rtXpTrack, false);
            RectTransform rtXpFill = xpFillGO.GetComponent<RectTransform>();
            rtXpFill.anchorMin = Vector2.zero;
            rtXpFill.anchorMax = Vector2.one;
            rtXpFill.sizeDelta = Vector2.zero;
            Image imgXpFill = xpFillGO.GetComponent<Image>();
            imgXpFill.sprite = sXpFill;
            imgXpFill.type = Image.Type.Filled;
            imgXpFill.fillMethod = Image.FillMethod.Horizontal;
            imgXpFill.fillOrigin = 0;
            imgXpFill.fillAmount = 0.72f;

            // 3. Quest Banner
            GameObject questGO = new GameObject("QuestBanner", typeof(RectTransform), typeof(Image));
            questGO.transform.SetParent(rtPanel, false);
            RectTransform rtQuest = questGO.GetComponent<RectTransform>();
            rtQuest.anchorMin = new Vector2(0, 1);
            rtQuest.anchorMax = new Vector2(0, 1);
            rtQuest.pivot = new Vector2(0, 1);
            rtQuest.anchoredPosition = new Vector2(145, -174);
            rtQuest.sizeDelta = new Vector2(440, 58);
            Image imgQuest = questGO.GetComponent<Image>();
            imgQuest.sprite = sQuestCard;

            // Quest Text
            GameObject questTxtGO = new GameObject("QuestText", typeof(RectTransform), typeof(TextMeshProUGUI));
            questTxtGO.transform.SetParent(rtQuest, false);
            RectTransform rtQuestTxt = questTxtGO.GetComponent<RectTransform>();
            rtQuestTxt.anchorMin = Vector2.zero;
            rtQuestTxt.anchorMax = Vector2.one;
            rtQuestTxt.sizeDelta = Vector2.zero;
            TextMeshProUGUI tmpQuest = questTxtGO.GetComponent<TextMeshProUGUI>();
            if (font != null) tmpQuest.font = font;
            tmpQuest.text = "FIND 100 PIECES OF GOLD";
            tmpQuest.fontSize = 18;
            tmpQuest.alignment = TextAlignmentOptions.Center;
            tmpQuest.fontStyle = FontStyles.Bold;
            tmpQuest.color = new Color(0.976f, 0.945f, 0.875f); // #F9F1DF

            // 4. Resource Bar
            GameObject resGO = new GameObject("ResourceBar", typeof(RectTransform), typeof(Image));
            resGO.transform.SetParent(rtPanel, false);
            RectTransform rtRes = resGO.GetComponent<RectTransform>();
            rtRes.anchorMin = new Vector2(1, 1);
            rtRes.anchorMax = new Vector2(1, 1);
            rtRes.pivot = new Vector2(1, 1);
            rtRes.anchoredPosition = new Vector2(-35, -35);
            rtRes.sizeDelta = new Vector2(580, 72);
            Image imgRes = resGO.GetComponent<Image>();
            imgRes.sprite = sResBar;

            // Stone Group
            GameObject stoneGrp = new GameObject("StoneGroup", typeof(RectTransform));
            stoneGrp.transform.SetParent(rtRes, false);
            RectTransform rtStoneGrp = stoneGrp.GetComponent<RectTransform>();
            rtStoneGrp.anchorMin = new Vector2(0, 0.5f);
            rtStoneGrp.anchorMax = new Vector2(0, 0.5f);
            rtStoneGrp.pivot = new Vector2(0, 0.5f);
            rtStoneGrp.anchoredPosition = new Vector2(24, 0);
            rtStoneGrp.sizeDelta = new Vector2(115, 52);

            GameObject stoneIcon = new GameObject("StoneIcon", typeof(RectTransform), typeof(Image));
            stoneIcon.transform.SetParent(rtStoneGrp, false);
            RectTransform rtStoneIcon = stoneIcon.GetComponent<RectTransform>();
            rtStoneIcon.anchorMin = new Vector2(0, 0.5f);
            rtStoneIcon.anchorMax = new Vector2(0, 0.5f);
            rtStoneIcon.pivot = new Vector2(0, 0.5f);
            rtStoneIcon.anchoredPosition = new Vector2(0, 0);
            rtStoneIcon.sizeDelta = new Vector2(50, 44);
            Image imgStone = stoneIcon.GetComponent<Image>();
            imgStone.sprite = sStone;
            imgStone.preserveAspect = true;

            // Stylized 34 Image
            GameObject stoneImgGO = new GameObject("StoneCountImage", typeof(RectTransform), typeof(Image));
            stoneImgGO.transform.SetParent(rtStoneGrp, false);
            RectTransform rtStoneImg = stoneImgGO.GetComponent<RectTransform>();
            rtStoneImg.anchorMin = new Vector2(0, 0.5f);
            rtStoneImg.anchorMax = new Vector2(0, 0.5f);
            rtStoneImg.pivot = new Vector2(0, 0.5f);
            rtStoneImg.anchoredPosition = new Vector2(56, 0);
            rtStoneImg.sizeDelta = new Vector2(40, 32);
            Image img34 = stoneImgGO.GetComponent<Image>();
            img34.sprite = s34;
            img34.preserveAspect = true;

            // Dynamic TMP Text (fallback / gameplay sync)
            GameObject stoneTxtGO = new GameObject("StoneCountText", typeof(RectTransform), typeof(TextMeshProUGUI));
            stoneTxtGO.transform.SetParent(rtStoneGrp, false);
            RectTransform rtStoneTxt = stoneTxtGO.GetComponent<RectTransform>();
            rtStoneTxt.anchorMin = new Vector2(0, 0.5f);
            rtStoneTxt.anchorMax = new Vector2(0, 0.5f);
            rtStoneTxt.pivot = new Vector2(0, 0.5f);
            rtStoneTxt.anchoredPosition = new Vector2(56, 0);
            rtStoneTxt.sizeDelta = new Vector2(55, 36);
            TextMeshProUGUI tmpStone = stoneTxtGO.GetComponent<TextMeshProUGUI>();
            if (font != null) tmpStone.font = font;
            tmpStone.text = "34";
            tmpStone.fontSize = 24;
            tmpStone.fontStyle = FontStyles.Bold;
            tmpStone.alignment = TextAlignmentOptions.MidlineLeft;
            tmpStone.color = Color.white;
            stoneTxtGO.SetActive(false);

            // Coin Group
            GameObject coinGrp = new GameObject("CoinGroup", typeof(RectTransform));
            coinGrp.transform.SetParent(rtRes, false);
            RectTransform rtCoinGrp = coinGrp.GetComponent<RectTransform>();
            rtCoinGrp.anchorMin = new Vector2(0, 0.5f);
            rtCoinGrp.anchorMax = new Vector2(0, 0.5f);
            rtCoinGrp.pivot = new Vector2(0, 0.5f);
            rtCoinGrp.anchoredPosition = new Vector2(160, 0);
            rtCoinGrp.sizeDelta = new Vector2(135, 52);

            GameObject coinIcon = new GameObject("CoinIcon", typeof(RectTransform), typeof(Image));
            coinIcon.transform.SetParent(rtCoinGrp, false);
            RectTransform rtCoinIcon = coinIcon.GetComponent<RectTransform>();
            rtCoinIcon.anchorMin = new Vector2(0, 0.5f);
            rtCoinIcon.anchorMax = new Vector2(0, 0.5f);
            rtCoinIcon.pivot = new Vector2(0, 0.5f);
            rtCoinIcon.anchoredPosition = new Vector2(0, 0);
            rtCoinIcon.sizeDelta = new Vector2(46, 46);
            Image imgCoin = coinIcon.GetComponent<Image>();
            imgCoin.sprite = sCoin;
            imgCoin.preserveAspect = true;

            // Stylized 245 Image
            GameObject coinImgGO = new GameObject("CoinCountImage", typeof(RectTransform), typeof(Image));
            coinImgGO.transform.SetParent(rtCoinGrp, false);
            RectTransform rtCoinImg = coinImgGO.GetComponent<RectTransform>();
            rtCoinImg.anchorMin = new Vector2(0, 0.5f);
            rtCoinImg.anchorMax = new Vector2(0, 0.5f);
            rtCoinImg.pivot = new Vector2(0, 0.5f);
            rtCoinImg.anchoredPosition = new Vector2(52, 0);
            rtCoinImg.sizeDelta = new Vector2(58, 32);
            Image img245 = coinImgGO.GetComponent<Image>();
            img245.sprite = s245;
            img245.preserveAspect = true;

            // Dynamic TMP Text
            GameObject coinTxtGO = new GameObject("CoinCountText", typeof(RectTransform), typeof(TextMeshProUGUI));
            coinTxtGO.transform.SetParent(rtCoinGrp, false);
            RectTransform rtCoinTxt = coinTxtGO.GetComponent<RectTransform>();
            rtCoinTxt.anchorMin = new Vector2(0, 0.5f);
            rtCoinTxt.anchorMax = new Vector2(0, 0.5f);
            rtCoinTxt.pivot = new Vector2(0, 0.5f);
            rtCoinTxt.anchoredPosition = new Vector2(52, 0);
            rtCoinTxt.sizeDelta = new Vector2(75, 36);
            TextMeshProUGUI tmpCoin = coinTxtGO.GetComponent<TextMeshProUGUI>();
            if (font != null) tmpCoin.font = font;
            tmpCoin.text = "245";
            tmpCoin.fontSize = 24;
            tmpCoin.fontStyle = FontStyles.Bold;
            tmpCoin.alignment = TextAlignmentOptions.MidlineLeft;
            tmpCoin.color = Color.white;
            coinTxtGO.SetActive(false);

            // Food / Meat Group
            GameObject foodGrp = new GameObject("FoodGroup", typeof(RectTransform));
            foodGrp.transform.SetParent(rtRes, false);
            RectTransform rtFoodGrp = foodGrp.GetComponent<RectTransform>();
            rtFoodGrp.anchorMin = new Vector2(0, 0.5f);
            rtFoodGrp.anchorMax = new Vector2(0, 0.5f);
            rtFoodGrp.pivot = new Vector2(0, 0.5f);
            rtFoodGrp.anchoredPosition = new Vector2(310, 0);
            rtFoodGrp.sizeDelta = new Vector2(150, 52);

            GameObject foodIcon = new GameObject("FoodIcon", typeof(RectTransform), typeof(Image));
            foodIcon.transform.SetParent(rtFoodGrp, false);
            RectTransform rtFoodIcon = foodIcon.GetComponent<RectTransform>();
            rtFoodIcon.anchorMin = new Vector2(0, 0.5f);
            rtFoodIcon.anchorMax = new Vector2(0, 0.5f);
            rtFoodIcon.pivot = new Vector2(0, 0.5f);
            rtFoodIcon.anchoredPosition = new Vector2(0, 0);
            rtFoodIcon.sizeDelta = new Vector2(48, 48);
            Image imgFood = foodIcon.GetComponent<Image>();
            imgFood.sprite = sMeat;
            imgFood.preserveAspect = true;

            // Stylized 88% Image
            GameObject foodImgGO = new GameObject("FoodPercentImage", typeof(RectTransform), typeof(Image));
            foodImgGO.transform.SetParent(rtFoodGrp, false);
            RectTransform rtFoodImg = foodImgGO.GetComponent<RectTransform>();
            rtFoodImg.anchorMin = new Vector2(0, 0.5f);
            rtFoodImg.anchorMax = new Vector2(0, 0.5f);
            rtFoodImg.pivot = new Vector2(0, 0.5f);
            rtFoodImg.anchoredPosition = new Vector2(54, 0);
            rtFoodImg.sizeDelta = new Vector2(62, 32);
            Image img88 = foodImgGO.GetComponent<Image>();
            img88.sprite = s88;
            img88.preserveAspect = true;

            // Dynamic TMP Text
            GameObject foodTxtGO = new GameObject("FoodPercentText", typeof(RectTransform), typeof(TextMeshProUGUI));
            foodTxtGO.transform.SetParent(rtFoodGrp, false);
            RectTransform rtFoodTxt = foodTxtGO.GetComponent<RectTransform>();
            rtFoodTxt.anchorMin = new Vector2(0, 0.5f);
            rtFoodTxt.anchorMax = new Vector2(0, 0.5f);
            rtFoodTxt.pivot = new Vector2(0, 0.5f);
            rtFoodTxt.anchoredPosition = new Vector2(54, 0);
            rtFoodTxt.sizeDelta = new Vector2(85, 36);
            TextMeshProUGUI tmpFood = foodTxtGO.GetComponent<TextMeshProUGUI>();
            if (font != null) tmpFood.font = font;
            tmpFood.text = "88%";
            tmpFood.fontSize = 24;
            tmpFood.fontStyle = FontStyles.Bold;
            tmpFood.alignment = TextAlignmentOptions.MidlineLeft;
            tmpFood.color = Color.white;
            foodTxtGO.SetActive(false);

            // Plus Button (exact brown square with beige +)
            GameObject plusGO = new GameObject("PlusButton", typeof(RectTransform), typeof(Image), typeof(Button));
            plusGO.transform.SetParent(rtRes, false);
            RectTransform rtPlus = plusGO.GetComponent<RectTransform>();
            rtPlus.anchorMin = new Vector2(1, 0.5f);
            rtPlus.anchorMax = new Vector2(1, 0.5f);
            rtPlus.pivot = new Vector2(1, 0.5f);
            rtPlus.anchoredPosition = new Vector2(-2, 0);
            rtPlus.sizeDelta = new Vector2(68, 68);
            Image imgPlus = plusGO.GetComponent<Image>();
            imgPlus.sprite = sPlus;
            imgPlus.preserveAspect = true;

            // 5. Action Buttons Cluster
            GameObject clusterGO = new GameObject("ActionButtonsCluster", typeof(RectTransform));
            clusterGO.transform.SetParent(rtPanel, false);
            RectTransform rtCluster = clusterGO.GetComponent<RectTransform>();
            rtCluster.anchorMin = new Vector2(1, 0);
            rtCluster.anchorMax = new Vector2(1, 0);
            rtCluster.pivot = new Vector2(1, 0);
            rtCluster.anchoredPosition = Vector2.zero;
            rtCluster.sizeDelta = new Vector2(500, 500);

            // COLLECT Button
            GameObject colGO = new GameObject("CollectButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup));
            colGO.transform.SetParent(rtCluster, false);
            RectTransform rtCol = colGO.GetComponent<RectTransform>();
            rtCol.anchorMin = new Vector2(1, 0);
            rtCol.anchorMax = new Vector2(1, 0);
            rtCol.pivot = new Vector2(0.5f, 0.5f);
            rtCol.anchoredPosition = new Vector2(-165, 175);
            rtCol.sizeDelta = new Vector2(235, 235);
            Image imgCol = colGO.GetComponent<Image>();
            imgCol.sprite = sCollectBg;
            imgCol.preserveAspect = true;

            GameObject colLbl = new GameObject("CollectLabel", typeof(RectTransform), typeof(Image));
            colLbl.transform.SetParent(colGO.transform, false);
            RectTransform rtColLbl = colLbl.GetComponent<RectTransform>();
            rtColLbl.anchorMin = new Vector2(0.5f, 0.5f);
            rtColLbl.anchorMax = new Vector2(0.5f, 0.5f);
            rtColLbl.pivot = new Vector2(0.5f, 0.5f);
            rtColLbl.anchoredPosition = Vector2.zero;
            rtColLbl.sizeDelta = new Vector2(150, 24);
            Image imgColLbl = colLbl.GetComponent<Image>();
            imgColLbl.sprite = sCollectTxt;
            imgColLbl.preserveAspect = true;

            // BAG Button
            GameObject bagGO = new GameObject("BagButton", typeof(RectTransform), typeof(Image), typeof(Button));
            bagGO.transform.SetParent(rtCluster, false);
            RectTransform rtBag = bagGO.GetComponent<RectTransform>();
            rtBag.anchorMin = new Vector2(1, 0);
            rtBag.anchorMax = new Vector2(1, 0);
            rtBag.pivot = new Vector2(0.5f, 0.5f);
            rtBag.anchoredPosition = new Vector2(-345, 95);
            rtBag.sizeDelta = new Vector2(145, 145);
            Image imgBag = bagGO.GetComponent<Image>();
            imgBag.sprite = sBagBg;
            imgBag.preserveAspect = true;

            GameObject bagLbl = new GameObject("BagLabel", typeof(RectTransform), typeof(Image));
            bagLbl.transform.SetParent(bagGO.transform, false);
            RectTransform rtBagLbl = bagLbl.GetComponent<RectTransform>();
            rtBagLbl.anchorMin = new Vector2(0.5f, 0.5f);
            rtBagLbl.anchorMax = new Vector2(0.5f, 0.5f);
            rtBagLbl.pivot = new Vector2(0.5f, 0.5f);
            rtBagLbl.anchoredPosition = Vector2.zero;
            rtBagLbl.sizeDelta = new Vector2(75, 26);
            Image imgBagLbl = bagLbl.GetComponent<Image>();
            imgBagLbl.sprite = sBagTxt;
            imgBagLbl.preserveAspect = true;

            // MAP Button
            GameObject mapGO = new GameObject("MapButton", typeof(RectTransform), typeof(Image), typeof(Button));
            mapGO.transform.SetParent(rtCluster, false);
            RectTransform rtMap = mapGO.GetComponent<RectTransform>();
            rtMap.anchorMin = new Vector2(1, 0);
            rtMap.anchorMax = new Vector2(1, 0);
            rtMap.pivot = new Vector2(0.5f, 0.5f);
            rtMap.anchoredPosition = new Vector2(-95, 345);
            rtMap.sizeDelta = new Vector2(145, 145);
            Image imgMap = mapGO.GetComponent<Image>();
            imgMap.sprite = sMapBg;
            imgMap.preserveAspect = true;

            GameObject mapLbl = new GameObject("MapLabel", typeof(RectTransform), typeof(Image));
            mapLbl.transform.SetParent(mapGO.transform, false);
            RectTransform rtMapLbl = mapLbl.GetComponent<RectTransform>();
            rtMapLbl.anchorMin = new Vector2(0.5f, 0.5f);
            rtMapLbl.anchorMax = new Vector2(0.5f, 0.5f);
            rtMapLbl.pivot = new Vector2(0.5f, 0.5f);
            rtMapLbl.anchoredPosition = Vector2.zero;
            rtMapLbl.sizeDelta = new Vector2(75, 26);
            Image imgMapLbl = mapLbl.GetComponent<Image>();
            imgMapLbl.sprite = sMapTxt;
            imgMapLbl.preserveAspect = true;

            // 6. Update Joystick Knob Style
            GameObject joyZone = GameObject.Find("JoystickZone");
            MobileJoystick joyComp = null;
            if (joyZone != null)
            {
                joyComp = joyZone.GetComponent<MobileJoystick>();
                Transform handle = joyZone.transform.Find("JoystickHandle");
                if (handle != null)
                {
                    Image imgH = handle.GetComponent<Image>();
                    if (imgH != null && sJoyKnob != null)
                    {
                        imgH.sprite = sJoyKnob;
                        imgH.color = Color.white;
                    }
                }
            }

            // 7. Wire Serialized Fields on JbelHUDController
            SerializedObject so = new SerializedObject(hudCtrl);
            so.FindProperty("pauseButton").objectReferenceValue = pauseGO.GetComponent<Button>();
            so.FindProperty("survivorNameText").objectReferenceValue = tmpName;
            so.FindProperty("levelText").objectReferenceValue = tmpLvl;
            so.FindProperty("xpBarFill").objectReferenceValue = imgXpFill;
            so.FindProperty("avatarImage").objectReferenceValue = imgAvatar;
            so.FindProperty("questBanner").objectReferenceValue = rtQuest;
            so.FindProperty("questText").objectReferenceValue = tmpQuest;
            so.FindProperty("stoneCountImage").objectReferenceValue = img34;
            so.FindProperty("goldCountImage").objectReferenceValue = img245;
            so.FindProperty("foodPercentImage").objectReferenceValue = img88;
            so.FindProperty("stoneCountText").objectReferenceValue = tmpStone;
            so.FindProperty("goldCountText").objectReferenceValue = tmpCoin;
            so.FindProperty("foodPercentText").objectReferenceValue = tmpFood;
            so.FindProperty("plusButton").objectReferenceValue = plusGO.GetComponent<Button>();
            so.FindProperty("collectButton").objectReferenceValue = colGO.GetComponent<Button>();
            so.FindProperty("bagButton").objectReferenceValue = bagGO.GetComponent<Button>();
            so.FindProperty("mapButton").objectReferenceValue = mapGO.GetComponent<Button>();
            so.FindProperty("collectCanvasGroup").objectReferenceValue = colGO.GetComponent<CanvasGroup>();
            if (joyComp != null) so.FindProperty("mobileJoystick").objectReferenceValue = joyComp;
            so.ApplyModifiedProperties();

            // Save Scene
            EditorUtility.SetDirty(hudPanelGO);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("<color=green><b>[JbelHUDBuilder]</b> JbelHUD_Panel built, wired, and saved successfully!</color>");
        }

        [MenuItem("Tools/Jbel Ighoud/Remove Old UI")]
        public static void RemoveOldUI()
        {
            GameObject canvas = GameObject.Find("MobileHUD_Canvas");
            if (canvas == null)
            {
                Debug.LogError("[JbelHUDBuilder] MobileHUD_Canvas not found!");
                return;
            }

            string[] oldUINames = new string[] {
                "JumpButton",
                "PickButton",
                "SmashButton",
                "PutDownButton",
                "InspectButton",
                "DayNightClockWidget",
                "InventoryHUDButton",
                "SurvivalHUD_Panel",
                "DiveButton",
                "ReturnToInventoryButton"
            };

            int removedCount = 0;
            foreach (string name in oldUINames)
            {
                Transform t = canvas.transform.Find(name);
                if (t != null)
                {
                    Undo.DestroyObjectImmediate(t.gameObject);
                    removedCount++;
                    Debug.Log($"[JbelHUDBuilder] Removed legacy UI element: {name}");
                }
            }

            // Also search scene root for any rogue legacy UI objects
            foreach (string name in oldUINames)
            {
                GameObject rogue = GameObject.Find(name);
                if (rogue != null && rogue.transform.parent != canvas.transform && (rogue.transform.parent == null || !rogue.transform.IsChildOf(canvas.transform)))
                {
                    Undo.DestroyObjectImmediate(rogue);
                    removedCount++;
                    Debug.Log($"[JbelHUDBuilder] Removed rogue legacy UI object from scene: {name}");
                }
            }

            EditorUtility.SetDirty(canvas);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log($"<color=green><b>[JbelHUDBuilder]</b> Successfully removed {removedCount} legacy UI elements. Canvas is now completely clean!</color>");
        }

        [MenuItem("Tools/Jbel Ighoud/Rebuild Loading Screen")]
        public static void BuildLoadingScreen()
        {
            GameObject canvas = GameObject.Find("MobileHUD_Canvas");
            if (canvas == null)
            {
                Debug.LogError("[JbelHUDBuilder] MobileHUD_Canvas not found in current scene!");
                return;
            }

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/IM_FELL_English_SC SDF.asset");
            if (font == null) font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

            Sprite sBg = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/loading/loading_background.png");
            Sprite sArt = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/loading/loading_artwork.png");
            Sprite sLogo = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/loading/logo_tecgaming.png");
            Sprite sTip = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/loading/banner_tip.png");
            Sprite sTrack = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/loading/loading_bar_track.png");
            Sprite sFill = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/loading/loading_bar_fill.png");

            Transform oldScreen = canvas.transform.Find("LoadingScreen_Panel");
            if (oldScreen != null)
            {
                Undo.DestroyObjectImmediate(oldScreen.gameObject);
            }

            // 1. Root Loading Screen Panel
            GameObject screenGO = new GameObject("LoadingScreen_Panel", typeof(RectTransform), typeof(CanvasGroup));
            Undo.RegisterCreatedObjectUndo(screenGO, "Create LoadingScreen_Panel");
            screenGO.transform.SetParent(canvas.transform, false);
            screenGO.transform.SetAsLastSibling(); // Ensure it renders on top of everything

            RectTransform rtScreen = screenGO.GetComponent<RectTransform>();
            rtScreen.anchorMin = Vector2.zero;
            rtScreen.anchorMax = Vector2.one;
            rtScreen.offsetMin = Vector2.zero;
            rtScreen.offsetMax = Vector2.zero;

            CanvasGroup cg = screenGO.GetComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.blocksRaycasts = true;
            cg.interactable = true;

            LoadingScreenController ctrl = screenGO.AddComponent<LoadingScreenController>();

            // 2. Background
            GameObject bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGO.transform.SetParent(rtScreen, false);
            RectTransform rtBg = bgGO.GetComponent<RectTransform>();
            rtBg.anchorMin = Vector2.zero;
            rtBg.anchorMax = Vector2.one;
            rtBg.offsetMin = Vector2.zero;
            rtBg.offsetMax = Vector2.zero;
            Image imgBg = bgGO.GetComponent<Image>();
            imgBg.sprite = sBg;
            imgBg.color = Color.white;

            // 3. Studio Logo (Top-Center)
            GameObject logoGO = new GameObject("StudioLogo", typeof(RectTransform), typeof(Image));
            logoGO.transform.SetParent(rtScreen, false);
            RectTransform rtLogo = logoGO.GetComponent<RectTransform>();
            rtLogo.anchorMin = new Vector2(0.5f, 1f);
            rtLogo.anchorMax = new Vector2(0.5f, 1f);
            rtLogo.pivot = new Vector2(0.5f, 1f);
            rtLogo.anchoredPosition = new Vector2(0f, -40f);
            rtLogo.sizeDelta = new Vector2(230f, 115f);
            Image imgLogo = logoGO.GetComponent<Image>();
            imgLogo.sprite = sLogo;
            imgLogo.preserveAspect = true;

            // 4. Center Artwork
            GameObject artGO = new GameObject("CenterArtwork", typeof(RectTransform), typeof(Image));
            artGO.transform.SetParent(rtScreen, false);
            RectTransform rtArt = artGO.GetComponent<RectTransform>();
            rtArt.anchorMin = new Vector2(0.5f, 0.5f);
            rtArt.anchorMax = new Vector2(0.5f, 0.5f);
            rtArt.pivot = new Vector2(0.5f, 0.5f);
            rtArt.anchoredPosition = new Vector2(0f, 40f);
            rtArt.sizeDelta = new Vector2(720f, 540f);
            Image imgArt = artGO.GetComponent<Image>();
            imgArt.sprite = sArt;
            imgArt.preserveAspect = true;

            // 5. Tips Banner (Above Progress Bar)
            GameObject tipGO = new GameObject("TipsBanner", typeof(RectTransform), typeof(Image));
            tipGO.transform.SetParent(rtScreen, false);
            RectTransform rtTip = tipGO.GetComponent<RectTransform>();
            rtTip.anchorMin = new Vector2(0.5f, 0f);
            rtTip.anchorMax = new Vector2(0.5f, 0f);
            rtTip.pivot = new Vector2(0.5f, 0f);
            rtTip.anchoredPosition = new Vector2(0f, 125f);
            rtTip.sizeDelta = new Vector2(560f, 105f);
            Image imgTip = tipGO.GetComponent<Image>();
            imgTip.sprite = sTip;
            imgTip.preserveAspect = true;

            // 6. Progress Bar Track & Fill
            GameObject trackGO = new GameObject("ProgressBar_Track", typeof(RectTransform), typeof(Image));
            trackGO.transform.SetParent(rtScreen, false);
            RectTransform rtTrack = trackGO.GetComponent<RectTransform>();
            rtTrack.anchorMin = new Vector2(0.5f, 0f);
            rtTrack.anchorMax = new Vector2(0.5f, 0f);
            rtTrack.pivot = new Vector2(0.5f, 0f);
            rtTrack.anchoredPosition = new Vector2(0f, 55f);
            rtTrack.sizeDelta = new Vector2(850f, 46f);
            Image imgTrack = trackGO.GetComponent<Image>();
            imgTrack.sprite = sTrack;
            imgTrack.preserveAspect = true;

            GameObject fillGO = new GameObject("ProgressBar_Fill", typeof(RectTransform), typeof(Image));
            fillGO.transform.SetParent(rtTrack, false);
            RectTransform rtFill = fillGO.GetComponent<RectTransform>();
            rtFill.anchorMin = Vector2.zero;
            rtFill.anchorMax = Vector2.one;
            rtFill.offsetMin = new Vector2(6f, 6f);
            rtFill.offsetMax = new Vector2(-6f, -6f);
            Image imgFill = fillGO.GetComponent<Image>();
            imgFill.sprite = sFill;
            imgFill.type = Image.Type.Filled;
            imgFill.fillMethod = Image.FillMethod.Horizontal;
            imgFill.fillOrigin = 0;
            imgFill.fillAmount = 0.5f;

            // Progress Percent Text
            GameObject pctGO = new GameObject("ProgressPercentText", typeof(RectTransform), typeof(TextMeshProUGUI));
            pctGO.transform.SetParent(rtTrack, false);
            RectTransform rtPct = pctGO.GetComponent<RectTransform>();
            rtPct.anchorMin = Vector2.zero;
            rtPct.anchorMax = Vector2.one;
            rtPct.sizeDelta = Vector2.zero;
            TextMeshProUGUI tmpPct = pctGO.GetComponent<TextMeshProUGUI>();
            if (font != null) tmpPct.font = font;
            tmpPct.text = "0%";
            tmpPct.fontSize = 20;
            tmpPct.fontStyle = FontStyles.Bold;
            tmpPct.alignment = TextAlignmentOptions.Center;
            tmpPct.color = new Color(0.976f, 0.945f, 0.875f); // #F9F1DF

            // 7. Wire Serialized Fields on LoadingScreenController
            SerializedObject so = new SerializedObject(ctrl);
            so.FindProperty("canvasGroup").objectReferenceValue = cg;
            so.FindProperty("progressBarFill").objectReferenceValue = imgFill;
            so.FindProperty("progressPercentText").objectReferenceValue = tmpPct;
            so.FindProperty("tipsImage").objectReferenceValue = imgTip;
            so.FindProperty("artworkImage").objectReferenceValue = imgArt;
            so.FindProperty("logoImage").objectReferenceValue = imgLogo;
            so.ApplyModifiedProperties();

            // Save Scene
            EditorUtility.SetDirty(screenGO);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("<color=green><b>[JbelHUDBuilder]</b> LoadingScreen_Panel built, wired, and saved successfully!</color>");
        }
    }
}
