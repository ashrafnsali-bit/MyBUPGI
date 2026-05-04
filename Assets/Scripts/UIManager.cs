using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class UIManager : MonoBehaviour
{
    public static UIManager instance;

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
    
    private float hitMarkerTimer = 0f;
    private float hitMarkerMaxTime = 0.3f;
    private float damageFlashTimer = 0f;
    private float damageFlashMaxTime = 0.5f;
    
    private float targetHealthPct = 1f;
    private float currentHealthPct = 1f;

    void Awake()
    {
        if (instance == null)
            instance = this;
        else if (instance != this)
            Destroy(gameObject);
            
        SetupUI();
    }

    void SetupUI()
    {
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

        // 2. Damage Overlay (Blood Screen)
        GameObject dmgObj = new GameObject("DamageOverlay");
        dmgObj.transform.SetParent(canvasObj.transform, false);
        damageOverlay = dmgObj.AddComponent<Image>();
        damageOverlay.color = new Color(0.8f, 0, 0, 0); // Transparent dark red
        RectTransform dmgRt = dmgObj.GetComponent<RectTransform>();
        dmgRt.anchorMin = Vector2.zero;
        dmgRt.anchorMax = Vector2.one;
        dmgRt.offsetMin = Vector2.zero;
        dmgRt.offsetMax = Vector2.zero;
        
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
        healthText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
        aBgRt.anchorMin = new Vector2(1, 0);
        aBgRt.anchorMax = new Vector2(1, 0);
        aBgRt.pivot = new Vector2(1, 0);
        aBgRt.anchoredPosition = new Vector2(-50, 50);
        aBgRt.sizeDelta = new Vector2(250, 80);

        GameObject ammoObj = new GameObject("AmmoText");
        ammoObj.transform.SetParent(ammoBgObj.transform, false);
        ammoText = ammoObj.AddComponent<Text>();
        ammoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
        gBgRt.anchorMin = new Vector2(1, 0);
        gBgRt.anchorMax = new Vector2(1, 0);
        gBgRt.pivot = new Vector2(1, 0);
        gBgRt.anchoredPosition = new Vector2(-50, 140); // Above ammo
        gBgRt.sizeDelta = new Vector2(150, 60);

        GameObject grenadeObj = new GameObject("GrenadeText");
        grenadeObj.transform.SetParent(grenadeBgObj.transform, false);
        grenadesText = grenadeObj.AddComponent<Text>();
        grenadesText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
            healthText.text = "HP: " + health.ToString();
        }

        if (healthBarFill != null)
        {
            targetHealthPct = Mathf.Clamp01((float)health / maxHealth);
            // Change color immediately based on target
            if (targetHealthPct <= 0.3f) healthBarImage.color = new Color(0.8f, 0.1f, 0.1f, 1f); // Red
            else if (targetHealthPct <= 0.6f) healthBarImage.color = new Color(0.8f, 0.8f, 0.1f, 1f); // Yellow
            else healthBarImage.color = new Color(0.2f, 0.8f, 0.2f, 1f); // Green
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
            grenadesText.text = "G: " + count.ToString();
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
        if (damageOverlay != null)
        {
            damageOverlay.color = new Color(0.8f, 0f, 0f, 0.5f); // Flash intensity
        }
    }

    void Update()
    {
        // 1. Smooth Health Bar Interpolation
        if (healthBarFill != null)
        {
            currentHealthPct = Mathf.Lerp(currentHealthPct, targetHealthPct, Time.deltaTime * 5f);
            healthBarFill.anchorMax = new Vector2(currentHealthPct, 1);
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
    }
}
