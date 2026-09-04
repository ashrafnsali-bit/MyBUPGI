using UnityEngine;
using System.Collections;

public class PlayerAbilities : MonoBehaviour
{
    public static PlayerAbilities instance;

    [Header("1. Tactical Dash")]
    public float dashForce = 28f;
    public float dashDuration = 0.25f;
    public float dashCooldown = 1.5f;
    private float lastDashTime = -10f;
    public bool isDashing = false;
    public bool isInvulnerable = false;

    [Header("2. Double Jump")]
    public float doubleJumpForce = 14f;
    private bool canDoubleJump = false;

    [Header("3. Adrenaline (Bullet Time)")]
    public float bulletTimeScale = 0.35f;
    public float bulletTimeDuration = 3.5f;
    public float bulletTimeCooldown = 7f;
    private float lastBulletTime = -20f;
    public bool isBulletTimeActive = false;
    private float bulletTimeEndTime = 0f;

    [Header("4. Energy Shield")]
    public float maxShield = 250f;
    public float currentShield = 250f;
    public float shieldRegenRate = 50f;
    public float shieldRegenDelay = 2.0f;
    private float lastDamageTime = -10f;

    [Header("5. Tactical Sonar Pulse")]
    public float sonarRadius = 80f;
    public float sonarCooldown = 6f;
    private float lastSonarTime = -10f;
    private string sonarMessage = "";
    private float sonarMessageTimer = 0f;

    private Rigidbody rb;
    private Camera playerCam;
    private PlayerMovementScript movementScript;
    private PlayerHealth playerHealth;
    private float defaultFOV;

    void Awake()
    {
        instance = this;
        rb = GetComponent<Rigidbody>();
        playerHealth = GetComponent<PlayerHealth>();
        movementScript = GetComponent<PlayerMovementScript>();
        
        playerCam = GetComponentInChildren<Camera>();
        if (playerCam != null) defaultFOV = playerCam.fieldOfView;
        else defaultFOV = 60f;
        
        currentShield = maxShield;
    }

    void Update()
    {
        // 1. Handle Shield Regeneration
        HandleShieldRegen();

        // 2. Handle Inputs
        HandleAbilitiesInput();

        // 3. Handle Bullet Time Timer
        if (isBulletTimeActive && Time.unscaledTime >= bulletTimeEndTime)
        {
            EndBulletTime();
        }

        // 4. Sonar message decay
        if (sonarMessageTimer > 0)
        {
            sonarMessageTimer -= Time.deltaTime;
            if (sonarMessageTimer <= 0) sonarMessage = "";
        }
    }

    void HandleAbilitiesInput()
    {
        // DASH (Left Shift or Q)
        if ((Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Q)) && CanDash())
        {
            StartCoroutine(PerformDash());
        }

