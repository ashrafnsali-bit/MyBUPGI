using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    [Header("Stats")]
    public float health = 100;
    private float maxHealth;
    public float damage = 10; // Increased to 10
    public float sightRange = 50;
    public float attackRange = 8;
    public float attackHysteresis = 2f; // Buffer to prevent jittery state switching
    public float moveSpeed = 5;
    public float rotationSpeed = 10;
    public float fireRate = 2.5f; // Shots per second. Changed from 0.4 to 2.5 for faster shooting
    
    [Header("Natural Movement")]
    public float wanderRadius = 15f; 
    public float wanderWaitTime = 3f;
    private float nextWanderTime;
    private bool isWandering = false;

    [Header("Tactical AI Options")]
    public bool enableHiveMind = true;
    public float alertRadius = 30f;
    public float retreatHealthThreshold = 0.3f; // 30% health
    public float dodgeChance = 0.4f; // 40% chance to dodge
    public float dodgeCooldown = 3f;

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
    
    // AI Tracking
    private float nextDodgeTime;
    private float flinchEndTime;
    private bool isRetreating = false;
    private bool hasAlertedOthers = false;

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
        
        // NAVIGATION TUNING: Snappier movement and better avoidance
        agent.speed = moveSpeed;
        agent.angularSpeed = rotationSpeed * 40; // SIGNIFICANTLY INCREASED: For faster turning while chasing
        agent.acceleration = moveSpeed * 3; // Snappier start/stop
        agent.stoppingDistance = attackRange * 0.8f; // Stop slightly before the absolute range limit
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance; // Prevent clumping

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

        // FLINCH MECHANIC: Stun the enemy briefly
        if (Time.time < flinchEndTime)
        {
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            return;
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
            
            // TACTICAL RETREAT
            float healthPct = health / maxHealth;
            isRetreating = (healthPct <= retreatHealthThreshold);

            // TACTICAL MOVEMENT (STRAFING / BACKING AWAY / DODGING)
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);
            
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
                // Pick a new strafe point every 2-4 seconds
                float strafeDirection = Random.value > 0.5f ? 1f : -1f;
                Vector3 strafePos = transform.position + (transform.right * strafeDirection * 4f);
                
                // If retreating, or player is too close, move backward
                if (isRetreating || distanceToPlayer < attackRange * 0.5f) {
                    strafePos -= transform.forward * 6f; // Move backwards
                }
                
                NavMeshHit navHit;
                if (UnityEngine.AI.NavMesh.SamplePosition(strafePos, out navHit, 3f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    strafeDestination = navHit.position;
                    if (agent != null && agent.isOnNavMesh) agent.SetDestination(strafeDestination);
                }
                nextStrafeTime = Time.time + Random.Range(1.0f, 2.5f); // Faster strafe updates
            }
            
            // Move towards strafe point if valid
            if (strafeDestination != Vector3.zero && Vector3.Distance(transform.position, strafeDestination) > 0.5f) {
                agent.isStopped = false;
                // Move faster when retreating or dodging
                float currentSpeed = isRetreating ? moveSpeed * 1.2f : moveSpeed * 0.7f;
                if (Time.time < nextStrafeTime - 1.0f) currentSpeed = moveSpeed * 1.5f; // Dodge burst speed
                agent.speed = currentSpeed; 
            } else {
                agent.isStopped = true;
            }
        }
        
        // Face player (CRITICAL: doing this while moving creates strafing effect)
        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed * 2.0f);
        }

        // Animation logic: show moving legs while aiming
        float moveVel = 0f;
        if (agent != null && !agent.isStopped) moveVel = agent.velocity.magnitude / moveSpeed;
        SafeSetAnimFloat(speedParameter, Mathf.Lerp(anim.GetFloat(speedParameter), moveVel, Time.deltaTime * 5), 0.05f);

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
                
                // Reload/Take cover pause after 3-6 shots
                if (burstShotsFired >= Random.Range(3, 7))
                {
                    isReloadingBurst = true;
                    burstReloadTime = Time.time + Random.Range(0.4f, 0.8f); // Reduced from 1-2.5s for less waiting
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
            Instantiate(muzzleFlash, firePoint.position, firePoint.rotation);
        }

        if (firePoint != null)
        {
            // DEBUG: Visualize fire direction
            Debug.DrawRay(firePoint.position, firePoint.forward * 10, Color.green, 2.0f);

            if (bulletPrefab != null)
            {
                // FORCE AIM: Calculate direction to player's center (approx 1.0m height)
                Vector3 targetPos = player.position + Vector3.up * 1.3f;
                Vector3 fireDirection = (targetPos - firePoint.position).normalized;
                
                // AIM SPREAD: Add slight randomness so enemies miss sometimes
                fireDirection.x += Random.Range(-0.08f, 0.08f);
                fireDirection.y += Random.Range(-0.08f, 0.08f);
                fireDirection.z += Random.Range(-0.08f, 0.08f);
                fireDirection.Normalize();

                Quaternion fireRotation = Quaternion.LookRotation(fireDirection);

                GameObject bullet = Instantiate(bulletPrefab, firePoint.position, fireRotation);
                bullet.SetActive(true); // Ensure it's active if prefab was inactive
                Rigidbody rb = bullet.GetComponent<Rigidbody>();
                if (rb != null) rb.linearVelocity = fireDirection * bulletSpeed;
                
                // IMPORTANT: Ignore collision between bullet and the enemy itself (Physics)
                Collider enemyCollider = GetComponent<Collider>();
                Collider bulletCollider = bullet.GetComponent<Collider>();
                if (enemyCollider != null && bulletCollider != null)
                {
                    Physics.IgnoreCollision(enemyCollider, bulletCollider);
                }

                // Ensure the bullet does damage and HITS the player
                BulletScript bs = bullet.GetComponent<BulletScript>();
                if (bs != null) {
                    bs.damage = damage;
                    bs.isEnemyBullet = true; 
                    bs.owner = gameObject; // Assign Owner
                    
                    // TARGET REFINEMENT: Ensure bullet ignores other enemies physical layer to avoid "friendly walling"
                    int enemyLayer = LayerMask.NameToLayer("Enemy");
                    if (enemyLayer != -1) {
                        bs.ignoreLayer |= (1 << enemyLayer);
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
                        
                        if (playerHealth != null) playerHealth.TakeDamage(damage);
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
        
        // FLINCH MECHANIC: If taking heavy damage, stun briefly
        if (amount >= 15f && health > amount)
        {
            flinchEndTime = Time.time + 0.2f; // Reduced from 0.6f so they don't freeze for too long
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
        }
        
        health -= amount;
        SafeSetAnimTrigger(hitTrigger);
        
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
