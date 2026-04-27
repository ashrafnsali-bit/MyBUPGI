using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class UIManager : MonoBehaviour
{
    public static UIManager instance;

    private Canvas canvas;
    private Text healthText;
    private Text ammoText;
    private Image hitMarker;
    private RectTransform healthBarFill;
    private float hitMarkerTimer = 0f;

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
        // Check if we already have a canvas for UI
        GameObject canvasObj = new GameObject("ModernCanvasUI");
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasObj.AddComponent<GraphicRaycaster>();

        // Health UI
        GameObject healthObj = new GameObject("HealthText");
        healthObj.transform.SetParent(canvasObj.transform, false);
        healthText = healthObj.AddComponent<Text>();
        healthText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        healthText.fontSize = 40;
        healthText.color = Color.green;
        healthText.alignment = TextAnchor.LowerLeft;
        
        RectTransform hrt = healthText.GetComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0, 0);
        hrt.anchorMax = new Vector2(0, 0);
        hrt.pivot = new Vector2(0, 0);
        hrt.anchoredPosition = new Vector2(50, 50);
        hrt.sizeDelta = new Vector2(300, 100);
        
        Outline hOutline = healthObj.AddComponent<Outline>();
        hOutline.effectColor = Color.black;

        // Health Bar Background
        GameObject bgObj = new GameObject("HealthBarBG");
        bgObj.transform.SetParent(canvasObj.transform, false);
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0, 0, 0, 0.5f);
        RectTransform bgRt = bgImg.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0, 0);
        bgRt.anchorMax = new Vector2(0, 0);
        bgRt.pivot = new Vector2(0, 0);
        bgRt.anchoredPosition = new Vector2(50, 30);
        bgRt.sizeDelta = new Vector2(300, 15);

        // Health Bar Fill
        GameObject fillObj = new GameObject("HealthBarFill");
        fillObj.transform.SetParent(bgObj.transform, false);
        Image fillImg = fillObj.AddComponent<Image>();
        fillImg.color = Color.green;
        healthBarFill = fillImg.GetComponent<RectTransform>();
        healthBarFill.anchorMin = new Vector2(0, 0);
        healthBarFill.anchorMax = new Vector2(1, 1);
        healthBarFill.pivot = new Vector2(0, 0.5f);
        healthBarFill.offsetMin = Vector2.zero;
        healthBarFill.offsetMax = Vector2.zero;

        // Ammo UI
        GameObject ammoObj = new GameObject("AmmoText");
        ammoObj.transform.SetParent(canvasObj.transform, false);
        ammoText = ammoObj.AddComponent<Text>();
        ammoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        ammoText.fontSize = 40;
        ammoText.color = Color.white;
        ammoText.alignment = TextAnchor.LowerRight;

        RectTransform art = ammoText.GetComponent<RectTransform>();
        art.anchorMin = new Vector2(1, 0);
        art.anchorMax = new Vector2(1, 0);
        art.pivot = new Vector2(1, 0);
        art.anchoredPosition = new Vector2(-50, 50);
        art.sizeDelta = new Vector2(300, 100);
        
        Outline aOutline = ammoObj.AddComponent<Outline>();
        aOutline.effectColor = Color.black;

        // Hit Marker
        GameObject hitObj = new GameObject("HitMarker");
        hitObj.transform.SetParent(canvasObj.transform, false);
        hitMarker = hitObj.AddComponent<Image>();
        hitMarker.color = new Color(1, 1, 1, 0); // Transparent initially
        
        RectTransform mrt = hitMarker.GetComponent<RectTransform>();
        mrt.anchorMin = new Vector2(0.5f, 0.5f);
        mrt.anchorMax = new Vector2(0.5f, 0.5f);
        mrt.pivot = new Vector2(0.5f, 0.5f);
        mrt.anchoredPosition = Vector2.zero;
        mrt.sizeDelta = new Vector2(30, 30);
        
        // We will just use a square rotated by 45 degrees as a simple hit marker if no sprite is assigned
        mrt.localRotation = Quaternion.Euler(0, 0, 45);
        
        UpdateHealth(100);
        UpdateAmmo(30, 90);
    }

    public void UpdateHealth(int health, int maxHealth = 100)
    {
        if (healthText != null)
        {
            healthText.text = "+ " + health.ToString();
            if (health < 30) healthText.color = Color.red;
            else healthText.color = Color.green;
        }

        if (healthBarFill != null)
        {
            float pct = Mathf.Clamp01((float)health / maxHealth);
            healthBarFill.anchorMax = new Vector2(pct, 1);
            if (health < 30) healthBarFill.GetComponent<Image>().color = Color.red;
            else healthBarFill.GetComponent<Image>().color = Color.green;
        }
    }

    public void UpdateAmmo(int current, int max)
    {
        if (ammoText != null)
        {
            ammoText.text = current.ToString() + " / " + max.ToString();
        }
    }

    public void ShowHitMarker()
    {
        hitMarkerTimer = 0.2f;
        if(hitMarker != null)
            hitMarker.color = new Color(1, 0, 0, 1); // Red hit marker
    }

    void Update()
    {
        if (hitMarkerTimer > 0)
        {
            hitMarkerTimer -= Time.deltaTime;
            if (hitMarkerTimer <= 0 && hitMarker != null)
            {
                hitMarker.color = new Color(1, 1, 1, 0);
            }
        }
    }
}
