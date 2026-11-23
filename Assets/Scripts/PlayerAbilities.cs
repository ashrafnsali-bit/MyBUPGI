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

        // 5. Update unified Arabic Abilities & Shield HUD
        if (UIManager.instance != null)
        {
            float dashCd = Mathf.Max(0f, (lastDashTime + dashCooldown) - Time.time);
            float btCd = Mathf.Max(0f, (lastBulletTime + bulletTimeCooldown) - Time.unscaledTime);
            float sonarCd = Mathf.Max(0f, (lastSonarTime + sonarCooldown) - Time.time);
            UIManager.instance.UpdateAbilitiesHUD(currentShield, maxShield, dashCd, btCd, isBulletTimeActive, sonarCd);
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

        GameAudio.PlayDash();
        if (UIManager.instance != null) UIManager.instance.FlashWaveBanner(new Color(0.6f, 0.92f, 1f, 0.14f));
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

        GameAudio.PlaySlowMo();
        if (UIManager.instance != null) UIManager.instance.FlashWaveBanner(new Color(0.15f, 0.55f, 1f, 0.18f));
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
        System.Collections.Generic.List<EnemyAI> detected = new System.Collections.Generic.List<EnemyAI>();
        foreach (var e in enemies)
        {
            if (e != null && e.gameObject.activeInHierarchy && Vector3.Distance(transform.position, e.transform.position) <= sonarRadius)
            {
                detected.Add(e);
            }
        }

        int count = detected.Count;
        sonarMessage = $"SONAR SCAN: {count} HOSTILES DETECTED WITHIN {sonarRadius}m!";
        sonarMessageTimer = 3.5f;

        GameAudio.PlaySonarPing();
        if (UIManager.instance != null)
        {
            UIManager.instance.FlashWaveBanner(new Color(0.05f, 0.85f, 1f, 0.16f));
            string arabicMsg = count > 0
                ? ArabicUI.Format(ArabicStrings.SonarDetected, count)
                : ArabicStrings.SonarNone;
            UIManager.instance.ShowBuff(arabicMsg, 3.2f);
        }

        if (count > 0) StartCoroutine(HighlightSonarEnemies(detected, 4.0f));
        if (CameraShake.instance != null) CameraShake.instance.TriggerShake(0.2f, 0.1f);
    }

    IEnumerator HighlightSonarEnemies(System.Collections.Generic.List<EnemyAI> targets, float duration)
    {
        var saved = new System.Collections.Generic.List<System.Tuple<Renderer, Color>>();
        Color pulseColor = new Color(0.1f, 0.95f, 1f, 1f);
        foreach (var enemy in targets)
        {
            if (enemy == null) continue;
            foreach (var r in enemy.GetComponentsInChildren<Renderer>())
            {
                if (r == null || r.material == null) continue;
                saved.Add(new System.Tuple<Renderer, Color>(r, r.material.color));
                r.material.color = Color.Lerp(r.material.color, pulseColor, 0.65f);
            }
        }

        yield return new WaitForSeconds(duration);

        foreach (var pair in saved)
        {
            if (pair.Item1 != null && pair.Item1.material != null)
            {
                pair.Item1.material.color = pair.Item2;
            }
        }
    }

    void OnGUI()
    {
        if (playerHealth == null || Time.timeScale == 0) return;

        // Abilities & Shield HUD is now rendered in unified Arabic via UIManager.UpdateAbilitiesHUD.
        // Keep only the subtle blue Slow-Mo tint while Bullet Time is active.
        if (isBulletTimeActive)
        {
            GUI.color = new Color(0f, 0.5f, 1f, 0.08f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        }
    }
}
