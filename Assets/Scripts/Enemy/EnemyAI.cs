using UnityEngine;
using UnityEngine.AI;
public enum EnemyArchetype { Assaulter, Sniper, Rusher }

public class EnemyAI : MonoBehaviour
{
    [Header("AI Archetype")]
    public EnemyArchetype archetype = EnemyArchetype.Assaulter;

    [Header("Stats")]
    public float health = 100;
    private float maxHealth;
    public float damage = 10;
    public float sightRange = 200; // Spots player across the map
    public float attackRange = 40; // Shoots from very far
    public float attackHysteresis = 5f; // Buffer to prevent jittery state switching
    public float moveSpeed = 6.5f; // Realistic sprint speed (fixes animation foot-sliding)
    public float rotationSpeed = 12; // Natural human turning speed
    public float fireRate = 8f; // Machine gun speed
    
    [Header("Natural Movement")]
    public float wanderRadius = 15f; 
    public float wanderWaitTime = 3f;
    private float nextWanderTime;
    private bool isWandering = false;

    [Header("Tactical AI Options")]
    public bool enableHiveMind = true;
    public float alertRadius = 80f; // Increased so one gunshot alerts the whole base
    public float retreatHealthThreshold = 0.2f; // Retreat only when very low
    public float dodgeChance = 0.6f; // 60% chance to dodge
    public float dodgeCooldown = 1.5f; // Dodge more frequently

    [Header("References")]
    public Transform firePoint;
    public GameObject muzzleFlash;
    public AudioClip shootSound;
    public GameObject bulletPrefab;
    public float bulletSpeed = 50;
    public GameObject deathEffect;
    public float destroyDelay = 3;

    [Header("Setup")]
    public float modelYOffset = 0;
    public bool autoFixHeight = true; // Changed to true by default to prevent sinking
    public string speedParameter = "Speed";
    public string shootTrigger = "Shoot";
    public string hitTrigger = "Hit";
    public string dieTrigger = "Die";
    public bool useLegacyAnimation = false;

    private NavMeshAgent agent;
    private Animator anim;
    private Animation legacyAnim;
    private string legacyIdle = "idle";
    private string legacyRun = "run";
    private Transform player;
    private float lastFireTime;
    private float nextPathUpdateTime;
    private bool isDead = false;
    private bool isCurrentlyAttacking = false;
    private float nextStrafeTime;
    private Vector3 strafeDestination;
    private bool isReloadingBurst = false;
    private float burstReloadTime;
    private int burstShotsFired = 0;
    
    private float nextDodgeTime;
    private float flinchEndTime;
    private float flinchCooldownTime; // Prevent stunlocking
    private bool isRetreating = false;
    private bool hasAlertedOthers = false;
    private Vector3 lastPlayerPos;
    private Vector3 playerVelocity;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();
        
        // AUTO-SETUP: Animator (Recursive Search)
        Animator[] anims = GetComponentsInChildren<Animator>();
        foreach (var a in anims)
        {
            // Prefer the animator that has an avatar (the actual 3D model) or a controller
            if (a.avatar != null || a.runtimeAnimatorController != null)
            {
                anim = a;
                break;
            }
        }
        if (anim == null && anims.Length > 0) anim = anims[0];

        // FALLBACK: Legacy Animation
        legacyAnim = GetComponentInChildren<Animation>();
        if (legacyAnim != null && (anim == null || anim.runtimeAnimatorController == null)) {
            useLegacyAnimation = true;
            
            // CRITICAL FIX FOR T-POSE: If Unity auto-added an empty Animator, it will BLOCK legacy animations and force a T-Pose.
            // We must disable it!
            if (anim != null) {
                anim.enabled = false;
                Debug.LogWarning("EnemyAI: Disabled empty Animator to allow Legacy Animation to play.");
            }
            
            // Auto-detect clip names to fix case-sensitivity issues
            foreach (AnimationState state in legacyAnim) {
                string n = state.name.ToLower();
                if (n.Contains("idle")) legacyIdle = state.name;
                if (n.Contains("run") || n.Contains("walk")) legacyRun = state.name;
            }
        }
        
