using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class UIManager : MonoBehaviour
{
    public static UIManager instance;

    // Mobile Input Flags
    public static bool MobileIsFiring = false;
    public static bool MobileJumpPressed = false;
    public static bool MobileReloadPressed = false;
    public static bool MobileSwitchWeaponPressed = false;
    public static bool MobileGrenadePressed = false;

    private Canvas canvas;
    private GameObject hudRoot;
    private Text healthText;
    private Text ammoText;
    private Text grenadesText;
    private Text reloadPromptText;
    private RectTransform healthBarFill;
    private Image healthBarImage;
    
    // New UI Elements
    private Image damageOverlay;
    private Image waveFlashOverlay;
    private float waveFlashTimer = 0f;
    private Color waveFlashColor = Color.clear;
    private Image crosshairImage;
    private RectTransform hitMarkerRoot;
    private Image[] hitMarkerLines = new Image[4];
    
    // Directional Damage
    private RectTransform damageDirRoot;
    private Image damageDirImage;
    private float damageDirTimer = 0f;
    private float damageDirMaxTime = 2f;
    private Transform playerTransform;
    private Vector3 damageSourcePos;

    private float hitMarkerTimer = 0f;
    private float hitMarkerMaxTime = 0.3f;
    private float damageFlashTimer = 0f;
    private float damageFlashMaxTime = 0.5f;
    
    private float targetHealthPct = 1f;
    private float currentHealthPct = 1f;
    private float trailHealthPct = 1f;
    private RectTransform healthBarTrailFill;

    // Hype UI (kill feed + combo + start overlay)
    private Text killFeedText;
    private Text comboText;
    private float killFeedTimer = 0f;
    private float comboTimer = 0f;
    private GameObject startOverlay;

    // Surprise & gifts UI
    private Text buffText;
    private Text eventTimerText;
    private Text achievementText;
    private float buffTimer = 0f;
    private float eventTimerTimer = 0f;
    private float achievementTimer = 0f;
    private Button dailyGiftButton;
    private Text dailyGiftLabel;
    private Text dailyGiftPreviewText;

    // Arabic Abilities & Shield HUD Panel
    private GameObject abilitiesPanel;
    private Text shieldText;
    private RectTransform shieldBarFill;
    private Text dashStatusText;
    private Text slowMoStatusText;
    private Text sonarStatusText;

    // Mystery Gift Beacon HUD
    private GameObject giftBeaconObj;
    private Text giftBeaconText;
    private float giftBeaconTimeout = 0f;

    public static bool IsStartOverlayActive
    {
        get { return instance != null && instance.startOverlay != null && instance.startOverlay.activeSelf; }
    }

    void Awake()
    {
        if (instance == null)
            instance = this;
        else if (instance != this)
            Destroy(gameObject);
            
#if UNITY_ANDROID || UNITY_IOS
        // Force Landscape orientation for mobile FPS to prevent layout squishing
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.orientation = ScreenOrientation.AutoRotation;
#endif

        // Hide old legacy UI elements that overlap with our new UI
        GameObject oldHUD = GameObject.Find("HUD_bullets");
        if (oldHUD != null) oldHUD.SetActive(false);

        SetupUI();
    }

    void SetupUI()
    {
        // Ensure EventSystem exists for UI interactions
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        // 1. Canvas Setup
        GameObject canvasObj = new GameObject("ModernCanvasUI");
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10; // Ensure it's on top
        
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // Gameplay HUD root (hidden cleanly during Start Screen and Game Over)
        hudRoot = new GameObject("GameplayHUDRoot");
        hudRoot.transform.SetParent(canvasObj.transform, false);
        RectTransform hudRt = hudRoot.AddComponent<RectTransform>();
        hudRt.anchorMin = Vector2.zero;
        hudRt.anchorMax = Vector2.one;
        hudRt.offsetMin = Vector2.zero;
        hudRt.offsetMax = Vector2.zero;
        hudRoot.SetActive(false);

        // 1.2 Hype UI (lives under hudRoot)
        SetupHypeUI(hudRoot.transform);

        // 1.5 Damage Vignette
        GameObject vignetteObj = new GameObject("DamageVignette");
        vignetteObj.transform.SetParent(hudRoot.transform, false);
        RectTransform vRt = vignetteObj.AddComponent<RectTransform>();
        vRt.anchorMin = Vector2.zero;
        vRt.anchorMax = Vector2.one;
        vRt.sizeDelta = Vector2.zero;
        damageOverlay = vignetteObj.AddComponent<Image>();
        damageOverlay.color = new Color(1, 0, 0, 0); // Transparent red
        damageOverlay.raycastTarget = false;

        // 1.55 Wave Transition Flash Overlay
        GameObject waveFlashObj = new GameObject("WaveFlashOverlay");
        waveFlashObj.transform.SetParent(hudRoot.transform, false);
        RectTransform wfRt = waveFlashObj.AddComponent<RectTransform>();
        wfRt.anchorMin = Vector2.zero;
        wfRt.anchorMax = Vector2.one;
        wfRt.sizeDelta = Vector2.zero;
        waveFlashOverlay = waveFlashObj.AddComponent<Image>();
        waveFlashOverlay.color = Color.clear;
        waveFlashOverlay.raycastTarget = false;

        // 1.6 Mobile Touch Controls (Only active on Mobile)
#if UNITY_ANDROID || UNITY_IOS
        SetupMobileControls(hudRoot.transform);
#endif

        // 3. Crosshair (Center Dot)
        GameObject crosshairObj = new GameObject("Crosshair");
        crosshairObj.transform.SetParent(hudRoot.transform, false);
        crosshairImage = crosshairObj.AddComponent<Image>();
        crosshairImage.color = new Color(1, 1, 1, 0.85f);
        crosshairImage.raycastTarget = false;
        RectTransform crt = crosshairObj.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(6, 6); // Small dot
        Outline cOut = crosshairObj.AddComponent<Outline>();
        cOut.effectColor = Color.black;
        cOut.effectDistance = new Vector2(1, -1);

        // 4. Modern Hit Marker
        GameObject hitRootObj = new GameObject("HitMarkerRoot");
        hitRootObj.transform.SetParent(hudRoot.transform, false);
        hitMarkerRoot = hitRootObj.AddComponent<RectTransform>();
        hitMarkerRoot.anchorMin = new Vector2(0.5f, 0.5f);
        hitMarkerRoot.anchorMax = new Vector2(0.5f, 0.5f);
        hitMarkerRoot.sizeDelta = new Vector2(40, 40);
        
        // Create 4 lines for the hit marker
        for (int i = 0; i < 4; i++)
        {
            GameObject lineObj = new GameObject("HitLine_" + i);
            lineObj.transform.SetParent(hitMarkerRoot, false);
            Image lineImg = lineObj.AddComponent<Image>();
            lineImg.color = new Color(1, 1, 1, 0); // Transparent initially
            RectTransform lrt = lineObj.GetComponent<RectTransform>();
            lrt.sizeDelta = new Vector2(4, 16);
            lrt.pivot = new Vector2(0.5f, -0.5f); // Pivot at bottom to push outwards
            lrt.localRotation = Quaternion.Euler(0, 0, 45 + (i * 90));
            hitMarkerLines[i] = lineImg;
        }

        // 4.2 Low-Ammo / Reload Prompt (below crosshair so player never gets surprised)
        GameObject reloadObj = new GameObject("ReloadPromptText");
        reloadObj.transform.SetParent(hudRoot.transform, false);
        reloadPromptText = reloadObj.AddComponent<Text>();
        UIStyle.ConfigureArabicText(reloadPromptText, 26, new Color(1f, 0.80f, 0.2f, 1f), true);
        reloadPromptText.raycastTarget = false;
        RectTransform rprt = reloadPromptText.rectTransform;
        rprt.anchorMin = new Vector2(0.5f, 0.5f);
        rprt.anchorMax = new Vector2(0.5f, 0.5f);
        rprt.anchoredPosition = new Vector2(0, -72);
        rprt.sizeDelta = new Vector2(420, 48);
        reloadObj.SetActive(false);

        // 4.5 Directional Damage Indicator
        GameObject dDirRootObj = new GameObject("DamageDirectionRoot");
        dDirRootObj.transform.SetParent(hudRoot.transform, false);
        damageDirRoot = dDirRootObj.AddComponent<RectTransform>();
        damageDirRoot.anchorMin = new Vector2(0.5f, 0.5f);
        damageDirRoot.anchorMax = new Vector2(0.5f, 0.5f);
        damageDirRoot.sizeDelta = new Vector2(300, 300); // Orbit radius around center
        
        GameObject dDirImgObj = new GameObject("DamageDirectionImage");
        dDirImgObj.transform.SetParent(dDirRootObj.transform, false);
        damageDirImage = dDirImgObj.AddComponent<Image>();
        damageDirImage.color = new Color(1, 0, 0, 0); // Transparent initially
        RectTransform ddRt = dDirImgObj.GetComponent<RectTransform>();
        ddRt.anchorMin = new Vector2(0.5f, 1f);
        ddRt.anchorMax = new Vector2(0.5f, 1f);
        ddRt.pivot = new Vector2(0.5f, 0f);
        ddRt.anchoredPosition = new Vector2(0, 100); // Push out from center
        ddRt.sizeDelta = new Vector2(120, 25);

        // 5. Health UI (Rounded dark-glass card)
        GameObject hpBgObj = new GameObject("HealthPanelBG");
        hpBgObj.transform.SetParent(hudRoot.transform, false);
        Image hpBgImg = hpBgObj.AddComponent<Image>();
        UIStyle.StyleCard(hpBgImg, new Color(0.05f, 0.08f, 0.13f, 0.84f));
        RectTransform hBgRt = hpBgObj.GetComponent<RectTransform>();
        hBgRt.anchorMin = new Vector2(0, 0);
        hBgRt.anchorMax = new Vector2(0, 0);
        hBgRt.pivot = new Vector2(0, 0);
        hBgRt.anchoredPosition = new Vector2(36, 36);
        hBgRt.sizeDelta = new Vector2(340, 92);

        GameObject healthObj = new GameObject("HealthText");
        healthObj.transform.SetParent(hpBgObj.transform, false);
        healthText = healthObj.AddComponent<Text>();
        UIStyle.ConfigureArabicText(healthText, 30, Color.white, true);
        RectTransform hrt = healthText.GetComponent<RectTransform>();
        hrt.anchorMin = Vector2.zero;
        hrt.anchorMax = Vector2.one;
        hrt.offsetMin = new Vector2(12, 22);
        hrt.offsetMax = new Vector2(-12, -4);

        // Health Bar Track
        GameObject trackObj = new GameObject("HealthBarTrack");
        trackObj.transform.SetParent(hpBgObj.transform, false);
        Image trackImg = trackObj.AddComponent<Image>();
        trackImg.color = new Color(0.15f, 0.20f, 0.28f, 1f);
        RectTransform tRt = trackObj.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0, 0);
        tRt.anchorMax = new Vector2(1, 0);
        tRt.pivot = new Vector2(0.5f, 0);
        tRt.anchoredPosition = new Vector2(0, 12);
        tRt.sizeDelta = new Vector2(-28, 12);
        
        // Health Bar Trail
        GameObject trailObj = new GameObject("HealthBarTrail");
        trailObj.transform.SetParent(trackObj.transform, false);
        Image trailImg = trailObj.AddComponent<Image>();
        trailImg.color = new Color(1f, 0.88f, 0.2f, 1f);
        healthBarTrailFill = trailObj.GetComponent<RectTransform>();
        healthBarTrailFill.anchorMin = new Vector2(0, 0);
        healthBarTrailFill.anchorMax = new Vector2(1, 1);
        healthBarTrailFill.pivot = new Vector2(0, 0.5f);
        healthBarTrailFill.offsetMin = Vector2.zero;
        healthBarTrailFill.offsetMax = Vector2.zero;

        // Health Bar Fill
        GameObject fillObj = new GameObject("HealthBarFill");
        fillObj.transform.SetParent(trackObj.transform, false);
        healthBarImage = fillObj.AddComponent<Image>();
        healthBarImage.color = new Color(0.18f, 0.84f, 0.42f, 1f);
        healthBarFill = fillObj.GetComponent<RectTransform>();
        healthBarFill.anchorMin = new Vector2(0, 0);
        healthBarFill.anchorMax = new Vector2(1, 1);
        healthBarFill.pivot = new Vector2(0, 0.5f);
        healthBarFill.offsetMin = Vector2.zero;
        healthBarFill.offsetMax = Vector2.zero;

        // 6. Ammo UI (Rounded dark-glass card, top-right)
        GameObject ammoBgObj = new GameObject("AmmoPanelBG");
        ammoBgObj.transform.SetParent(hudRoot.transform, false);
        Image ammoBgImg = ammoBgObj.AddComponent<Image>();
        UIStyle.StyleCard(ammoBgImg, new Color(0.05f, 0.08f, 0.13f, 0.84f));
        RectTransform aBgRt = ammoBgObj.GetComponent<RectTransform>();
        aBgRt.anchorMin = new Vector2(1, 1);
        aBgRt.anchorMax = new Vector2(1, 1);
        aBgRt.pivot = new Vector2(1, 1);
        aBgRt.anchoredPosition = new Vector2(-28, -22);
        aBgRt.sizeDelta = new Vector2(220, 72);

        GameObject ammoObj = new GameObject("AmmoText");
        ammoObj.transform.SetParent(ammoBgObj.transform, false);
        ammoText = ammoObj.AddComponent<Text>();
        UIStyle.ConfigureArabicText(ammoText, 34, Color.white, true);
        RectTransform art = ammoText.GetComponent<RectTransform>();
        art.anchorMin = Vector2.zero;
        art.anchorMax = Vector2.one;
        art.offsetMin = Vector2.zero;
        art.offsetMax = Vector2.zero;

        // 7. Grenades UI (Rounded pill below ammo)
        GameObject grenadeBgObj = new GameObject("GrenadePanelBG");
        grenadeBgObj.transform.SetParent(hudRoot.transform, false);
        Image grenadeBgImg = grenadeBgObj.AddComponent<Image>();
        UIStyle.StyleCard(grenadeBgImg, new Color(0.05f, 0.08f, 0.13f, 0.84f));
        RectTransform gBgRt = grenadeBgObj.GetComponent<RectTransform>();
        gBgRt.anchorMin = new Vector2(1, 1);
        gBgRt.anchorMax = new Vector2(1, 1);
        gBgRt.pivot = new Vector2(1, 1);
        gBgRt.anchoredPosition = new Vector2(-28, -104);
        gBgRt.sizeDelta = new Vector2(170, 54);

        GameObject grenadeObj = new GameObject("GrenadeText");
        grenadeObj.transform.SetParent(grenadeBgObj.transform, false);
        grenadesText = grenadeObj.AddComponent<Text>();
        UIStyle.ConfigureArabicText(grenadesText, 24, Color.white, true);
        RectTransform grt = grenadesText.GetComponent<RectTransform>();
        grt.anchorMin = Vector2.zero;
        grt.anchorMax = Vector2.one;
        grt.offsetMin = Vector2.zero;
        grt.offsetMax = Vector2.zero;

        UpdateHealth(100);
        UpdateAmmo(30, 90);
        UpdateGrenades(3);
        SetupAbilitiesPanel(hudRoot.transform);
        SetupGiftBeaconUI(hudRoot.transform);

        // Start overlay created LAST with dedicated high sortingOrder so it sits above all HUD & GameManager announcements
        SetupStartOverlay(canvasObj.transform);
    }

    private void SetupAbilitiesPanel(Transform parent)
    {
        abilitiesPanel = new GameObject("AbilitiesPanelBG");
        abilitiesPanel.transform.SetParent(parent, false);
        Image bg = abilitiesPanel.AddComponent<Image>();
        UIStyle.StyleCard(bg, new Color(0.05f, 0.08f, 0.13f, 0.84f));
        RectTransform rt = abilitiesPanel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(0, 0);
        rt.pivot = new Vector2(0, 0);
        rt.anchoredPosition = new Vector2(36, 140); // Directly above bottom-left Health bar
        rt.sizeDelta = new Vector2(340, 174);

        GameObject topAccent = new GameObject("Accent");
        topAccent.transform.SetParent(abilitiesPanel.transform, false);
        Image accImg = topAccent.AddComponent<Image>();
        accImg.color = new Color(0f, 0.82f, 1f, 0.9f);
        RectTransform accRt = topAccent.GetComponent<RectTransform>();
        accRt.anchorMin = new Vector2(0.04f, 1);
        accRt.anchorMax = new Vector2(0.96f, 1);
        accRt.pivot = new Vector2(0.5f, 1);
        accRt.offsetMin = new Vector2(0, -3);
        accRt.offsetMax = Vector2.zero;

        shieldText = CreateAbilityRow(abilitiesPanel.transform, "ShieldText", -22, 20, Color.white);
        shieldText.text = ArabicUI.Format(ArabicStrings.ShieldTemplate, 250, 250);

        GameObject sTrack = new GameObject("ShieldTrack");
        sTrack.transform.SetParent(abilitiesPanel.transform, false);
        Image stImg = sTrack.AddComponent<Image>();
        stImg.color = new Color(0.14f, 0.20f, 0.30f, 1f);
        RectTransform stRt = sTrack.GetComponent<RectTransform>();
        stRt.anchorMin = new Vector2(0, 1);
        stRt.anchorMax = new Vector2(1, 1);
        stRt.pivot = new Vector2(0.5f, 1);
        stRt.anchoredPosition = new Vector2(0, -44);
        stRt.sizeDelta = new Vector2(-28, 10);

        GameObject sFill = new GameObject("ShieldFill");
        sFill.transform.SetParent(sTrack.transform, false);
        Image sfImg = sFill.AddComponent<Image>();
        sfImg.color = new Color(0f, 0.84f, 1f, 1f);
        shieldBarFill = sFill.GetComponent<RectTransform>();
        shieldBarFill.anchorMin = Vector2.zero;
        shieldBarFill.anchorMax = Vector2.one;
        shieldBarFill.offsetMin = Vector2.zero;
        shieldBarFill.offsetMax = Vector2.zero;

        dashStatusText = CreateAbilityRow(abilitiesPanel.transform, "DashText", -72, 19, new Color(0.25f, 1f, 0.55f));
        slowMoStatusText = CreateAbilityRow(abilitiesPanel.transform, "SlowMoText", -100, 19, new Color(0.25f, 1f, 0.55f));
        sonarStatusText = CreateAbilityRow(abilitiesPanel.transform, "SonarText", -128, 19, new Color(0.25f, 1f, 0.55f));
        Text djText = CreateAbilityRow(abilitiesPanel.transform, "DoubleJumpText", -154, 16, new Color(0.75f, 0.83f, 0.94f));
        djText.text = ArabicStrings.AbilityDoubleJump;
    }

    private Text CreateAbilityRow(Transform parent, string name, float yOffset, int fontSize, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Text txt = obj.AddComponent<Text>();
        UIStyle.ConfigureArabicText(txt, fontSize, color, true, TextAnchor.MiddleRight);
        txt.raycastTarget = false;
        RectTransform rt = txt.rectTransform;
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, yOffset);
        rt.sizeDelta = new Vector2(-28, 28);
        return txt;
    }

    private void SetupGiftBeaconUI(Transform parent)
    {
        giftBeaconObj = new GameObject("GiftBeaconBanner");
        giftBeaconObj.transform.SetParent(parent, false);
        Image bg = giftBeaconObj.AddComponent<Image>();
        UIStyle.StyleCard(bg, new Color(0.14f, 0.10f, 0.02f, 0.90f));
        RectTransform rt = giftBeaconObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0, -108);
        rt.sizeDelta = new Vector2(390, 48);

        GameObject txtObj = new GameObject("GiftBeaconText");
        txtObj.transform.SetParent(giftBeaconObj.transform, false);
        giftBeaconText = txtObj.AddComponent<Text>();
        UIStyle.ConfigureArabicText(giftBeaconText, 22, new Color(1f, 0.86f, 0.25f, 1f), true);
        giftBeaconText.raycastTarget = false;
        RectTransform trt = giftBeaconText.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(12, 2);
        trt.offsetMax = new Vector2(-12, -2);

        giftBeaconObj.SetActive(false);
    }

    public void UpdateAbilitiesHUD(float shield, float maxShield, float dashCd, float slowMoCd, bool slowMoActive, float sonarCd)
    {
        if (abilitiesPanel == null) return;
        bool visible = !IsStartOverlayActive && (GameManager.instance == null || !GameManager.instance.isGameOver);
        if (abilitiesPanel.activeSelf != visible) abilitiesPanel.SetActive(visible);
        if (!visible) return;

        if (shieldText != null)
            shieldText.text = ArabicUI.Format(ArabicStrings.ShieldTemplate, Mathf.CeilToInt(shield), Mathf.CeilToInt(maxShield));
        if (shieldBarFill != null)
            shieldBarFill.anchorMax = new Vector2(Mathf.Clamp01(maxShield > 0f ? shield / maxShield : 0f), 1f);

        Color readyCol = new Color(0.25f, 1f, 0.55f, 1f);
        Color cdCol = new Color(1f, 0.72f, 0.2f, 1f);
        Color activeCol = new Color(0.2f, 0.9f, 1f, 1f);

        if (dashStatusText != null)
        {
            dashStatusText.text = dashCd <= 0f ? ArabicStrings.AbilityDashReady : ArabicUI.Format(ArabicStrings.AbilityDashCd, dashCd.ToString("F1"));
            dashStatusText.color = dashCd <= 0f ? readyCol : cdCol;
        }
        if (slowMoStatusText != null)
        {
            if (slowMoActive) { slowMoStatusText.text = ArabicStrings.AbilitySlowMoActive; slowMoStatusText.color = activeCol; }
            else if (slowMoCd <= 0f) { slowMoStatusText.text = ArabicStrings.AbilitySlowMoReady; slowMoStatusText.color = readyCol; }
            else { slowMoStatusText.text = ArabicUI.Format(ArabicStrings.AbilitySlowMoCd, slowMoCd.ToString("F1")); slowMoStatusText.color = cdCol; }
        }
        if (sonarStatusText != null)
        {
            sonarStatusText.text = sonarCd <= 0f ? ArabicStrings.AbilitySonarReady : ArabicUI.Format(ArabicStrings.AbilitySonarCd, sonarCd.ToString("F1"));
            sonarStatusText.color = sonarCd <= 0f ? readyCol : cdCol;
        }
    }

    public void UpdateGiftBeacon(Vector3 giftWorldPos, float remainingSeconds = -1f)
    {
        if (giftBeaconObj == null || giftBeaconText == null) return;
        if (IsStartOverlayActive || (GameManager.instance != null && GameManager.instance.isGameOver))
        {
            giftBeaconObj.SetActive(false);
            return;
        }

        Transform cam = Camera.main != null ? Camera.main.transform : null;
        if (cam == null) return;

        Vector3 toGift = giftWorldPos - cam.position;
        int meters = Mathf.Max(1, Mathf.RoundToInt(toGift.magnitude));
        toGift.y = 0f;
        Vector3 fwd = cam.forward;
        fwd.y = 0f;
        float angle = Vector3.SignedAngle(fwd.normalized, toGift.normalized, Vector3.up);
        string dirArrow = Mathf.Abs(angle) <= 30f ? "▲" : (angle > 30f && angle < 150f ? "▶" : (angle < -30f && angle > -150f ? "◀" : "▼"));

        string timerSuffix = remainingSeconds >= 0f ? "  (" + Mathf.CeilToInt(remainingSeconds) + "s)" : "";
        giftBeaconObj.SetActive(true);
        giftBeaconText.text = dirArrow + "  " + ArabicUI.Format(ArabicStrings.GiftBeaconLabel, meters) + timerSuffix;
        giftBeaconTimeout = 0.25f;
    }

    public void ClearGiftBeacon()
    {
        if (giftBeaconObj != null) giftBeaconObj.SetActive(false);
        giftBeaconTimeout = 0f;
    }

    public void UpdateHealth(int health, int maxHealth = 100)
    {
        if (healthText != null)
        {
            healthText.text = ArabicUI.Format(ArabicStrings.HealthTemplate, health);
        }

        if (healthBarFill != null)
        {
            targetHealthPct = Mathf.Clamp01((float)health / maxHealth);
        }
    }

    public void UpdateAmmo(int current, int max)
    {
        if (ammoText != null)
        {
            ammoText.text = current.ToString() + " / " + max.ToString();
            if (current == 0) ammoText.color = Color.red;
            else if (current <= 3) ammoText.color = new Color(1f, 0.78f, 0.15f, 1f);
            else ammoText.color = Color.white;
        }
    }

    public void UpdateReloadPrompt(int currentInMag, int reserveAmmo, bool isReloading)
    {
        if (reloadPromptText == null) return;
        if (IsStartOverlayActive || (GameManager.instance != null && GameManager.instance.isGameOver))
        {
            if (reloadPromptText.gameObject.activeSelf) reloadPromptText.gameObject.SetActive(false);
            return;
        }

        if (isReloading)
        {
            reloadPromptText.gameObject.SetActive(true);
            reloadPromptText.text = ArabicStrings.ReloadingNow;
            reloadPromptText.color = new Color(0.4f, 0.92f, 1f, 1f);
        }
        else if (reserveAmmo > 0 && currentInMag <= 3)
        {
            reloadPromptText.gameObject.SetActive(true);
            reloadPromptText.text = ArabicStrings.ReloadPrompt;
            float pulse = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f));
            reloadPromptText.color = currentInMag == 0
                ? new Color(1f, 0.35f, 0.2f, pulse)
                : new Color(1f, 0.82f, 0.2f, pulse);
        }
        else if (reloadPromptText.gameObject.activeSelf)
        {
            reloadPromptText.gameObject.SetActive(false);
        }
    }

    public void UpdateGrenades(int count)
    {
        if (grenadesText != null)
        {
            grenadesText.text = ArabicUI.Format(ArabicStrings.GrenadesTemplate, count);
            if (count == 0) grenadesText.color = Color.red;
            else grenadesText.color = Color.white;
        }
    }

    public void ShowHitMarker(bool isKill = false)
    {
        hitMarkerTimer = isKill ? hitMarkerMaxTime * 1.25f : hitMarkerMaxTime;
        float popScale = isKill ? 1.85f : 1.45f;
        if (hitMarkerRoot != null) hitMarkerRoot.localScale = new Vector3(popScale, popScale, 1f);
        
        // Distinct from red damage taken: white for hit, emerald green for confirmed kill.
        Color markerColor = isKill ? new Color(0.2f, 1f, 0.45f, 1f) : Color.white;
        foreach (var img in hitMarkerLines)
        {
            if (img != null) img.color = markerColor;
        }
    }

    public void SetAiming(bool isAiming)
    {
        if (crosshairImage == null) return;
        float targetAlpha = isAiming ? 0f : 0.8f;
        Color c = crosshairImage.color;
        c.a = Mathf.MoveTowards(c.a, targetAlpha, Time.unscaledDeltaTime * 8f);
        crosshairImage.color = c;
    }

    public void FlashWaveBanner(Color tint)
    {
        if (waveFlashOverlay == null) return;
        waveFlashColor = tint;
        waveFlashOverlay.color = tint;
        waveFlashTimer = 0.5f;
    }

    public void ShowDamageFlash()
    {
        damageFlashTimer = damageFlashMaxTime;
    }

    public void ShowDirectionalDamage(Vector3 sourcePos, Transform pTransform)
    {
        damageDirTimer = damageDirMaxTime;
        damageSourcePos = sourcePos;
        playerTransform = pTransform;
        
        if (damageDirImage != null)
        {
            damageDirImage.color = new Color(1f, 0f, 0f, 0.8f); // Bright red
        }
    }

    void Update()
    {
        if (hudRoot != null)
        {
            bool showHud = !IsStartOverlayActive && (GameManager.instance == null || !GameManager.instance.isGameOver);
            if (hudRoot.activeSelf != showHud) hudRoot.SetActive(showHud);
        }

        // 1. Smoothly update health bar fill
        if (healthBarFill != null)
        {
            currentHealthPct = Mathf.Lerp(currentHealthPct, targetHealthPct, Time.deltaTime * 5f);
            healthBarFill.anchorMax = new Vector2(currentHealthPct, 1);
            
            // Health color goes from Green -> Yellow -> Red
            healthBarImage.color = Color.Lerp(Color.red, Color.green, currentHealthPct);
        }

        // Health Bar Trail (Yellow/White that shrinks slower)
        if (healthBarTrailFill != null)
        {
            if (trailHealthPct > targetHealthPct)
            {
                // Shrink slowly
                trailHealthPct = Mathf.Lerp(trailHealthPct, targetHealthPct, Time.deltaTime * 2f);
            }
            else
            {
                // Snap if health goes up
                trailHealthPct = targetHealthPct;
            }
            healthBarTrailFill.anchorMax = new Vector2(trailHealthPct, 1);
        }

        // 5. Damage Vignette Pulse
        if (damageOverlay != null)
        {
            if (currentHealthPct <= 0.3f && currentHealthPct > 0)
            {
                // Pulse strongly when low health
                float pulse = Mathf.PingPong(Time.time * 3f, 0.3f) + 0.1f;
                damageOverlay.color = new Color(1, 0, 0, pulse);
            }
            else if (damageFlashTimer > 0)
            {
                damageFlashTimer -= Time.deltaTime;
                float alpha = (damageFlashTimer / damageFlashMaxTime) * 0.4f;
                damageOverlay.color = new Color(1, 0, 0, alpha);
            }
            else
            {
                damageOverlay.color = new Color(1, 0, 0, 0);
            }
        }

        // 2. Hit Marker Animational Damage Indicator Logic
        if (damageDirTimer > 0 && playerTransform != null)
        {
            damageDirTimer -= Time.deltaTime;
            
            // Calculate relative angle
            Vector3 dirToDamage = (damageSourcePos - playerTransform.position).normalized;
            dirToDamage.y = 0; // Flat plane
            
            Vector3 playerForward = playerTransform.forward;
            playerForward.y = 0;
            playerForward.Normalize();
            
            float angle = Vector3.SignedAngle(playerForward, dirToDamage, Vector3.up);
            
            // Rotate the root to point to the damage (UI Z axis rotation is negative for clockwise)
            if (damageDirRoot != null)
            {
                damageDirRoot.localRotation = Quaternion.Euler(0, 0, -angle);
            }
            
            // Fade out
            if (damageDirImage != null)
            {
                Color c = damageDirImage.color;
                c.a = Mathf.Lerp(0, 0.8f, damageDirTimer / damageDirMaxTime);
                damageDirImage.color = c;
            }
        }

        // 2. Hit Marker Animation
        if (hitMarkerTimer > 0)
        {
            hitMarkerTimer -= Time.deltaTime;
            float progress = hitMarkerTimer / hitMarkerMaxTime; // 1 to 0
            
            // Shrink back to normal
            float scale = Mathf.Lerp(1f, 1.5f, progress);
            if (hitMarkerRoot != null) hitMarkerRoot.localScale = new Vector3(scale, scale, 1f);
            
            // Fade out
            foreach (var img in hitMarkerLines)
            {
                if (img != null)
                {
                    Color c = img.color;
                    c.a = progress;
                    img.color = c;
                }
            }
        }

        // 3. Damage Overlay Fade
        if (damageFlashTimer > 0)
        {
            damageFlashTimer -= Time.deltaTime;
            if (damageOverlay != null)
            {
                Color c = damageOverlay.color;
                c.a = Mathf.Lerp(0, 0.5f, damageFlashTimer / damageFlashMaxTime);
                damageOverlay.color = c;
            }
        }

        // 4. Hype UI fades (uses unscaled time so it still reads during slow-motion)
        if (killFeedTimer > 0 && killFeedText != null)
        {
            killFeedTimer -= Time.unscaledDeltaTime;
            Color c = killFeedText.color;
            c.a = Mathf.Clamp01(killFeedTimer / 0.8f);
            killFeedText.color = c;
            killFeedText.rectTransform.anchoredPosition = Vector2.Lerp(
                killFeedText.rectTransform.anchoredPosition,
                new Vector2(0, 330), Time.unscaledDeltaTime * 4f);
        }

        if (comboTimer > 0 && comboText != null)
        {
            comboTimer -= Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(comboTimer / 1.6f);
            comboText.color = new Color(1f, 0.75f, 0f, t);
            comboText.rectTransform.localScale = Vector3.one * (1f + (1f - t) * 0.25f);
            if (comboTimer <= 0) comboText.gameObject.SetActive(false);
        }

        FadeToast(buffText, ref buffTimer);
        FadeToast(eventTimerText, ref eventTimerTimer);
        FadeToast(achievementText, ref achievementTimer);

        if (waveFlashTimer > 0f && waveFlashOverlay != null)
        {
            waveFlashTimer -= Time.unscaledDeltaTime;
            Color c = waveFlashColor;
            c.a = waveFlashColor.a * Mathf.Clamp01(waveFlashTimer / 0.5f);
            waveFlashOverlay.color = c;
        }

        if (giftBeaconTimeout > 0f)
        {
            giftBeaconTimeout -= Time.unscaledDeltaTime;
            if (giftBeaconTimeout <= 0f && giftBeaconObj != null) giftBeaconObj.SetActive(false);
        }

        if (dailyGiftButton != null && Time.frameCount % 30 == 0) RefreshDailyGiftButton();
    }

    private void FadeToast(Text text, ref float timer)
    {
        if (text == null || timer <= 0f) return;
        timer -= Time.unscaledDeltaTime;
        Color c = text.color;
        c.a = Mathf.Clamp01(timer / 0.8f);
        text.color = c;
    }

    private void SetupStartOverlay(Transform parent)
    {
        startOverlay = new GameObject("StartOverlay");
        startOverlay.transform.SetParent(parent, false);
        RectTransform rt = startOverlay.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Dedicated high-sorting Canvas so StartOverlay sits cleanly above all HUD & GameManager banners
        Canvas overlayCanvas = startOverlay.AddComponent<Canvas>();
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = 30;
        startOverlay.AddComponent<GraphicRaycaster>();

        Image bg = startOverlay.AddComponent<Image>();
        bg.color = new Color(0.03f, 0.05f, 0.09f, 0.88f);

        // Sleek Centered Glass Card
        GameObject cardObj = new GameObject("StartCenterCard");
        cardObj.transform.SetParent(startOverlay.transform, false);
        Image cardImg = cardObj.AddComponent<Image>();
        UIStyle.StyleCard(cardImg, new Color(0.07f, 0.10f, 0.16f, 0.94f));
        RectTransform cardRt = cardObj.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = new Vector2(0, -6);
        cardRt.sizeDelta = new Vector2(860, 620);

        // Gold top accent bar inside card
        GameObject cardAccent = new GameObject("CardAccent");
        cardAccent.transform.SetParent(cardObj.transform, false);
        Image caImg = cardAccent.AddComponent<Image>();
        caImg.color = new Color(1f, 0.78f, 0.18f, 1f);
        RectTransform caRt = cardAccent.GetComponent<RectTransform>();
        caRt.anchorMin = new Vector2(0.06f, 1f);
        caRt.anchorMax = new Vector2(0.94f, 1f);
        caRt.pivot = new Vector2(0.5f, 1f);
        caRt.offsetMin = new Vector2(0, -4);
        caRt.offsetMax = Vector2.zero;

        // Title
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(cardObj.transform, false);
        Text title = titleObj.AddComponent<Text>();
        UIStyle.ConfigureArabicText(title, 82, new Color(1f, 0.80f, 0.18f, 1f), true);
        title.text = "MYBUPGI";
        RectTransform titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0.5f, 1f);
        titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0, -86);
        titleRt.sizeDelta = new Vector2(760, 110);

        // Subtitle
        GameObject subObj = new GameObject("Subtitle");
        subObj.transform.SetParent(cardObj.transform, false);
        Text sub = subObj.AddComponent<Text>();
        UIStyle.ConfigureArabicText(sub, 34, new Color(0.92f, 0.95f, 1f, 1f), true);
        sub.text = ArabicStrings.StartTagline;
        RectTransform subRt = sub.rectTransform;
        subRt.anchorMin = new Vector2(0.5f, 1f);
        subRt.anchorMax = new Vector2(0.5f, 1f);
        subRt.anchoredPosition = new Vector2(0, -162);
        subRt.sizeDelta = new Vector2(760, 56);

        // Compact Responsive Controls Pill inside card
        GameObject controlsCardObj = new GameObject("ControlsCard");
        controlsCardObj.transform.SetParent(cardObj.transform, false);
        Image controlsCardImg = controlsCardObj.AddComponent<Image>();
        UIStyle.StyleCard(controlsCardImg, new Color(0.04f, 0.06f, 0.11f, 0.90f), false);
        RectTransform ccRt = controlsCardObj.GetComponent<RectTransform>();
        ccRt.anchorMin = new Vector2(0.05f, 1f);
        ccRt.anchorMax = new Vector2(0.95f, 1f);
        ccRt.pivot = new Vector2(0.5f, 0.5f);
        ccRt.anchoredPosition = new Vector2(0, -248);
        ccRt.sizeDelta = new Vector2(0, 74);

        GameObject controlsObj = new GameObject("Controls");
        controlsObj.transform.SetParent(controlsCardObj.transform, false);
        Text controls = controlsObj.AddComponent<Text>();
        UIStyle.ConfigureArabicText(controls, 22, new Color(0.84f, 0.90f, 0.98f, 1f), false, TextAnchor.MiddleCenter, false);
        controls.text = ArabicStrings.ControlsCard;
        controls.horizontalOverflow = HorizontalWrapMode.Wrap;
        controls.resizeTextForBestFit = true;
        controls.resizeTextMinSize = 15;
        controls.resizeTextMaxSize = 22;
        RectTransform controlsRt = controls.rectTransform;
        controlsRt.anchorMin = Vector2.zero;
        controlsRt.anchorMax = Vector2.one;
        controlsRt.offsetMin = new Vector2(18, 8);
        controlsRt.offsetMax = new Vector2(-18, -8);

        // Progress hint: the run resumes at the level after the last cleared one.
        int lastCleared = LevelDirector.LastClearedLevel;
        if (lastCleared > 0)
        {
            GameObject resumeObj = new GameObject("ResumeText");
            resumeObj.transform.SetParent(cardObj.transform, false);
            Text resume = resumeObj.AddComponent<Text>();
            UIStyle.ConfigureArabicText(resume, 22, new Color(1f, 0.80f, 0.22f, 1f), true);
            resume.text = ArabicUI.Format(ArabicStrings.ResumeLevel, lastCleared + 1);
            resume.raycastTarget = false;
            RectTransform resumeRt = resume.rectTransform;
            resumeRt.anchorMin = new Vector2(0.5f, 1f);
            resumeRt.anchorMax = new Vector2(0.5f, 1f);
            resumeRt.anchoredPosition = new Vector2(0, -310);
            resumeRt.sizeDelta = new Vector2(720, 40);
        }

        // Primary Play Button (Sleek Rounded Pill)
        GameObject playObj = new GameObject("PlayButton");
        playObj.transform.SetParent(cardObj.transform, false);
        Image playImg = playObj.AddComponent<Image>();
        Button playBtn = playObj.AddComponent<Button>();
        UIStyle.StyleButton(playBtn, playImg, new Color(0.11f, 0.72f, 0.36f, 1f), true);
        playBtn.onClick.AddListener(() =>
        {
            startOverlay.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Time.timeScale = 1f;
            PlayerHealth ph = FindFirstObjectByType<PlayerHealth>();
            if (ph != null) ph.ResetSpawnProtection();
            GameAudio.PlayWaveStart();
            if (GameManager.instance != null) GameManager.instance.OnPlayerStartedGame();
        });
        RectTransform playRt = playObj.GetComponent<RectTransform>();
        playRt.anchorMin = new Vector2(0.5f, 0f);
        playRt.anchorMax = new Vector2(0.5f, 0f);
        playRt.anchoredPosition = new Vector2(0, 212);
        playRt.sizeDelta = new Vector2(400, 78);

        GameObject playTextObj = new GameObject("Text");
        playTextObj.transform.SetParent(playObj.transform, false);
        Text playText = playTextObj.AddComponent<Text>();
        UIStyle.ConfigureArabicText(playText, 36, Color.white, true);
        playText.text = ArabicStrings.PlayButton;
        RectTransform ptrt = playText.rectTransform;
        ptrt.anchorMin = Vector2.zero;
        ptrt.anchorMax = Vector2.one;
        ptrt.offsetMin = Vector2.zero;
        ptrt.offsetMax = Vector2.zero;

        // Daily Gift Button (Sleek Rounded Pill)
        GameObject dailyObj = new GameObject("DailyGiftButton");
        dailyObj.transform.SetParent(cardObj.transform, false);
        Image dailyImg = dailyObj.AddComponent<Image>();
        Button dailyBtn = dailyObj.AddComponent<Button>();
        UIStyle.StyleButton(dailyBtn, dailyImg, new Color(0.90f, 0.48f, 0.12f, 1f), true);
        RectTransform dailyRt = dailyObj.GetComponent<RectTransform>();
        dailyRt.anchorMin = new Vector2(0.5f, 0f);
        dailyRt.anchorMax = new Vector2(0.5f, 0f);
        dailyRt.anchoredPosition = new Vector2(0, 122);
        dailyRt.sizeDelta = new Vector2(400, 64);

        GameObject dailyTextObj = new GameObject("Text");
        dailyTextObj.transform.SetParent(dailyObj.transform, false);
        Text dailyLabel = dailyTextObj.AddComponent<Text>();
        UIStyle.ConfigureArabicText(dailyLabel, 26, Color.white, true);
        dailyLabel.text = ArabicStrings.DailyButton;
        RectTransform dtrt = dailyLabel.rectTransform;
        dtrt.anchorMin = Vector2.zero;
        dtrt.anchorMax = Vector2.one;
        dtrt.offsetMin = Vector2.zero;
        dtrt.offsetMax = Vector2.zero;

        dailyGiftButton = dailyBtn;
        dailyGiftLabel = dailyLabel;

        // Reward value preview under Daily Gift button
        GameObject previewObj = new GameObject("DailyGiftPreview");
        previewObj.transform.SetParent(cardObj.transform, false);
        dailyGiftPreviewText = previewObj.AddComponent<Text>();
        UIStyle.ConfigureArabicText(dailyGiftPreviewText, 21, new Color(1f, 0.85f, 0.35f, 1f), true);
        dailyGiftPreviewText.raycastTarget = false;
        RectTransform pvRt = dailyGiftPreviewText.rectTransform;
        pvRt.anchorMin = new Vector2(0.5f, 0f);
        pvRt.anchorMax = new Vector2(0.5f, 0f);
        pvRt.anchoredPosition = new Vector2(0, 56);
        pvRt.sizeDelta = new Vector2(760, 42);

        RefreshDailyGiftButton();

        dailyBtn.onClick.AddListener(() =>
        {
            if (GameFeatures.instance == null) return;
            GameFeatures.instance.ClaimDailyGift();
            GameAudio.PlayWaveComplete();
            RefreshDailyGiftButton();
        });

        // Fullscreen toggle pill button (top-right of Start Overlay)
        GameObject fsObj = new GameObject("FullscreenButton");
        fsObj.transform.SetParent(startOverlay.transform, false);
        Image fsImg = fsObj.AddComponent<Image>();
        Button fsBtn = fsObj.AddComponent<Button>();
        UIStyle.StyleButton(fsBtn, fsImg, new Color(0.14f, 0.20f, 0.30f, 0.95f), true);
        fsBtn.onClick.AddListener(() => { Screen.fullScreen = !Screen.fullScreen; });
        RectTransform fsRt = fsObj.GetComponent<RectTransform>();
        fsRt.anchorMin = new Vector2(1f, 1f);
        fsRt.anchorMax = new Vector2(1f, 1f);
        fsRt.pivot = new Vector2(1f, 1f);
        fsRt.anchoredPosition = new Vector2(-28, -24);
        fsRt.sizeDelta = new Vector2(196, 48);

        GameObject fsTextObj = new GameObject("Text");
        fsTextObj.transform.SetParent(fsObj.transform, false);
        Text fsText = fsTextObj.AddComponent<Text>();
        UIStyle.ConfigureArabicText(fsText, 20, Color.white, true);
        fsText.text = ArabicStrings.FullscreenButton;
        RectTransform ftrt = fsText.rectTransform;
        ftrt.anchorMin = Vector2.zero;
        ftrt.anchorMax = Vector2.one;
        ftrt.offsetMin = Vector2.zero;
        ftrt.offsetMax = Vector2.zero;

        // Pause world until the player clicks "Play" so they enter action cleanly.
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RefreshDailyGiftButton()
    {
        if (dailyGiftButton == null || dailyGiftLabel == null || GameFeatures.instance == null) return;
        bool claimed = GameFeatures.instance.IsDailyClaimed();
        dailyGiftLabel.text = claimed ? ArabicStrings.DailyClaimedButton : ArabicStrings.DailyButton;
        dailyGiftButton.interactable = !claimed;
        if (dailyGiftPreviewText != null)
        {
            dailyGiftPreviewText.text = claimed ? ArabicStrings.DailyClaimedSummary : ArabicStrings.DailyRewardPreview;
            dailyGiftPreviewText.color = claimed ? new Color(0.35f, 0.95f, 0.55f, 1f) : new Color(1f, 0.85f, 0.35f, 1f);
        }
    }

    private void SetupHypeUI(Transform parent)
    {
        GameObject feedObj = new GameObject("KillFeedText");
        feedObj.transform.SetParent(parent, false);
        killFeedText = feedObj.AddComponent<Text>();
        ArabicUI.Apply(killFeedText, true);
        killFeedText.fontSize = 52;
        killFeedText.color = new Color(1f, 0.9f, 0.2f, 0f);
        killFeedText.alignment = TextAnchor.MiddleCenter;
        killFeedText.raycastTarget = false;
        RectTransform feedRt = killFeedText.rectTransform;
        feedRt.anchorMin = new Vector2(0.5f, 0.5f);
        feedRt.anchorMax = new Vector2(0.5f, 0.5f);
        feedRt.anchoredPosition = new Vector2(0, 280);
        feedRt.sizeDelta = new Vector2(1200, 100);

        GameObject comboObj = new GameObject("ComboText");
        comboObj.transform.SetParent(parent, false);
        comboText = comboObj.AddComponent<Text>();
        ArabicUI.Apply(comboText, true);
        comboText.fontSize = 64;
        comboText.color = new Color(1f, 0.75f, 0f, 0f);
        comboText.alignment = TextAnchor.MiddleRight;
        comboText.raycastTarget = false;
        RectTransform comboRt = comboText.rectTransform;
        comboRt.anchorMin = new Vector2(0.5f, 0.5f);
        comboRt.anchorMax = new Vector2(0.5f, 0.5f);
        comboRt.anchoredPosition = new Vector2(520, -120);
        comboRt.sizeDelta = new Vector2(420, 120);
        comboObj.SetActive(false);

        // Buff / event / achievement toasts (above the kill feed, below the announcement).
        buffText = CreateToastText(parent, "BuffText", new Vector2(0, 430), new Vector2(1200, 60), 34, new Color(0.4f, 0.95f, 1f));
        eventTimerText = CreateToastText(parent, "EventTimerText", new Vector2(0, 385), new Vector2(1200, 60), 32, new Color(1f, 0.5f, 0.9f));
        achievementText = CreateToastText(parent, "AchievementText", new Vector2(0, 340), new Vector2(1300, 70), 36, new Color(1f, 0.85f, 0.2f));
    }

    private Text CreateToastText(Transform parent, string name, Vector2 pos, Vector2 size, int fontSize, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        Text text = obj.AddComponent<Text>();
        ArabicUI.Apply(text, true);
        text.fontSize = fontSize;
        text.color = new Color(color.r, color.g, color.b, 0f);
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        RectTransform rt = text.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return text;
    }

    public void ShowKillMessage(string message)
    {
        if (killFeedText == null) return;
        killFeedText.text = message;
        killFeedText.color = new Color(1f, 0.9f, 0.2f, 1f);
        killFeedText.rectTransform.anchoredPosition = new Vector2(0, 360);
        killFeedTimer = 0.8f;
    }

    public void ShowCombo(int multiplier)
    {
        if (comboText == null) return;
        comboText.gameObject.SetActive(true);
        comboText.text = ArabicUI.Format(ArabicStrings.ComboTemplate, multiplier);
        comboText.rectTransform.localScale = Vector3.one * 1.35f;
        comboText.color = new Color(1f, 0.75f, 0f, 1f);
        comboTimer = 1.6f;
    }

    public void ShowBuff(string label, float seconds)
    {
        if (buffText == null) return;
        buffText.text = label;
        Color c = buffText.color;
        c.a = 1f;
        buffText.color = c;
        buffTimer = seconds;
    }

    public void ShowEventTimer(string label, float seconds)
    {
        if (eventTimerText == null) return;
        eventTimerText.text = label;
        Color c = eventTimerText.color;
        c.a = 1f;
        eventTimerText.color = c;
        eventTimerTimer = seconds;
    }

    public void ShowAchievement(string label)
    {
        if (achievementText == null) return;
        achievementText.text = ArabicUI.Format(ArabicStrings.AchievementToast, label);
        Color c = achievementText.color;
        c.a = 1f;
        achievementText.color = c;
        achievementTimer = 3.2f;
    }

    private void SetupMobileControls(Transform parent)
    {
        // ----------------- Left Side: Joystick -----------------
        GameObject joyBgObj = new GameObject("MobileJoystick");
        joyBgObj.transform.SetParent(parent, false);
        RectTransform joyBgRt = joyBgObj.AddComponent<RectTransform>();
        joyBgRt.anchorMin = new Vector2(0, 0); // Bottom Left
        joyBgRt.anchorMax = new Vector2(0, 0);
        joyBgRt.anchoredPosition = new Vector2(350, 300); // Moved up/right slightly to clear HP
        joyBgRt.sizeDelta = new Vector2(350, 350);
        
        Image joyBgImg = joyBgObj.AddComponent<Image>();
        joyBgImg.color = new Color(0, 0, 0, 0.2f);
        
        GameObject joyHandleObj = new GameObject("Handle");
        joyHandleObj.transform.SetParent(joyBgObj.transform, false);
        RectTransform joyHandleRt = joyHandleObj.AddComponent<RectTransform>();
        joyHandleRt.anchorMin = new Vector2(0.5f, 0.5f);
        joyHandleRt.anchorMax = new Vector2(0.5f, 0.5f);
        joyHandleRt.sizeDelta = new Vector2(150, 150);
        
        Image joyHandleImg = joyHandleObj.AddComponent<Image>();
        joyHandleImg.color = new Color(1, 1, 1, 0.5f);
        
        joyBgObj.AddComponent<MobileJoystick>();

        // ----------------- Right Side: Action Buttons -----------------
        // Now that Ammo is top-right, we have the whole bottom-right corner.

        // Vivid colors for the new 3D buttons
        Color fireColor = new Color(0.9f, 0.15f, 0.15f, 0.85f);
        Color jumpColor = new Color(0.15f, 0.6f, 1f, 0.85f);
        Color reloadColor = new Color(1f, 0.6f, 0f, 0.85f);
        Color bombColor = new Color(0.3f, 0.3f, 0.3f, 0.85f);
        Color swapColor = new Color(0.2f, 0.8f, 0.2f, 0.85f);

        // Fire Button (Large, main thumb resting position)
        GameObject fireBtn = CreateMobileButton(parent, "FireBtn", new Vector2(-200, 200), 180, fireColor, ArabicStrings.MobileFire);
        EventTrigger fireTrig = fireBtn.GetComponent<EventTrigger>();
        if (fireTrig == null) fireTrig = fireBtn.AddComponent<EventTrigger>();
        EventTrigger.Entry downEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        downEntry.callback.AddListener((data) => { MobileIsFiring = true; });
        fireTrig.triggers.Add(downEntry);
        EventTrigger.Entry upEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        upEntry.callback.AddListener((data) => { MobileIsFiring = false; });
        fireTrig.triggers.Add(upEntry);

        // Jump Button (Directly above Fire)
        GameObject jumpBtn = CreateMobileButton(parent, "JumpBtn", new Vector2(-200, 420), 120, jumpColor, ArabicStrings.MobileJump);
        EventTrigger jumpTrig = jumpBtn.GetComponent<EventTrigger>();
        EventTrigger.Entry jDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        jDown.callback.AddListener((data) => { MobileJumpPressed = true; });
        jumpTrig.triggers.Add(jDown);

        // Reload Button (Left of Fire)
        GameObject reloadBtn = CreateMobileButton(parent, "ReloadBtn", new Vector2(-420, 200), 120, reloadColor, ArabicStrings.MobileReload);
        EventTrigger reloadTrig = reloadBtn.GetComponent<EventTrigger>();
        EventTrigger.Entry rDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        rDown.callback.AddListener((data) => { MobileReloadPressed = true; });
        reloadTrig.triggers.Add(rDown);

        // Grenade Button (Above Reload)
        GameObject grenadeBtn = CreateMobileButton(parent, "GrenadeBtn", new Vector2(-420, 360), 120, bombColor, ArabicStrings.MobileGrenade);
        EventTrigger bombTrig = grenadeBtn.GetComponent<EventTrigger>();
        EventTrigger.Entry bDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        bDown.callback.AddListener((data) => { MobileGrenadePressed = true; });
        bombTrig.triggers.Add(bDown);

        // Swap Weapon Button (Above Jump)
        GameObject swapBtn = CreateMobileButton(parent, "SwapBtn", new Vector2(-200, 580), 120, swapColor, ArabicStrings.MobileSwap);
        EventTrigger swapTrig = swapBtn.GetComponent<EventTrigger>();
        EventTrigger.Entry sDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        sDown.callback.AddListener((data) => { MobileSwitchWeaponPressed = true; });
        swapTrig.triggers.Add(sDown);
    }

    private GameObject CreateMobileButton(Transform parent, string name, Vector2 anchoredPos, float size, Color color, string text)
    {
        // Root object (handles raycasts)
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 0); // Bottom Right
        rt.anchorMax = new Vector2(1, 0);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(size, size);
        
        // Base dark shadow/lip (simulates 3D depth)
        GameObject lipObj = new GameObject("Lip");
        lipObj.transform.SetParent(btnObj.transform, false);
        RectTransform lipRt = lipObj.AddComponent<RectTransform>();
        lipRt.anchorMin = Vector2.zero; lipRt.anchorMax = Vector2.one;
        lipRt.offsetMin = new Vector2(0, -12); // Extends down
        lipRt.offsetMax = Vector2.zero;
        Image lipImg = lipObj.AddComponent<Image>();
        lipImg.color = new Color(color.r * 0.4f, color.g * 0.4f, color.b * 0.4f, 1f); // Darker shade
        
        // Main button body
        GameObject topObj = new GameObject("Top");
        topObj.transform.SetParent(btnObj.transform, false);
        RectTransform topRt = topObj.AddComponent<RectTransform>();
        topRt.anchorMin = Vector2.zero; topRt.anchorMax = Vector2.one;
        topRt.offsetMin = Vector2.zero; topRt.offsetMax = Vector2.zero;
        Image topImg = topObj.AddComponent<Image>();
        topImg.color = color;
        
        // Add glassy highlight (Top edge)
        GameObject highlightObj = new GameObject("Highlight");
        highlightObj.transform.SetParent(topObj.transform, false);
        RectTransform highRt = highlightObj.AddComponent<RectTransform>();
        highRt.anchorMin = new Vector2(0, 1); highRt.anchorMax = new Vector2(1, 1);
        highRt.pivot = new Vector2(0.5f, 1f);
        highRt.anchoredPosition = Vector2.zero;
        highRt.sizeDelta = new Vector2(0, size * 0.15f); // 15% of height
        Image highImg = highlightObj.AddComponent<Image>();
        highImg.color = new Color(1, 1, 1, 0.3f);

        // Text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(topObj.transform, false);
        Text t = textObj.AddComponent<Text>();
        ArabicUI.Apply(t, true);
        t.text = text;
        t.fontSize = (int)(size * 0.28f);
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        
        // Text Shadow
        Shadow txtShadow = textObj.AddComponent<Shadow>();
        txtShadow.effectColor = new Color(0, 0, 0, 0.8f);
        txtShadow.effectDistance = new Vector2(2, -2);
        
        RectTransform trt = textObj.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.sizeDelta = Vector2.zero;

        // Interaction (Push down effect using EventTrigger)
        EventTrigger trigger = btnObj.AddComponent<EventTrigger>();
        
        EventTrigger.Entry down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        down.callback.AddListener((data) => { 
            topRt.offsetMin = new Vector2(0, -8); 
            topRt.offsetMax = new Vector2(0, -8); 
            topImg.color = new Color(Mathf.Clamp01(color.r * 1.3f), Mathf.Clamp01(color.g * 1.3f), Mathf.Clamp01(color.b * 1.3f), color.a);
        });
        trigger.triggers.Add(down);
        
        EventTrigger.Entry up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        up.callback.AddListener((data) => { 
            topRt.offsetMin = Vector2.zero; 
            topRt.offsetMax = Vector2.zero; 
            topImg.color = color;
        });
        trigger.triggers.Add(up);

        // Invisible Raycast Target on root
        Image rootImg = btnObj.AddComponent<Image>();
        rootImg.color = new Color(0,0,0,0);
        rootImg.raycastTarget = true;

        return btnObj;
    }
}