        // BULLET TIME (F or E)
        if ((Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.E)) && CanBulletTime())
        {
            StartCoroutine(ActivateBulletTime());
        }

        // TACTICAL SONAR (X)
        if (Input.GetKeyDown(KeyCode.X) && CanSonar())
        {
            PerformSonarPulse();
        }

        // DOUBLE JUMP
        if (movementScript != null)
        {
            if (movementScript.grounded)
            {
                canDoubleJump = true;
            }
            else if (Input.GetKeyDown(KeyCode.Space) && canDoubleJump && !movementScript.grounded)
            {
                PerformDoubleJump();
            }
        }
    }

    public bool CanDash() => Time.time >= lastDashTime + dashCooldown && !isDashing;
    public bool CanBulletTime() => Time.unscaledTime >= lastBulletTime + bulletTimeCooldown && !isBulletTimeActive;
    public bool CanSonar() => Time.time >= lastSonarTime + sonarCooldown;

    IEnumerator PerformDash()
    {
        lastDashTime = Time.time;
        isDashing = true;
        isInvulnerable = true;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 inputDir = new Vector3(h, 0, v).normalized;

        Vector3 dashDir;
        if (inputDir.sqrMagnitude > 0.01f)
        {
            dashDir = transform.TransformDirection(inputDir);
        }
        else
        {
            dashDir = transform.forward;
        }

        dashDir.y = 0;
        dashDir.Normalize();

        if (rb != null)
        {
            rb.linearVelocity = new Vector3(dashDir.x * dashForce, 2f, dashDir.z * dashForce);
        }

        if (CameraShake.instance != null) CameraShake.instance.TriggerShake(0.15f, 0.15f);

        // FOV Warp effect
        if (playerCam != null) playerCam.fieldOfView = defaultFOV + 12f;

        yield return new WaitForSeconds(dashDuration);

        if (playerCam != null) playerCam.fieldOfView = defaultFOV;

        isInvulnerable = false;
        isDashing = false;
    }

    void PerformDoubleJump()
    {
        canDoubleJump = false;
        if (rb != null)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, doubleJumpForce, rb.linearVelocity.z);
        }

        if (CameraShake.instance != null) CameraShake.instance.TriggerShake(0.1f, 0.1f);
    }

    IEnumerator ActivateBulletTime()
    {
        isBulletTimeActive = true;
        lastBulletTime = Time.unscaledTime;
        bulletTimeEndTime = Time.unscaledTime + bulletTimeDuration;

        Time.timeScale = bulletTimeScale;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        if (CameraShake.instance != null) CameraShake.instance.TriggerShake(0.2f, 0.1f);

        yield return new WaitForSecondsRealtime(bulletTimeDuration);

        EndBulletTime();
    }

    void EndBulletTime()
    {
        if (!isBulletTimeActive) return;
        isBulletTimeActive = false;
        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;
    }

    void HandleShieldRegen()
    {
        // Fully disabled: Shield does NOT recharge automatically
    }

    public float AbsorbDamageWithShield(float amount)
    {
        lastDamageTime = Time.time;
        if (isInvulnerable) return 0f; // Invulnerable during Dash!

        if (currentShield > 0)
        {
            // Shield absorbs 85% of damage
            float shieldAbsorb = Mathf.Min(currentShield, amount * 0.85f);
            currentShield -= shieldAbsorb;
            amount -= shieldAbsorb;
        }

        return amount; // Remaining damage directly to HP
    }

    public void OnEnemyKilled()
    {
        // Auto-heal disabled: Health only restored by picking up Health Pickups in the world
    }

    void PerformSonarPulse()
    {
        lastSonarTime = Time.time;
        EnemyAI[] enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        int count = 0;
        foreach (var e in enemies)
        {
            if (e != null && e.gameObject.activeInHierarchy && Vector3.Distance(transform.position, e.transform.position) <= sonarRadius)
            {
                count++;
            }
        }

        sonarMessage = $"SONAR SCAN: {count} HOSTILES DETECTED WITHIN {sonarRadius}m!";
        sonarMessageTimer = 3.5f;

        if (CameraShake.instance != null) CameraShake.instance.TriggerShake(0.2f, 0.1f);
    }

    void OnGUI()
    {
        if (playerHealth == null || Time.timeScale == 0) return;

        // Modern Bottom-Left Abilities & Shield HUD (Positioned above Health Bar)
        float boxWidth = 260;
        float boxHeight = 135;
        float startX = 30;
        float startY = Screen.height - boxHeight - 145;

        // Background Panel
        GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.85f);
        GUI.DrawTexture(new Rect(startX, startY, boxWidth, boxHeight), Texture2D.whiteTexture);

        // Border
        GUI.color = new Color(0.2f, 0.6f, 1.0f, 0.8f);
        GUI.DrawTexture(new Rect(startX, startY, boxWidth, 2), Texture2D.whiteTexture);

        // SHIELD BAR
        GUI.color = Color.white;
        GUI.Label(new Rect(startX + 10, startY + 8, 200, 20), $"<b>ENERGY SHIELD: {(int)currentShield}/{(int)maxShield}</b>");

        // Shield bar background
        GUI.color = new Color(0.1f, 0.1f, 0.2f, 1f);
        GUI.DrawTexture(new Rect(startX + 10, startY + 28, boxWidth - 20, 14), Texture2D.whiteTexture);

        // Shield bar fill (Neon Cyan)
        GUI.color = new Color(0f, 0.8f, 1f, 1f);
        float shieldPct = Mathf.Clamp01(currentShield / maxShield);
        GUI.DrawTexture(new Rect(startX + 12, startY + 30, (boxWidth - 24) * shieldPct, 10), Texture2D.whiteTexture);

        // ABILITIES STATUS
        float dashCooldownRemaining = Mathf.Max(0, (lastDashTime + dashCooldown) - Time.time);
        float btCooldownRemaining = Mathf.Max(0, (lastBulletTime + bulletTimeCooldown) - Time.unscaledTime);
        float sonarCooldownRemaining = Mathf.Max(0, (lastSonarTime + sonarCooldown) - Time.time);

        // Dash Status
        string dashStatus = dashCooldownRemaining <= 0 ? "<color=#00FF66>READY [SHIFT/Q]</color>" : $"<color=#FFAA00>{dashCooldownRemaining:F1}s</color>";
        GUI.color = Color.white;
        GUI.Label(new Rect(startX + 10, startY + 48, 240, 20), $"<b>[DASH]:</b> {dashStatus}");

        // Bullet Time Status
        string btStatus = isBulletTimeActive ? "<color=#00E5FF>ACTIVE!</color>" : (btCooldownRemaining <= 0 ? "<color=#00FF66>READY [F]</color>" : $"<color=#FFAA00>{btCooldownRemaining:F1}s</color>");
        GUI.Label(new Rect(startX + 10, startY + 68, 240, 20), $"<b>[SLOW-MO]:</b> {btStatus}");

        // Sonar Status
        string sonarStatus = sonarCooldownRemaining <= 0 ? "<color=#00FF66>READY [X]</color>" : $"<color=#FFAA00>{sonarCooldownRemaining:F1}s</color>";
        GUI.Label(new Rect(startX + 10, startY + 88, 240, 20), $"<b>[SONAR]:</b> {sonarStatus}");

        // Double jump hint
        GUI.color = new Color(0.8f, 0.8f, 0.8f, 0.8f);
        GUI.Label(new Rect(startX + 10, startY + 110, 240, 20), "<size=11>SPACE in air: Double Jump</size>");

        // ACTIVE NOTIFICATION / SONAR BANNER
        if (!string.IsNullOrEmpty(sonarMessage))
        {
            float msgW = 450;
            float msgH = 40;
            float msgX = (Screen.width - msgW) / 2;
            float msgY = 120;

            GUI.color = new Color(0, 0, 0, 0.8f);
            GUI.DrawTexture(new Rect(msgX, msgY, msgW, msgH), Texture2D.whiteTexture);

            GUI.color = new Color(0f, 1f, 0.8f, 1f);
            GUI.DrawTexture(new Rect(msgX, msgY, msgW, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(msgX, msgY + msgH - 2, msgW, 2), Texture2D.whiteTexture);

            GUI.color = Color.cyan;
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(msgX, msgY, msgW, msgH), $"<b>{sonarMessage}</b>");
            GUI.skin.label.alignment = TextAnchor.UpperLeft;
        }

        // Bullet Time visual overlay
        if (isBulletTimeActive)
        {
            GUI.color = new Color(0f, 0.5f, 1f, 0.08f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        }
    }
}