        // NAVIGATION TUNING: Snappier movement and better avoidance
        agent.speed = moveSpeed;
        agent.angularSpeed = rotationSpeed * 15; // Smoother turning, less robotic
        agent.acceleration = moveSpeed * 1.5f; // Realistic human acceleration
        agent.stoppingDistance = attackRange * 0.2f; 
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

        // AUTO-SETUP: Weapon
        var weaponSetup = GetComponent<EnemyWeaponSetup>();
        if (weaponSetup == null) weaponSetup = gameObject.AddComponent<EnemyWeaponSetup>();
        
        // AUTO-SETUP: FirePoint (re-assign after weapon setup)
        if (firePoint == null) {
            // Wait for Start() to potentially set it up via WeaponSetup, or create fallback here.
             GameObject fp = new GameObject("FirePoint_Auto");
            fp.transform.SetParent(transform);
            fp.transform.localPosition = new Vector3(0.2f, 1.5f, 1.0f); // Increased Z to 1.0f to avoid self-collision
            fp.transform.localRotation = Quaternion.identity;
            firePoint = fp.transform;
        }

        maxHealth = health; // Store max health for UI scaling
    }

    void Start()
    {
        ApplyArchetypeStats();

        // Try to get firePoint from WeaponSetup if available
        var weaponSetup = GetComponent<EnemyWeaponSetup>();
        if (weaponSetup != null && weaponSetup.firePoint != null) {
            firePoint = weaponSetup.firePoint;
        }

        // AUTO-SETUP: Bullet Prefab
        if (bulletPrefab == null)
        {
            bulletPrefab = Resources.Load<GameObject>("Bullet");
            if (bulletPrefab == null)
            {
                Debug.LogWarning("EnemyAI: Bullet Prefab is missing! Creating a fallback sphere.");
                GenerateFallbackBullet();
            }
        }

        // AUTO-SETUP: Shoot Sound
        if (shootSound == null)
        {
            GunScript[] guns = Resources.FindObjectsOfTypeAll<GunScript>();
            foreach (var g in guns)
            {
                if (g.shoot_sound_source != null && g.shoot_sound_source.clip != null)
                {
                    shootSound = g.shoot_sound_source.clip;
                    break;
                }
            }
        }

        // ROBUST PLAYER DETECTION
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) {
            var playerScript = FindFirstObjectByType<PlayerMovementScript>();
            if (playerScript != null) p = playerScript.gameObject;
        }
        
        if (p != null) {
            player = p.transform;
            Debug.Log("EnemyAI: Found player at " + player.position);
        } else {
            Debug.LogError("EnemyAI: Could not find player! Make sure player has 'Player' tag.");
        }

        // RIGIDBODY CONFIG: For smoother NavMesh control
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // NAVMESH SNAPPING: Ensure enemy is on valid floor on start
        if (agent != null) {
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(transform.position, out navHit, 10.0f, NavMesh.AllAreas)) {
                agent.Warp(navHit.position);
            }
        }

        // AUTO-HEIGHT FIX: Align model to ground if requested
        if (autoFixHeight) FixModelHeight();
    }

    private void ApplyArchetypeStats()
    {
        switch (archetype)
        {
            case EnemyArchetype.Sniper:
                health *= 0.7f;
                damage *= 2.5f;
                attackRange = 30f;
                sightRange = 70f;
                fireRate = 0.5f; 
                moveSpeed *= 0.8f;
                dodgeChance = 0.1f;
                break;
            case EnemyArchetype.Rusher:
                health *= 1.3f;
                damage *= 0.8f;
                attackRange = 5f;
                moveSpeed *= 1.6f;
                fireRate = 3.5f; 
                dodgeChance = 0.7f;
                break;
        }
    }

    private void GenerateFallbackBullet()
    {
        // Create a simple sphere to act as a bullet
        bulletPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bulletPrefab.name = "FallbackBullet";
        bulletPrefab.transform.localScale = Vector3.one * 0.1f;
        
        // Add Rigidbody
        var rb = bulletPrefab.AddComponent<Rigidbody>();
        rb.useGravity = false; // Bullets usually fly straight
        
        // Add BulletScript so it deals damage
        var bs = bulletPrefab.AddComponent<BulletScript>();
        bs.damage = damage;
        bs.bloodEffect = null; // No effect for fallback
        bs.decalHitWall = null;

        // Make it red so it's visible
        var rend = bulletPrefab.GetComponent<Renderer>();
        if (rend != null) {
            rend.material = new Material(Shader.Find("Sprites/Default")); // Self-illuminated
            rend.material.color = Color.red;
        }

        // Add Trail Renderer for high visibility
        var trail = bulletPrefab.AddComponent<TrailRenderer>();
        trail.startWidth = 0.1f;
        trail.endWidth = 0.0f;
        trail.time = 0.5f;
        trail.material = rend.material;

        // Hide it so the 'prefab' itself isn't floating in the scene
        // We will activate copies when shooting
        bulletPrefab.SetActive(false);
    }

    void Update()
    {
        if (isDead) return;
        
        // Kill enemy if they fall off the map
        if (transform.position.y < -50f) 
        {
            Die();
            return;
        }
        
        if (player == null) {
            Debug.LogWarning("EnemyAI: Player is null! Enemy cannot move.");
            return;
        }

        // TRACK PLAYER VELOCITY FOR PREDICTIVE AIMING
        if (Time.deltaTime > 0) {
            playerVelocity = (player.position - lastPlayerPos) / Time.deltaTime;
            lastPlayerPos = player.position;
        }

        // FLINCH MECHANIC: Slow them down briefly but DO NOT stop them from shooting!
        if (Time.time < flinchEndTime)
        {
            if (agent != null && agent.isOnNavMesh) agent.speed = moveSpeed * 0.2f; // Slow down instead of freezing
            // We REMOVED the 'return;' here so they can still aim and shoot back even while taking damage!
        }

        float distance = Vector3.Distance(transform.position, player.position);
        
        // HYSTERESIS LOGIC: Stay in attack state unless player moves significantly away
        float currentAttackRange = isCurrentlyAttacking ? (attackRange + attackHysteresis) : attackRange;

        if (distance <= currentAttackRange)
        {
            isCurrentlyAttacking = true;
            Attack();
        }
        else
        {
            isCurrentlyAttacking = false;
            if (distance <= sightRange)
            {
                Chase();
            }
            else
            {
                Wander();
            }
        }
        
        // HIVE MIND ALERT: Alert others when we first spot the player
        if ((isCurrentlyAttacking || distance <= sightRange) && !hasAlertedOthers && enableHiveMind)
        {
            AlertNearbyEnemies();
        }
    }

    void Wander()
    {
        if (agent == null || !agent.isOnNavMesh) return;
        agent.updateRotation = true; // Ensure they look where they are going when wandering

        // If we reached the target or haven't started wandering, pick a new spot after waiting
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (isWandering) {
                isWandering = false;
                nextWanderTime = Time.time + Random.Range(wanderWaitTime * 0.5f, wanderWaitTime * 1.5f);
            }

            if (Time.time >= nextWanderTime)
            {
                Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
                randomDirection += transform.position;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(randomDirection, out hit, wanderRadius, 1))
                {
                    agent.isStopped = false;
                    agent.SetDestination(hit.position);
                    isWandering = true;
                }
            }
        }

        // Sync animation with wandering speed
        float speed = agent.velocity.magnitude / moveSpeed;
        SafeSetAnimFloat(speedParameter, speed * 0.5f, 0.2f); // Walk slowly while wandering
    }

    void Chase()
    {
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = moveSpeed; // Restore full speed
            agent.stoppingDistance = attackRange * 0.2f; // Stop at preferred distance
            agent.updateRotation = true; // Let NavMesh handle rotation during travel

            // PERFORMANCE: Throttle path updates
            if (Time.time >= nextPathUpdateTime) {
                agent.SetDestination(player.position);
                nextPathUpdateTime = Time.time + 0.2f; // Update path every 0.2s
            }
            
            // CRITICAL FIX: Normalize speed to 0-1 for BlendTree
            // BlendTree expects: 0 = idle, 0.5 = walk, 1.0 = run
            float speed = agent.velocity.magnitude / moveSpeed; 
            
            // SMOOTHING: Avoid jittery animation changes
            float currentAnimSpeed = anim.GetFloat(speedParameter);
            SafeSetAnimFloat(speedParameter, Mathf.Lerp(currentAnimSpeed, speed, Time.deltaTime * 5));
        }
    }

    void Attack()
    {
        if (agent != null && agent.isOnNavMesh) {
            agent.updateRotation = false; // Disable NavMesh rotation to face player manually
            
            // TACTICAL MOVEMENT (STRAFING / BACKING AWAY / DODGING)
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);

            // TACTICAL RETREAT
            float healthPct = health / maxHealth;
            isRetreating = (healthPct <= retreatHealthThreshold);

            if (archetype == EnemyArchetype.Sniper && distanceToPlayer < attackRange * 0.6f)
            {
                isRetreating = true;
            }
            if (archetype == EnemyArchetype.Rusher)
            {
                isRetreating = false; // Rushers never retreat
            }
            
            // DODGE LOGIC: Sudden burst of speed to the side to dodge bullets
            if (Time.time >= nextDodgeTime && Random.value < (dodgeChance * Time.deltaTime)) {
                float dodgeDir = Random.value > 0.5f ? 1f : -1f;
                Vector3 dodgePos = transform.position + (transform.right * dodgeDir * 6f);
                NavMeshHit navHit;
                if (NavMesh.SamplePosition(dodgePos, out navHit, 4f, NavMesh.AllAreas)) {
                    strafeDestination = navHit.position;
                    if (agent != null && agent.isOnNavMesh) agent.SetDestination(strafeDestination);
                    nextStrafeTime = Time.time + 1.5f; // Pause normal strafing
                }
                nextDodgeTime = Time.time + dodgeCooldown;
            }
            
            if (Time.time >= nextStrafeTime)
            {
                // SMART FLANKING / CIRCLE STRAFING
                Vector3 dirToPlayer = (player.position - transform.position).normalized;
                Vector3 rightDir = Vector3.Cross(dirToPlayer, Vector3.up).normalized;
                
                float strafeDirection = Random.value > 0.5f ? 1f : -1f;
                // Move sideways relative to player
                Vector3 strafePos = transform.position + (rightDir * strafeDirection * 6f);
                
                // Dynamic distance control - HYPER AGGRESSIVE
                if (isRetreating) {
                    strafePos -= dirToPlayer * 6f; // Back away if near death
                } else if (distanceToPlayer > attackRange * 0.3f && archetype != EnemyArchetype.Sniper) {
                    strafePos += dirToPlayer * 8f; // Push forward extremely aggressively
                }
                
                NavMeshHit navHit;
                if (UnityEngine.AI.NavMesh.SamplePosition(strafePos, out navHit, 4f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    strafeDestination = navHit.position;
                    if (agent != null && agent.isOnNavMesh) agent.SetDestination(strafeDestination);
                }
                nextStrafeTime = Time.time + Random.Range(1.0f, 2.0f); // Faster strafe updates for dynamic movement
            }
            
            // Move towards strafe point if valid
            if (strafeDestination != Vector3.zero) {
                agent.stoppingDistance = 0.5f; // MUST be small so they actually walk to the strafe point!
                agent.isStopped = false;
                
                // REALISM: Humans walk slower when strafing sideways or backwards
                float currentSpeed = moveSpeed * 0.65f; 
                if (isRetreating) currentSpeed = moveSpeed * 0.8f; // Moving backwards quickly
                agent.speed = currentSpeed; 
            }
        }
        
        // Face player (SMOOTH ROTATION)
        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            // Slerp with a fixed smooth speed instead of ultra-fast snapping
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 8.0f);
        }

        // Animation logic: show moving legs while aiming
        float moveVel = 0f;
        if (agent != null && !agent.isStopped) moveVel = agent.velocity.magnitude / moveSpeed;
        SafeSetAnimFloat(speedParameter, Mathf.Lerp(anim.GetFloat(speedParameter), moveVel, Time.deltaTime * 8f));

        // DEBUG: Visualize attack range
        Debug.DrawLine(transform.position, player.position, Color.red);

        // TACTICAL SHOOTING (BURST FIRE)
        if (isReloadingBurst)
        {
            if (Time.time >= burstReloadTime)
            {
                isReloadingBurst = false;
                burstShotsFired = 0;
            }
        }
        else
        {
            // If fireRate is very low (e.g. 0.4 from old inspector values), we use a max to prevent 2.5s delays
            float actualFireDelay = 1f / Mathf.Max(fireRate, 2.0f);
            if (Time.time >= lastFireTime + actualFireDelay)
            {
                Shoot();
                lastFireTime = Time.time;
                burstShotsFired++;
                
                // Reload/Take cover pause after 8-15 shots (Extremely long bursts)
                if (burstShotsFired >= Random.Range(8, 16))
                {
                    isReloadingBurst = true;
                    burstReloadTime = Time.time + Random.Range(0.1f, 0.3f); // Almost ZERO pause! Relentless pressure.
                }
            }
        }
    }

    void Shoot()
    {
        SafeSetAnimTrigger(shootTrigger);
        
        if (shootSound != null) AudioSource.PlayClipAtPoint(shootSound, transform.position);
        
        if (muzzleFlash != null && firePoint != null)
        {
            GameObject flash = Instantiate(muzzleFlash, firePoint.position, firePoint.rotation);
            Destroy(flash, 0.05f); // CRITICAL: Destroy instantly to prevent URP light limit crash!
        }

        if (firePoint != null)
        {
            // DEBUG: Visualize fire direction
            Debug.DrawRay(firePoint.position, firePoint.forward * 10, Color.green, 2.0f);

            if (bulletPrefab != null)
            {
                // PREDICTIVE AIMING: Calculate where the player will be
                Vector3 targetPos = player.position + Vector3.up * 1.3f;
                float dist = Vector3.Distance(firePoint.position, targetPos);
                float timeToHit = dist / bulletSpeed;
                
                Vector3 predictedPos = targetPos + (playerVelocity * timeToHit * 0.7f);
                Vector3 fireDirection = (predictedPos - firePoint.position).normalized;
                
                // AIM SPREAD
                fireDirection.x += Random.Range(-0.04f, 0.04f);
                fireDirection.y += Random.Range(-0.04f, 0.04f);
                fireDirection.z += Random.Range(-0.04f, 0.04f);
                fireDirection.Normalize();

                Quaternion fireRotation = Quaternion.LookRotation(fireDirection);

                // 1. VISUAL BULLET (No Damage)
                GameObject bullet = Instantiate(bulletPrefab, firePoint.position, fireRotation);
                bullet.SetActive(true); 
                Rigidbody rb = bullet.GetComponent<Rigidbody>();
                if (rb != null) rb.linearVelocity = fireDirection * bulletSpeed;
                
                BulletScript bs = bullet.GetComponent<BulletScript>();
                if (bs != null) {
                    bs.damage = 0; // Disable physical damage, we rely entirely on Hitscan now!
                    bs.isEnemyBullet = true; 
                    bs.owner = gameObject;
                }

                // 2. HITSCAN SYSTEM (100% Reliable Damage)
                // Use a Raycast to instantly detect hits on the player
                RaycastHit hit;
                // Raycast past the player to ensure we hit them even if they move slightly
                if (Physics.Raycast(firePoint.position, fireDirection, out hit, attackRange * 1.5f))
                {
                    if (hit.transform == player || hit.transform.IsChildOf(player))
                    {
                        PlayerHealth playerHealth = hit.transform.GetComponentInParent<PlayerHealth>();
                        if (playerHealth != null) {
                            playerHealth.TakeDamage(damage, transform.position);
                        }
                    }
                }
            }
            else
            {
                // FALLBACK RAYCAST
                Debug.DrawLine(firePoint.position, player.position, Color.yellow, 0.1f);
                RaycastHit hit;
                Vector3 direction = (player.position - firePoint.position).normalized;
                // Add spread
                direction.x += Random.Range(-0.05f, 0.05f);
                direction.y += Random.Range(-0.05f, 0.05f);
                
                // RaycastAll to pass through enemy triggers/colliders if needed, or just standard
                if (Physics.Raycast(firePoint.position, direction, out hit, attackRange))
                {
                    if (hit.transform == player || hit.transform.root == player)
                    {
                        Debug.Log("EnemyAI: Fallback Raycast HIT PLAYER!");
                        var playerHealth = hit.transform.GetComponent<PlayerHealth>();
                        if (playerHealth == null) playerHealth = hit.transform.GetComponentInParent<PlayerHealth>();
                        
                        if (playerHealth != null) playerHealth.TakeDamage(damage, transform.position);
                    }
                }
            }
        }
        else
        {
            Debug.LogError("EnemyAI: FirePoint is NULL! Cannot shoot.");
        }
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;
        Debug.Log(">>> Enemy " + gameObject.name + " HIT! Damage: " + amount + " Health: " + health + " -> " + (health - amount));
        
        // ANTI-STUNLOCK (FLINCH MECHANIC): Only flinch occasionally, never get stun-locked
        if (amount >= 15f && health > amount && Time.time > flinchCooldownTime)
        {
            flinchEndTime = Time.time + 0.2f; // Very brief flinch
            flinchCooldownTime = Time.time + 2.0f; // Cannot flinch again for 2 seconds (forces them to fight back)
            
            if (agent != null && agent.isOnNavMesh) 
            {
                if (player != null) {
                    Vector3 knockbackDir = (transform.position - player.position).normalized;
                    knockbackDir.y = 0;
                    agent.Move(knockbackDir * 0.5f); // Minor physical Knockback
                }
            }
        }
        
        health -= amount;
        
        // Play hit animation but don't interrupt shooting
        if (Time.time > flinchCooldownTime - 1.8f) SafeSetAnimTrigger(hitTrigger);
        
        // Alert others if shot from afar
        if (!hasAlertedOthers && enableHiveMind) AlertNearbyEnemies();
        
        if (health <= 0) Die();
    }

    public void AlertNearbyEnemies()
    {
        hasAlertedOthers = true;
        Collider[] cols = Physics.OverlapSphere(transform.position, alertRadius);
        foreach (Collider col in cols)
        {
            EnemyAI ally = col.GetComponentInParent<EnemyAI>();
            if (ally != null && ally != this && !ally.isDead)
            {
                ally.ReceiveAlert(player);
            }
        }
    }

    public void ReceiveAlert(Transform targetPlayer)
    {
        if (isDead || targetPlayer == null) return;
        if (player == null) player = targetPlayer;
        hasAlertedOthers = true; // Prevent infinite alert loops
        
        // Artificially boost sight range to ensure they start chasing immediately
        float distToTarget = Vector3.Distance(transform.position, player.position);
        if (sightRange < distToTarget + 5f) {
            sightRange = distToTarget + 10f;
        }
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;
        isCurrentlyAttacking = false;
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
        SafeSetAnimTrigger(dieTrigger);
        if (deathEffect != null) Instantiate(deathEffect, transform.position, transform.rotation);
        
        // Notify GameManager
        if (GameManager.instance != null) GameManager.instance.EnemyDied();

        Destroy(gameObject, destroyDelay);
    }

    private void FixModelHeight()
    {
        // Smart Height Fix using NavMeshAgent.baseOffset to prevent breaking Humanoid Animators
        if (agent == null) return;
        
        foreach (Transform child in transform) {
            // Find the child that has the visuals
            if (child.GetComponent<Animator>() != null || child.name.ToLower().Contains("mesh")) {
                Renderer[] renderers = child.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0) {
                    Bounds b = renderers[0].bounds;
                    foreach(var r in renderers) b.Encapsulate(r.bounds);
                    
                    // If the lowest point of the model is below the root's Y position
                    if (b.min.y < transform.position.y - 0.1f) {
                        float diff = transform.position.y - b.min.y;
                        // Lift the entire agent visually using baseOffset!
                        agent.baseOffset += (diff + modelYOffset);
                    } else if (modelYOffset != 0) {
                        agent.baseOffset += modelYOffset;
                    }
                }
                break; // Only check the main visual child
            }
        }
    }

    private void SafeSetAnimFloat(string param, float value, float dampTime = 0f)
    {
        if (useLegacyAnimation && legacyAnim != null)
        {
            if (param == speedParameter)
            {
                if (value > 0.1f)
                    legacyAnim.CrossFade(legacyRun, 0.2f);
                else
                    legacyAnim.CrossFade(legacyIdle, 0.2f);
            }
            return;
        }

        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return;
        try { 
            if (dampTime > 0)
                anim.SetFloat(param, value, dampTime, Time.deltaTime);
            else
                anim.SetFloat(param, value); 
        } catch { }
    }

    private void SafeSetAnimTrigger(string param)
    {
        if (useLegacyAnimation && legacyAnim != null) return; // Legacy doesn't use triggers

        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return;
        try { anim.SetTrigger(param); } catch { }
    }

    void OnGUI()
    {
        // Display Enemy Health and Status above head
        if (Camera.main == null || isDead) return;

        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2.2f);
        if (screenPos.z > 0 && Vector3.Distance(transform.position, Camera.main.transform.position) < 30)
        {
            float barWidth = 120;
            float barHeight = 15;
            float x = screenPos.x - barWidth / 2;
            float y = Screen.height - screenPos.y;

            // Background
            GUI.color = new Color(0, 0, 0, 0.5f);
            GUI.DrawTexture(new Rect(x, y, barWidth, barHeight + 20), Texture2D.whiteTexture);

            // Health Bar
            GUI.color = Color.red;
            float fill = Mathf.Clamp01(health / maxHealth); 
            GUI.DrawTexture(new Rect(x + 2, y + 2, (barWidth - 4) * fill, barHeight - 4), Texture2D.whiteTexture);

            // STATUS TEXT: Helps troubleshoot movement issues
            string stateName = isCurrentlyAttacking ? "ATTACK" : (agent.velocity.magnitude > 0.1f ? "CHASE" : "IDLE/WANDER");
            if (isRetreating) stateName = "RETREATING";
            if (Time.time < flinchEndTime) stateName = "FLINCHED";
            
            string navStatus = (agent != null && agent.isOnNavMesh) ? "ON NAVMESH" : "OFF NAVMESH";
            GUI.color = (agent != null && agent.isOnNavMesh) ? Color.white : Color.yellow;
            GUI.Label(new Rect(x + 5, y + barHeight, barWidth, 20), $"[{stateName}] {navStatus}");
        }
    }
}
