using UnityEngine;
using UnityEngine.UI;

public class FloatingDamage : MonoBehaviour
{
    private Text damageText;
    private float floatSpeed = 2f;
    private float fadeSpeed = 2f;
    private Color textColor;
    private Transform mainCamera;

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
        
        damageText = textObj.AddComponent<Text>();
        damageText.text = damageAmount.ToString();
        damageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        damageText.fontSize = 40;
        damageText.alignment = TextAnchor.MiddleCenter;
        damageText.color = Color.yellow;
        
        // Outline for better visibility
        Outline outline = textObj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1, -1);

        textColor = damageText.color;
        Destroy(gameObject, 1.5f);
    }

    void Update()
    {
        // Float up
        transform.position += Vector3.up * floatSpeed * Time.deltaTime;
        
        // Always face camera
        if (mainCamera != null)
        {
            transform.LookAt(transform.position + mainCamera.rotation * Vector3.forward,
                mainCamera.rotation * Vector3.up);
        }
        
        // Fade out
        textColor.a -= fadeSpeed * Time.deltaTime;
        if(damageText != null)
            damageText.color = textColor;
    }

    public static void Create(Vector3 position, int damageAmount)
    {
        GameObject go = new GameObject("FloatingDamage");
        go.transform.position = position + Vector3.up + new Vector3(Random.Range(-0.5f,0.5f), Random.Range(-0.5f,0.5f), Random.Range(-0.5f,0.5f));
        FloatingDamage fd = go.AddComponent<FloatingDamage>();
        fd.Setup(damageAmount);
    }
}
