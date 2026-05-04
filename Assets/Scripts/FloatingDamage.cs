using UnityEngine;
using UnityEngine.UI;

public class FloatingDamage : MonoBehaviour
{
    private Text damageText;
    private float floatSpeed = 3f; // Start faster
    private float fadeSpeed = 1.5f;
    private Color textColor;
    private Transform mainCamera;
    private float lifeTime = 0f;
    private float maxLifeTime = 1.5f;
    private RectTransform textRect;

    public void Setup(int damageAmount)
    {
        mainCamera = Camera.main.transform;
        
        // Create Canvas dynamically
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        
        RectTransform rt = GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(100, 50);
        rt.localScale = new Vector3(0.02f, 0.02f, 0.02f);
        
        GameObject textObj = new GameObject("DamageText");
        textObj.transform.SetParent(transform, false);
        textRect = textObj.AddComponent<RectTransform>();
        textRect.localScale = Vector3.zero; // Start small for pop effect
        
        damageText = textObj.AddComponent<Text>();
        damageText.text = damageAmount.ToString();
        damageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        damageText.fontSize = 50;
        damageText.fontStyle = FontStyle.Bold;
        damageText.alignment = TextAnchor.MiddleCenter;
        
        // Color based on damage
        if (damageAmount >= 50) damageText.color = new Color(1f, 0.2f, 0f); // Orange/Red for high damage
        else damageText.color = Color.yellow; // Yellow for normal
        
        // Outline for better visibility
        Outline outline = textObj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2, -2);

        textColor = damageText.color;
        Destroy(gameObject, maxLifeTime);
    }

    void Update()
    {
        lifeTime += Time.deltaTime;
        
        // Float up with decaying speed
        float currentSpeed = Mathf.Lerp(floatSpeed, 0.5f, lifeTime / maxLifeTime);
        transform.position += Vector3.up * currentSpeed * Time.deltaTime;
        
        // Always face camera
        if (mainCamera != null)
        {
            transform.LookAt(transform.position + mainCamera.rotation * Vector3.forward,
                mainCamera.rotation * Vector3.up);
        }
        
        // Scale in pop effect
        if (lifeTime < 0.2f)
        {
            float scale = Mathf.Lerp(0f, 1.2f, lifeTime / 0.2f);
            textRect.localScale = new Vector3(scale, scale, scale);
        }
        else if (lifeTime < 0.3f)
        {
            // Bounce back slightly
            float scale = Mathf.Lerp(1.2f, 1f, (lifeTime - 0.2f) / 0.1f);
            textRect.localScale = new Vector3(scale, scale, scale);
        }
        
        // Fade out
        if (lifeTime > maxLifeTime * 0.5f)
        {
            textColor.a -= fadeSpeed * Time.deltaTime;
            if(damageText != null)
                damageText.color = textColor;
        }
    }

    public static void Create(Vector3 position, int damageAmount)
    {
        GameObject go = new GameObject("FloatingDamage");
        go.transform.position = position + Vector3.up + new Vector3(Random.Range(-0.5f,0.5f), Random.Range(-0.5f,0.5f), Random.Range(-0.5f,0.5f));
        FloatingDamage fd = go.AddComponent<FloatingDamage>();
        fd.Setup(damageAmount);
    }
}
