using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 100;
    public float currentHealth;
    public float damageCooldown = 0.45f; // Generous damage grace period
    private float lastDamageTime = -10f;
    public AudioClip hitSound;
    public AudioClip deathSound;
    private bool isDead = false;
    private float spawnTime;

    void Awake()
    {
        maxHealth = 100;
        currentHealth = maxHealth;
        spawnTime = Time.time;

        // Auto-create UIManager if it doesn't exist
        if (FindFirstObjectByType<UIManager>() == null)
        {
            GameObject uiObj = new GameObject("UIManager");
            uiObj.AddComponent<UIManager>();
        }

        // Auto-create GameManager if it doesn't exist
        if (FindFirstObjectByType<GameManager>() == null)
        {
            GameObject gmObj = new GameObject("GameManager");
            gmObj.AddComponent<GameManager>();
        }
    }

    void Start()
    {
        maxHealth = 100;
        currentHealth = maxHealth;
        spawnTime = Time.time;
        if (UIManager.instance != null) UIManager.instance.UpdateHealth((int)currentHealth, (int)maxHealth);
        
        // Auto-assign hit sound if missing
        if (hitSound == null)
        {
            var pms = GetComponent<PlayerMovementScript>();
            if (pms != null && pms._hitSound != null)
            {
                hitSound = pms._hitSound.clip;
            }
        }
        
        // Auto-attach Grenade system
        if (GetComponent<PlayerGrenades>() == null)
        {
            gameObject.AddComponent<PlayerGrenades>();
        }

        // Auto-attach Special Abilities system
        if (GetComponent<PlayerAbilities>() == null)
        {
            gameObject.AddComponent<PlayerAbilities>();
        }
    }

    void Update()
    {
        // Continuously ensure that if health reaches 0 or below, player dies immediately
        if (!isDead && currentHealth <= 0)
        {
            Die();
        }
    }

    public void TakeDamage(float amount, Vector3 sourcePosition = default(Vector3))
    {
        if (isDead) return; 

        // Spawn protection grace period (1 second at start of level)
        if (Time.time < spawnTime + 1.0f) return;

        // Damage cooldown (prevents stunlock from multiple bullets in same frame)
        if (Time.time < lastDamageTime + damageCooldown) return;
        lastDamageTime = Time.time;

        // Let Shield absorb damage
        var abilities = GetComponent<PlayerAbilities>();
        if (abilities != null)
        {
            amount = abilities.AbsorbDamageWithShield(amount);
        }

        if (amount > 0)
        {
            currentHealth -= amount;
        }

        // Always play hit feedback
        if (hitSound) AudioSource.PlayClipAtPoint(hitSound, transform.position);
        Debug.Log("Player taken damage: " + amount + ". Current Health: " + currentHealth);
        
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            if (UIManager.instance != null) 
            {
                UIManager.instance.UpdateHealth(0, (int)maxHealth);
            }
            Die();
            return;
        }

        if (UIManager.instance != null) 
        {
            UIManager.instance.UpdateHealth((int)currentHealth, (int)maxHealth);
            UIManager.instance.ShowDamageFlash();
            if (sourcePosition != default(Vector3)) {
                UIManager.instance.ShowDirectionalDamage(sourcePosition, transform);
            }
        }
        if (CameraShake.instance != null) CameraShake.instance.TriggerShake(0.25f, 0.15f);
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
    }

    public void Heal(float amount)
    {
        if (isDead) return;
        currentHealth += amount;
        if (currentHealth > maxHealth) currentHealth = maxHealth;
        
        if (UIManager.instance != null) 
        {
            UIManager.instance.UpdateHealth((int)currentHealth, (int)maxHealth);
        }
        Debug.Log("Player healed: " + amount + ". Current Health: " + currentHealth);
    }

    public void Die()
    {
        if (isDead) return; // Prevent multiple death calls
        isDead = true;
        currentHealth = 0;
        
        Debug.Log("Player Died!");
        if (deathSound) AudioSource.PlayClipAtPoint(deathSound, transform.position);
        
        // Hide player model and weapon renderers
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) r.enabled = false;
        
        // Disable player movement & looking
        var movement = GetComponent<PlayerMovementScript>();
        if (movement) movement.enabled = false;

        var mouseLook = GetComponent<MouseLookScript>();
        if (mouseLook) mouseLook.enabled = false;

        var charController = GetComponent<CharacterController>();
        if (charController) charController.enabled = false;

        // Disable weapons & shooting
        var gunInventory = GetComponent<GunInventory>();
        if (gunInventory) gunInventory.enabled = false;

        var gunScripts = GetComponentsInChildren<GunScript>(true);
        foreach (var gun in gunScripts) gun.enabled = false;

        // Disable abilities & grenades
        var abilities = GetComponent<PlayerAbilities>();
        if (abilities) abilities.enabled = false;

        var grenades = GetComponent<PlayerGrenades>();
        if (grenades) grenades.enabled = false;

        // Unlock mouse cursor for UI
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (UIManager.instance != null)
        {
            UIManager.instance.UpdateHealth(0, (int)maxHealth);
        }
        
        // Ensure GameManager triggers Game Over screen
        if (GameManager.instance == null)
        {
            var gm = FindFirstObjectByType<GameManager>();
            if (gm == null)
            {
                GameObject gmObj = new GameObject("GameManager");
                gm = gmObj.AddComponent<GameManager>();
            }
        }
        if (GameManager.instance != null) GameManager.instance.PlayerDied();
    }
}
