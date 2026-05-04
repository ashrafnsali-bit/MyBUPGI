using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 100; // Normalized to 100
    public float currentHealth;
    public float damageCooldown = 0.2f; // Prevents being hit too many times at once
    private float lastDamageTime;
    public AudioClip hitSound;
    public AudioClip deathSound;
    private bool isDead = false;
    
    void Start()
    {
        currentHealth = maxHealth;
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
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return; 
        if (Time.time < lastDamageTime + damageCooldown) return; // Ignores damage if too soon
        
        lastDamageTime = Time.time;
        currentHealth -= amount;
        if (hitSound) AudioSource.PlayClipAtPoint(hitSound, transform.position);
        Debug.Log("Player taken damage: " + amount + ". Current Health: " + currentHealth);
        
        if (UIManager.instance != null) 
        {
            UIManager.instance.UpdateHealth((int)currentHealth, (int)maxHealth);
            UIManager.instance.ShowDamageFlash();
        }
        if (CameraShake.instance != null) CameraShake.instance.TriggerShake(0.3f, 0.2f);
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
        
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        if (isDead) return; // Prevent multiple death calls
        isDead = true;
        
        Debug.Log("Player Died!");
        if (deathSound) AudioSource.PlayClipAtPoint(deathSound, transform.position);
        
        // Hide player model (don't destroy, just disable renderer)
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) r.enabled = false;
        
        // Disable player controls
        var movement = GetComponent<PlayerMovementScript>();
        if (movement) movement.enabled = false;
        
        // Notify GameManager
        if (GameManager.instance != null) GameManager.instance.PlayerDied();
    }

    // Removed OnGUI to use the new UIManager instead.
}
