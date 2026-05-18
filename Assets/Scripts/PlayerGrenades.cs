using UnityEngine;

public class PlayerGrenades : MonoBehaviour
{
    public int grenadeCount = 3;
    public float throwForce = 15f;
    public float upwardForce = 5f;
    
    private Transform cameraMain;
    private GameObject grenadePrefab;

    void Start()
    {
        cameraMain = transform.Find("Main Camera");
        if (cameraMain == null) cameraMain = Camera.main.transform;
        
        UpdateUI();
        GenerateFallbackGrenade();
    }

    void GenerateFallbackGrenade()
    {
        // 1. Create empty root
        grenadePrefab = new GameObject("GrenadePrefab");
        
        // 2. Physical components
        var rb = grenadePrefab.AddComponent<Rigidbody>();
        rb.mass = 1.2f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        
        var collider = grenadePrefab.AddComponent<CapsuleCollider>();
        collider.radius = 0.08f;
        collider.height = 0.25f;
        
        // 3. Logic script
        grenadePrefab.AddComponent<Grenade>();
        
        // 4. Body (Capsule)
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.transform.SetParent(grenadePrefab.transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = new Vector3(0.16f, 0.10f, 0.16f);
        Destroy(body.GetComponent<Collider>());
        var bodyRend = body.GetComponent<Renderer>();
        if (bodyRend != null) {
            bodyRend.material = GetSafeMaterial(new Color(0.35f, 0.25f, 0.15f)); // Military Brown
        }

        // 5. Top Fuse (Cylinder)
        GameObject fuse = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        fuse.transform.SetParent(grenadePrefab.transform);
        fuse.transform.localPosition = new Vector3(0, 0.13f, 0);
        fuse.transform.localScale = new Vector3(0.06f, 0.04f, 0.06f);
        Destroy(fuse.GetComponent<Collider>());
        var fuseRend = fuse.GetComponent<Renderer>();
        if (fuseRend != null) {
            fuseRend.material = GetSafeMaterial(new Color(0.1f, 0.1f, 0.1f)); // Dark grey
        }
        
        // 6. Pin (Small Cube)
        GameObject pin = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pin.transform.SetParent(grenadePrefab.transform);
        pin.transform.localPosition = new Vector3(0.04f, 0.15f, 0);
        pin.transform.localScale = new Vector3(0.08f, 0.01f, 0.01f);
        Destroy(pin.GetComponent<Collider>());
        var pinRend = pin.GetComponent<Renderer>();
        if (pinRend != null) {
            pinRend.material = GetSafeMaterial(new Color(0.9f, 0.9f, 0.9f)); // Silver
        }

        grenadePrefab.SetActive(false);
    }

    Material GetSafeMaterial(Color color)
    {
        // Try URP Lit Shader first
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard"); // Fallback to Built-in Standard
        if (shader == null) shader = Shader.Find("Sprites/Default"); // Absolute fallback

        Material mat = new Material(shader);
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color); // URP Color Property
        
        // Optional metallic look if standard/URP
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.5f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.5f);

        return mat;
    }

    void Update()
    {
#if UNITY_ANDROID || UNITY_IOS
        if (UIManager.MobileGrenadePressed)
        {
            UIManager.MobileGrenadePressed = false;
            if (grenadeCount > 0 && Time.timeScale > 0)
            {
                ThrowGrenade();
            }
        }
#endif
        if (Input.GetKeyDown(KeyCode.G) && grenadeCount > 0 && Time.timeScale > 0)
        {
            ThrowGrenade();
        }
    }

    void ThrowGrenade()
    {
        grenadeCount--;
        UpdateUI();

        GameObject grenade = Instantiate(grenadePrefab, cameraMain.position + cameraMain.forward * 0.5f, cameraMain.rotation);
        grenade.name = "ThrownGrenade";
        grenade.SetActive(true);

        Rigidbody rb = grenade.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 forceToAdd = cameraMain.forward * throwForce + transform.up * upwardForce;
            rb.AddForce(forceToAdd, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 10f, ForceMode.Impulse);
        }
    }

    public void AddGrenades(int amount)
    {
        grenadeCount += amount;
        UpdateUI();
    }

    void UpdateUI()
    {
        if (UIManager.instance != null) UIManager.instance.UpdateGrenades(grenadeCount);
    }
}
