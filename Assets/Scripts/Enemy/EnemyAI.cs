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
    public float fireRate = 0.4f; // Slower shooting
    
    [Header("Natural Movement")]
    public float wanderRadius = 15f; 
    public float wanderWaitTime = 3f;
    private float nextWanderTime;
    private bool isWandering = false;

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
    public bool autoFixHeight = false;
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

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();
        
        // AUTO-SETUP: Animator (Recursive Search)
        anim = GetComponentInChildren<Animator>();
        
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
                // Try finding it by raw path if Resources fail (simplified approach for this user's project structure)
                // Note: Resources.Load only works if it's in a Resources folder.
                // Since user has "Easy FPS/Prefabs/Bullet.prefab", we might need to rely on inspector assignment
                // OR try to load from a known Resources path if I move it there.
                // For now, let's just log a warning and use the raycast fallback.
                Debug.LogWarning("EnemyAI: Bullet Prefab is missing! Creating a fallback sphere.");
                GenerateFallbackBullet();
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
        
        if (player == null) {
            Debug.LogWarning("EnemyAI: Player is null! Enemy cannot move.");
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
    }

    void Wander()
    {
        if (agent == null || !agent.isOnNavMesh) return;

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
        // Debug.Log("Enemy State: ATTACKING"); // Very spammy, uncomment if needed
        if (agent != null && agent.isOnNavMesh) {
            agent.isStopped = true;
            agent.updateRotation = false; // Disable NavMesh rotation to face player manually
        }
        
        // Face player
        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed * 2.0f); // FASTER: Facinig the player when attacking
        }

        // Use ground velocity for animation to avoid floating speed values
        float moveVel = agent.velocity.magnitude / moveSpeed;
        SafeSetAnimFloat(speedParameter, Mathf.Lerp(anim.GetFloat(speedParameter), 0, Time.deltaTime * 5), 0.05f);

        // DEBUG: Visualize attack range
        Debug.DrawLine(transform.position, player.position, Color.red);

        // Check angle - only shoot if facing player properly? Reduced strictness for now.
        if (Time.time >= lastFireTime + (1f / fireRate))
        {
            Debug.Log("EnemyAI: Triggering Shoot...");
            Shoot();
            lastFireTime = Time.time;
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
        health -= amount;
        SafeSetAnimTrigger(hitTrigger);
        if (health <= 0) Die();
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
        RaycastHit hit;
        // Raycast from slightly above the enemy downward to find the actual floor
        if (Physics.Raycast(transform.position + Vector3.up * 1.0f, Vector3.down, out hit, 5.0f, LayerMask.GetMask("Default", "LevelPart")))
        {
            // Position model children relative to this root to align feet with ground
            float distanceToGround = hit.distance - 1.0f; // Subtract the 1.0f offset we added to the ray start
            foreach (Transform child in transform) {
                // If the child is the model/visuals, adjust its local Y
                if (child.GetComponent<Animator>() != null || child.name.ToLower().Contains("mesh") || child.name.ToLower().Contains("body")) {
                    child.localPosition = new Vector3(child.localPosition.x, -distanceToGround + modelYOffset, child.localPosition.z);
                }
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
            string navStatus = (agent != null && agent.isOnNavMesh) ? "ON NAVMESH" : "OFF NAVMESH";
            GUI.color = (agent != null && agent.isOnNavMesh) ? Color.white : Color.yellow;
            GUI.Label(new Rect(x + 5, y + barHeight, barWidth, 20), $"[{stateName}] {navStatus}");
        }
    }
}
