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
    private Text healthText;
    private Text ammoText;
    private Text grenadesText;
    private RectTransform healthBarFill;
    private Image healthBarImage;
    
    // New UI Elements
    private Image damageOverlay;
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

        // 1.2 Start overlay + hype UI
        SetupStartOverlay(canvasObj.transform);
        SetupHypeUI(canvasObj.transform);

        // 1.5 Damage Vignette
        GameObject vignetteObj = new GameObject("DamageVignette");
        vignetteObj.transform.SetParent(canvasObj.transform, false);
        RectTransform vRt = vignetteObj.AddComponent<RectTransform>();
        vRt.anchorMin = Vector2.zero;
        vRt.anchorMax = Vector2.one;
        vRt.sizeDelta = Vector2.zero;
        damageOverlay = vignetteObj.AddComponent<Image>();
        damageOverlay.color = new Color(1, 0, 0, 0); // Transparent red
        damageOverlay.raycastTarget = false;

        // 1.6 Mobile Touch Controls (Only active on Mobile)
#if UNITY_ANDROID || UNITY_IOS
        SetupMobileControls(canvasObj.transform);
#endif

        // 3. Crosshair (Center Dot)
        GameObject crosshairObj = new GameObject("Crosshair");
        crosshairObj.transform.SetParent(canvasObj.transform, false);
        Image crosshairImg = crosshairObj.AddComponent<Image>();
        crosshairImg.color = new Color(1, 1, 1, 0.8f);
        RectTransform crt = crosshairObj.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(6, 6); // Small dot
        Outline cOut = crosshairObj.AddComponent<Outline>();
        cOut.effectColor = Color.black;
        cOut.effectDistance = new Vector2(1, -1);

        // 4. Modern Hit Marker
        GameObject hitRootObj = new GameObject("HitMarkerRoot");
        hitRootObj.transform.SetParent(canvasObj.transform, false);
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

        // 4.5 Directional Damage Indicator
        GameObject dDirRootObj = new GameObject("DamageDirectionRoot");
        dDirRootObj.transform.SetParent(canvasObj.transform, false);
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

        // 5. Health UI
        // Background
        GameObject hpBgObj = new GameObject("HealthPanelBG");
        hpBgObj.transform.SetParent(canvasObj.transform, false);
        Image hpBgImg = hpBgObj.AddComponent<Image>();
        hpBgImg.color = new Color(0, 0, 0, 0.6f);
        RectTransform hBgRt = hpBgObj.GetComponent<RectTransform>();
        hBgRt.anchorMin = new Vector2(0, 0);
        hBgRt.anchorMax = new Vector2(0, 0);
        hBgRt.pivot = new Vector2(0, 0);
        hBgRt.anchoredPosition = new Vector2(50, 50);
        hBgRt.sizeDelta = new Vector2(350, 100);

        // Text
        GameObject healthObj = new GameObject("HealthText");
        healthObj.transform.SetParent(hpBgObj.transform, false);
        healthText = healthObj.AddComponent<Text>();
        ArabicUI.Apply(healthText, true);
        healthText.fontStyle = FontStyle.Bold;
        healthText.fontSize = 45;
        healthText.color = Color.white;
        healthText.alignment = TextAnchor.MiddleCenter;
        RectTransform hrt = healthText.GetComponent<RectTransform>();
        hrt.anchorMin = Vector2.zero;
        hrt.anchorMax = Vector2.one;
        hrt.offsetMin = new Vector2(0, 20); // Make room for bar
        hrt.offsetMax = Vector2.zero;
        Outline hOutline = healthObj.AddComponent<Outline>();
        hOutline.effectColor = new Color(0, 0, 0, 0.8f);

        // Health Bar Track
        GameObject trackObj = new GameObject("HealthBarTrack");
        trackObj.transform.SetParent(hpBgObj.transform, false);
        Image trackImg = trackObj.AddComponent<Image>();
        trackImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        RectTransform tRt = trackObj.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0, 0);
        tRt.anchorMax = new Vector2(1, 0);
        tRt.pivot = new Vector2(0.5f, 0);
        tRt.anchoredPosition = new Vector2(0, 10);
        tRt.sizeDelta = new Vector2(-20, 15); // Padding
        
        // Health Bar Trail (Yellow/White that shrinks slower)
        GameObject trailObj = new GameObject("HealthBarTrail");
        trailObj.transform.SetParent(trackObj.transform, false);
        Image trailImg = trailObj.AddComponent<Image>();
        trailImg.color = new Color(1f, 0.9f, 0.2f, 1f); // Yellow trail
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
        healthBarImage.color = new Color(0.2f, 0.8f, 0.2f, 1f); // Nice green
        healthBarFill = fillObj.GetComponent<RectTransform>();
        healthBarFill.anchorMin = new Vector2(0, 0);
        healthBarFill.anchorMax = new Vector2(1, 1);
        healthBarFill.pivot = new Vector2(0, 0.5f);
        healthBarFill.offsetMin = Vector2.zero;
        healthBarFill.offsetMax = Vector2.zero;

        // 6. Ammo UI
        GameObject ammoBgObj = new GameObject("AmmoPanelBG");
        ammoBgObj.transform.SetParent(canvasObj.transform, false);
        Image ammoBgImg = ammoBgObj.AddComponent<Image>();
        ammoBgImg.color = new Color(0, 0, 0, 0.6f);
        RectTransform aBgRt = ammoBgObj.GetComponent<RectTransform>();
        aBgRt.anchorMin = new Vector2(1, 1); // Top Right
        aBgRt.anchorMax = new Vector2(1, 1);
        aBgRt.pivot = new Vector2(1, 1);
        aBgRt.anchoredPosition = new Vector2(-50, -50);
        aBgRt.sizeDelta = new Vector2(250, 80);

        GameObject ammoObj = new GameObject("AmmoText");
        ammoObj.transform.SetParent(ammoBgObj.transform, false);
        ammoText = ammoObj.AddComponent<Text>();
        ArabicUI.Apply(ammoText, true);
        ammoText.fontStyle = FontStyle.Bold;
        ammoText.fontSize = 45;
        ammoText.color = Color.white;
        ammoText.alignment = TextAnchor.MiddleCenter;
        RectTransform art = ammoText.GetComponent<RectTransform>();
        art.anchorMin = Vector2.zero;
        art.anchorMax = Vector2.one;
        art.offsetMin = Vector2.zero;
        art.offsetMax = Vector2.zero;
        Outline aOutline = ammoObj.AddComponent<Outline>();
        aOutline.effectColor = new Color(0, 0, 0, 0.8f);

        // 7. Grenades UI
        GameObject grenadeBgObj = new GameObject("GrenadePanelBG");
        grenadeBgObj.transform.SetParent(canvasObj.transform, false);
        Image grenadeBgImg = grenadeBgObj.AddComponent<Image>();
        grenadeBgImg.color = new Color(0, 0, 0, 0.6f);
        RectTransform gBgRt = grenadeBgObj.GetComponent<RectTransform>();
        gBgRt.anchorMin = new Vector2(1, 1); // Top Right
        gBgRt.anchorMax = new Vector2(1, 1);
        gBgRt.pivot = new Vector2(1, 1);
        gBgRt.anchoredPosition = new Vector2(-50, -140); // Below ammo
        gBgRt.sizeDelta = new Vector2(150, 60);

        GameObject grenadeObj = new GameObject("GrenadeText");
        grenadeObj.transform.SetParent(grenadeBgObj.transform, false);
        grenadesText = grenadeObj.AddComponent<Text>();
        ArabicUI.Apply(grenadesText, true);
        grenadesText.fontStyle = FontStyle.Bold;
        grenadesText.fontSize = 35;
        grenadesText.color = Color.white;
        grenadesText.alignment = TextAnchor.MiddleCenter;
        RectTransform grt = grenadesText.GetComponent<RectTransform>();
        grt.anchorMin = Vector2.zero;
        grt.anchorMax = Vector2.one;
        grt.offsetMin = Vector2.zero;
        grt.offsetMax = Vector2.zero;
        Outline gOutline = grenadeObj.AddComponent<Outline>();
        gOutline.effectColor = new Color(0, 0, 0, 0.8f);

        UpdateHealth(100);
        UpdateAmmo(30, 90);
        UpdateGrenades(3);
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
            else ammoText.color = Color.white;
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

    public void ShowHitMarker()
    {
        hitMarkerTimer = hitMarkerMaxTime;
        hitMarkerRoot.localScale = new Vector3(1.5f, 1.5f, 1f); // Pop up size
        
        foreach (var img in hitMarkerLines)
        {
            if (img != null) img.color = Color.white; // Start white, fade out
        }
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

        Image bg = startOverlay.AddComponent<Image>();
        bg.color = new Color(0.03f, 0.04f, 0.06f, 0.94f);

        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(startOverlay.transform, false);
        Text title = titleObj.AddComponent<Text>();
        title.font = ArabicUI.Get(true);
        title.text = "MYBUPGI";
        title.fontSize = 110;
        title.fontStyle = FontStyle.Bold;
        title.color = new Color(1f, 0.78f, 0.15f, 1f);
        title.alignment = TextAnchor.MiddleCenter;
        RectTransform titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax = new Vector2(0.5f, 0.5f);
        titleRt.anchoredPosition = new Vector2(0, 210);
        titleRt.sizeDelta = new Vector2(1400, 180);

        GameObject subObj = new GameObject("Subtitle");
        subObj.transform.SetParent(startOverlay.transform, false);
        Text sub = subObj.AddComponent<Text>();
        ArabicUI.Apply(sub, false);
        sub.text = ArabicStrings.StartSubtitle;
        sub.fontSize = 56;
        sub.color = Color.white;
        sub.alignment = TextAnchor.MiddleCenter;
        RectTransform subRt = sub.rectTransform;
        subRt.anchorMin = new Vector2(0.5f, 0.5f);
        subRt.anchorMax = new Vector2(0.5f, 0.5f);
        subRt.anchoredPosition = new Vector2(0, 70);
        subRt.sizeDelta = new Vector2(1500, 100);

        GameObject controlsObj = new GameObject("Controls");
        controlsObj.transform.SetParent(startOverlay.transform, false);
        Text controls = controlsObj.AddComponent<Text>();
        ArabicUI.Apply(controls, false);
        controls.text = ArabicStrings.Controls;
        controls.fontSize = 34;
        controls.color = new Color(0.85f, 0.88f, 0.95f, 1f);
        controls.alignment = TextAnchor.MiddleCenter;
        RectTransform controlsRt = controls.rectTransform;
        controlsRt.anchorMin = new Vector2(0.5f, 0.5f);
        controlsRt.anchorMax = new Vector2(0.5f, 0.5f);
        controlsRt.anchoredPosition = new Vector2(0, -60);
        controlsRt.sizeDelta = new Vector2(1600, 120);

        GameObject playObj = new GameObject("PlayButton");
        playObj.transform.SetParent(startOverlay.transform, false);
        Image playImg = playObj.AddComponent<Image>();
        playImg.color = new Color(1f, 0.6f, 0f, 1f);
        Button playBtn = playObj.AddComponent<Button>();
        playBtn.onClick.AddListener(() =>
        {
            startOverlay.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Time.timeScale = 1f;
        });
        RectTransform playRt = playObj.GetComponent<RectTransform>();
        playRt.anchorMin = new Vector2(0.5f, 0.5f);
        playRt.anchorMax = new Vector2(0.5f, 0.5f);
        playRt.anchoredPosition = new Vector2(0, -210);
        playRt.sizeDelta = new Vector2(420, 100);

        GameObject playTextObj = new GameObject("Text");
        playTextObj.transform.SetParent(playObj.transform, false);
        Text playText = playTextObj.AddComponent<Text>();
        ArabicUI.Apply(playText, true);
        playText.text = ArabicStrings.StartSubtitle;
        playText.fontSize = 46;
        playText.color = Color.white;
        playText.alignment = TextAnchor.MiddleCenter;
        RectTransform ptrt = playText.rectTransform;
        ptrt.anchorMin = Vector2.zero;
        ptrt.anchorMax = Vector2.one;
        ptrt.offsetMin = Vector2.zero;
        ptrt.offsetMax = Vector2.zero;
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
