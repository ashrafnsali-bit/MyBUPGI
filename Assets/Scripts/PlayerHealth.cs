using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 5000; // Drastically increased
    public float currentHealth;
    public float damageCooldown = 0.2f; // Prevents being hit too many times at once
    private float lastDamageTime;
    public AudioClip hitSound;
    public AudioClip deathSound;
    private bool isDead = false;
    
    void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return; 
        if (Time.time < lastDamageTime + damageCooldown) return; // Ignores damage if too soon
        
        lastDamageTime = Time.time;
        currentHealth -= amount;
        if (hitSound) AudioSource.PlayClipAtPoint(hitSound, transform.position);
        Debug.Log("Player taken damage: " + amount + ". Current Health: " + currentHealth);
        
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

    void OnGUI()
    {
        // Try to find GameManager if instance is lost
        if (GameManager.instance == null) GameManager.instance = Object.FindFirstObjectByType<GameManager>();
        
        if (GameManager.instance != null && GameManager.instance.isGameOver) return;

        // Draw Player Health Bar
        float barWidth = 300;
        float barHeight = 30;
        float x = 20;
        float y = 20;

        // Background (Black)
        GUI.color = Color.black; 
        GUI.DrawTexture(new Rect(x, y, barWidth, barHeight), Texture2D.whiteTexture);

        // Foreground (Green/Red based on health)
        GUI.color = currentHealth > (maxHealth * 0.3f) ? Color.green : Color.red;
        float fillPercent = Mathf.Clamp01(currentHealth / maxHealth);
        GUI.DrawTexture(new Rect(x, y, barWidth * fillPercent, barHeight), Texture2D.whiteTexture);

        // Text with explicit style to avoid skin issues
        GUIStyle textStyle = new GUIStyle();
        textStyle.fontSize = 20;
        textStyle.normal.textColor = Color.white;
        textStyle.alignment = TextAnchor.MiddleLeft;
        textStyle.padding = new RectOffset(10, 0, 0, 0);
        
        GUI.Label(new Rect(x, y, barWidth, barHeight), "PLAYER HEALTH: " + (int)currentHealth, textStyle);
        GUI.color = Color.white; // Reset color
    }
}
