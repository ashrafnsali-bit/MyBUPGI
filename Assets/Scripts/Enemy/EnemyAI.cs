using UnityEngine;
using UnityEngine.AI;
public enum EnemyArchetype { Assaulter, Sniper, Rusher, Tank, Grenadier }

public class EnemyAI : MonoBehaviour
{
    [Header("AI Archetype")]
    public EnemyArchetype archetype = EnemyArchetype.Assaulter;

    [Header("Stats")]
    public float health = 100;
    private float maxHealth;
    public float damage = 4; // Balanced damage per bullet
    public float sightRange = 30f; // Balanced vision range (was 500)
    public float attackRange = 18f; // Balanced engagement range (was 80)
    public float attackHysteresis = 4f; // Reasonable hysteresis
    public float moveSpeed = 6.0f;
    public float rotationSpeed = 14f;
    public float fireRate = 2.5f;
    
    [Header("Natural Movement")]
    public float wanderRadius = 12f; 
    public float wanderWaitTime = 3f;
    private float nextWanderTime;
    private bool isWandering = false;

    [Header("Tactical AI Options")]
    public bool enableHiveMind = true;
    public float alertRadius = 25f; // Alert nearby allies within 25m (was 150)
    public float retreatHealthThreshold = 0.0f;
    public float dodgeChance = 0.25f;
    public float dodgeCooldown = 2.0f;

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

    private float nextGrenadeTime;
    private GameObject enemyGrenadePrefab;
    private float nextContainerCheckTime = 0f;
    private bool hasSpottedPlayer = false;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();
        
        // AUTO-SETUP: Animator (Recursive Search)
        Animator[] anims = GetComponentsInChildren<Animator>();
        foreach (var a in anims)
        {
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
            if (anim != null) anim.enabled = false;
            
            foreach (AnimationState state in legacyAnim) {
                string n = state.name.ToLower();
                if (n.Contains("idle")) legacyIdle = state.name;
                if (n.Contains("run") || n.Contains("walk")) legacyRun = state.name;
            }
        }
        
        // AUTO-SETUP: Weapon
        var weaponSetup = GetComponent<EnemyWeaponSetup>();
        if (weaponSetup == null) weaponSetup = gameObject.AddComponent<EnemyWeaponSetup>();

        EnsureFirePoint();
        EnforceAggressiveStats();
        maxHealth = health;
    }

    void EnsureFirePoint()
    {
        if (firePoint == null) {
            var weaponSetup = GetComponent<EnemyWeaponSetup>();
            if (weaponSetup != null && weaponSetup.firePoint != null) {
                firePoint = weaponSetup.firePoint;
            }
        }

        if (firePoint == null) {
            Transform existingFp = transform.Find("FirePoint_Auto");
            if (existingFp != null) {
                firePoint = existingFp;
            } else {
                GameObject fp = new GameObject("FirePoint_Auto");
                fp.transform.SetParent(transform);
                fp.transform.localPosition = new Vector3(0.2f, 1.4f, 0.8f);
                fp.transform.localRotation = Quaternion.identity;
                firePoint = fp.transform;
            }
        }
    }

    private float spawnTime;

    void Start()
    {
        spawnTime = Time.time;
        FindPlayer();
        EnsureFirePoint();
        EnforceAggressiveStats();

        // AUTO-SETUP: Bullet Prefab
        if (bulletPrefab == null)
        {
            bulletPrefab = Resources.Load<GameObject>("Bullet");
            if (bulletPrefab == null)
            {
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

        // RIGIDBODY CONFIG: For smoother NavMesh control
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // NAVMESH SNAPPING
        if (agent != null) {
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(transform.position, out navHit, 10.0f, NavMesh.AllAreas)) {
                agent.Warp(navHit.position);
            }
        }

        if (autoFixHeight) FixModelHeight();
        EnsureOutsideContainer();
    }

    void FindPlayer()
    {
        if (player != null) return;

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) {
            var playerScript = FindFirstObjectByType<PlayerMovementScript>();
            if (playerScript != null) p = playerScript.gameObject;
        }
        if (p == null) {
            var playerHealth = FindFirstObjectByType<PlayerHealth>();
            if (playerHealth != null) p = playerHealth.gameObject;
        }
        
        if (p != null) {
            player = p.transform;
        }
    }

    private void EnforceAggressiveStats()
    {
        // Balanced, responsive combat stats
        health = Mathf.Max(health, 70f);
        damage = Mathf.Clamp(damage, 2f, 6f);
        retreatHealthThreshold = 0f;

        switch (archetype)
        {
            case EnemyArchetype.Sniper:
                damage = 7f;
                attackRange = 35f;
                sightRange = 55f;
                fireRate = 1.0f; 
                moveSpeed = 5.0f;
                break;
            case EnemyArchetype.Rusher:
                health = 80f;
                damage = 2f;
                attackRange = 14f;
                sightRange = 45f;
                moveSpeed = 8.0f;
                fireRate = 3.8f; 
                break;
            case EnemyArchetype.Tank:
                health = 150f;
                damage = 4f;
                attackRange = 16f;
                sightRange = 40f;
                moveSpeed = 4.5f;
                fireRate = 2.2f;
                break;
            case EnemyArchetype.Grenadier:
                health = 75f;
                damage = 3f;
                attackRange = 20f;
                sightRange = 45f;
                moveSpeed = 6.0f;
                fireRate = 2.5f;
                break;
            case EnemyArchetype.Assaulter:
            default:
                damage = 3f;
                attackRange = 22f;
                sightRange = 48f;
                moveSpeed = 6.5f;
                fireRate = 2.8f;
                break;
        }

        attackHysteresis = 5f;
        alertRadius = 35f;

        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.angularSpeed = 300f;
            agent.acceleration = 20f;
            agent.stoppingDistance = 2.5f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        }
    }

    private void GenerateFallbackBullet()
    {
        bulletPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bulletPrefab.name = "FallbackBullet";
        bulletPrefab.transform.localScale = Vector3.one * 0.15f;
        
        var rb = bulletPrefab.AddComponent<Rigidbody>();
        rb.useGravity = false;
        
        var bs = bulletPrefab.AddComponent<BulletScript>();
        bs.damage = damage;

        var rend = bulletPrefab.GetComponent<Renderer>();
        if (rend != null) {
            rend.material = new Material(Shader.Find("Sprites/Default"));
            rend.material.color = Color.red;
        }

        var trail = bulletPrefab.AddComponent<TrailRenderer>();
        trail.startWidth = 0.15f;
        trail.endWidth = 0.0f;
        trail.time = 0.4f;
        if (rend != null) trail.material = rend.material;

        bulletPrefab.SetActive(false);
    }

    void Update()
    {
        if (isDead) return;
        
        if (transform.position.y < -50f) 
        {
            Die();
            return;
        }
        
        if (player == null) {
            FindPlayer();
            if (player == null) {
                Wander();
                return;
            }
        }

        EnsureFirePoint();

        // Anti-hiding failsafe: periodically ensure enemy is not inside a container
        if (Time.time >= nextContainerCheckTime)
        {
            nextContainerCheckTime = Time.time + 2.0f;
            EnsureOutsideContainer();
        }

        // TRACK PLAYER VELOCITY FOR PREDICTIVE AIMING
        if (Time.deltaTime > 0) {
            playerVelocity = (player.position - lastPlayerPos) / Time.deltaTime;
            lastPlayerPos = player.position;
        }

        float distance = Vector3.Distance(transform.position, player.position);
        float currentAttackRange = isCurrentlyAttacking ? (attackRange + attackHysteresis) : attackRange;

        // Active visual spotting: if within sight range and has line of sight, spot player immediately!
        bool canSeePlayer = (distance <= sightRange) && HasLineOfSightToPlayer();

        if (canSeePlayer)
        {
            hasSpottedPlayer = true;
            if (!hasAlertedOthers && enableHiveMind)
            {
                AlertNearbyEnemies();
            }
        }

        // Behavior decision:
        if (canSeePlayer && distance <= currentAttackRange)
        {
            // Close enough and visible -> Shoot and strafe!
            isCurrentlyAttacking = true;
            Attack();
        }
        else if (hasSpottedPlayer || canSeePlayer || hasAlertedOthers)
        {
            // Spotted player or alerted -> Aggressively chase to engage! Never stand still!
            isCurrentlyAttacking = false;
            Chase();
        }
        else
        {
            // Far away and unspotted -> Patrol/Wander
            isCurrentlyAttacking = false;
            Wander();
        }
    }

    void Wander()
    {
        if (agent == null || !agent.isOnNavMesh) return;
        agent.updateRotation = true;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (isWandering) {
                isWandering = false;
                nextWanderTime = Time.time + Random.Range(1.0f, 2.0f); // Shorter active pauses
            }

            if (Time.time >= nextWanderTime)
            {
                Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
                randomDirection += transform.position;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(randomDirection, out hit, wanderRadius, 1))
                {
                    if (!IsInsideOrNearContainer(hit.position))
                    {
                        agent.isStopped = false;
                        agent.SetDestination(hit.position);
                        isWandering = true;
                    }
                }
            }
        }

        float speed = agent.velocity.magnitude / moveSpeed;
        SafeSetAnimFloat(speedParameter, speed * 0.5f, 0.2f);
    }

    void Chase()
    {
        if (agent != null && agent.isOnNavMesh && player != null)
        {
            agent.isStopped = false;
            agent.speed = moveSpeed;
            agent.stoppingDistance = 2.5f;
            agent.updateRotation = true;

            if (Time.time >= nextPathUpdateTime) {
                agent.SetDestination(player.position);
                nextPathUpdateTime = Time.time + 0.15f;
            }
            
            float speed = agent.velocity.magnitude / Mathf.Max(moveSpeed, 1f); 
            SafeSetAnimFloat(speedParameter, Mathf.Max(speed, 0.8f));
        }
    }

    void Attack()
    {
        if (player == null) return;
        if (Time.time < spawnTime + 0.3f) return; // Quick 0.3s reaction

        if (agent != null && agent.isOnNavMesh) {
            agent.updateRotation = false; // Turn manually to track player
            
            float distanceToPlayer = Vector3.Distance(transform.position, player.position);

            if (distanceToPlayer > 5.0f)
            {
                agent.isStopped = false;
                agent.speed = moveSpeed;
                agent.stoppingDistance = 3.0f;
                agent.SetDestination(player.position);
            }
            else
            {
                // Active close-quarters combat: strafe left/right, never stand still!
                if (Time.time >= nextStrafeTime)
                {
                    nextStrafeTime = Time.time + Random.Range(1.0f, 2.0f);
                    Vector3 strafeDir = Vector3.Cross((player.position - transform.position).normalized, Vector3.up);
                    if (Random.value < 0.5f) strafeDir = -strafeDir;
                    Vector3 candidatePos = transform.position + strafeDir * Random.Range(2.5f, 4.5f);
                    NavMeshHit strafeHit;
                    if (NavMesh.SamplePosition(candidatePos, out strafeHit, 3.0f, NavMesh.AllAreas))
                    {
                        agent.isStopped = false;
                        agent.speed = moveSpeed * 0.75f;
                        agent.SetDestination(strafeHit.position);
                    }
                }
            }
        }
        
        // Aim at player
        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 18.0f);
        }

        float moveVel = (agent != null && !agent.isStopped) ? (agent.velocity.magnitude / moveSpeed) : 0f;
        SafeSetAnimFloat(speedParameter, Mathf.Max(moveVel, 0.5f));

        Debug.DrawLine(transform.position, player.position, Color.red);

        // TACTICAL BURST FIRING
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
            float actualFireDelay = 1f / Mathf.Max(fireRate, 2.0f);
            if (Time.time >= lastFireTime + actualFireDelay)
            {
                if (archetype == EnemyArchetype.Grenadier && Time.time >= nextGrenadeTime && Random.value < 0.35f)
                {
                    ThrowGrenade();
                    lastFireTime = Time.time;
                    nextGrenadeTime = Time.time + Random.Range(5f, 8f);
                }
                else
                {
                    Shoot();
                    lastFireTime = Time.time;
                    burstShotsFired++;
                    
                    // Sustained burst of 4-7 shots, then brief 0.8s - 1.3s tactical pause
                    if (burstShotsFired >= Random.Range(4, 7))
                    {
                        isReloadingBurst = true;
                        burstReloadTime = Time.time + Random.Range(0.8f, 1.3f);
                    }
                }
            }
        }
    }

    public bool HasLineOfSightToPlayer()
    {
        if (player == null) return false;
        
        Vector3 eyePos = transform.position + Vector3.up * 1.5f;
        Vector3 playerChest = player.position + Vector3.up * 1.0f;
        Vector3 toPlayer = playerChest - eyePos;
        float dist = toPlayer.magnitude;

        RaycastHit[] hits = Physics.RaycastAll(eyePos, toPlayer.normalized, dist, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit h in hits)
        {
            if (h.transform == transform || h.transform.IsChildOf(transform) || h.transform.root == transform) continue;
            if (h.transform.GetComponentInParent<EnemyAI>() != null || h.transform.GetComponent<EnemyAI>() != null) continue;
            if (h.collider.isTrigger && !h.transform.CompareTag("Player")) continue;
            if (h.transform.name.Contains("Bullet")) continue;

            if (h.transform == player || h.transform.IsChildOf(player) || h.transform.root == player || h.transform.CompareTag("Player") || h.transform.GetComponentInParent<PlayerHealth>() != null)
            {
                return true;
            }

            // Hit solid wall or obstacle
            return false;
        }

        // No obstacles found between enemy and player
        return true;
    }

    void Shoot()
    {
        if (player == null) return;
        EnsureFirePoint();

        SafeSetAnimTrigger(shootTrigger);
        
        if (shootSound != null) AudioSource.PlayClipAtPoint(shootSound, transform.position);
        
        Vector3 spawnPos = (firePoint != null) ? firePoint.position : (transform.position + Vector3.up * 1.4f + transform.forward * 0.5f);
        Quaternion spawnRot = (firePoint != null) ? firePoint.rotation : transform.rotation;

        if (muzzleFlash != null)
        {
            GameObject flash = Instantiate(muzzleFlash, spawnPos, spawnRot);
            Destroy(flash, 0.05f);
        }

        Vector3 targetPos = player.position + Vector3.up * 1.0f;
        Vector3 baseDir = (targetPos - spawnPos).normalized;

        // Balanced bullet spread: depends on archetype and player movement
        float spreadAmount = 0.05f;
        switch (archetype)
        {
            case EnemyArchetype.Sniper:
                spreadAmount = 0.015f;
                break;
            case EnemyArchetype.Rusher:
                spreadAmount = 0.08f;
                break;
            case EnemyArchetype.Tank:
                spreadAmount = 0.065f;
                break;
            case EnemyArchetype.Assaulter:
            default:
                spreadAmount = 0.045f;
                break;
        }

        // Dodging reward: if player is sprinting/dodging, increase spread so bullets miss
        if (playerVelocity.magnitude > 1.2f)
        {
            spreadAmount += 0.025f;
        }

        Vector3 fireDirection = baseDir + new Vector3(
            Random.Range(-spreadAmount, spreadAmount), 
            Random.Range(-spreadAmount, spreadAmount), 
            Random.Range(-spreadAmount, spreadAmount)
        );
        fireDirection.Normalize();

        Quaternion fireRotation = Quaternion.LookRotation(fireDirection);

        // 1. VISUAL BULLET
        if (bulletPrefab != null)
        {
            GameObject bullet = Instantiate(bulletPrefab, spawnPos, fireRotation);
            bullet.SetActive(true); 
            Rigidbody rb = bullet.GetComponent<Rigidbody>();
            if (rb != null) rb.linearVelocity = fireDirection * bulletSpeed;
            
            BulletScript bs = bullet.GetComponent<BulletScript>();
            if (bs != null) {
                bs.damage = 0; // Visual bullet; raycast below applies fair synchronized damage
                bs.isEnemyBullet = true; 
                bs.owner = gameObject;
            }
        }

        // 2. FAIR HIT DETECTION ALONG SPREAD TRAJECTORY (Allows player to dodge)
        Vector3 rayOrigin = spawnPos;
        float maxBulletDist = attackRange + attackHysteresis + 3.0f;

        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, fireDirection, maxBulletDist, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit h in hits)
        {
            // Ignore shooter itself and all its limbs/weapons
            if (h.transform == transform || h.transform.IsChildOf(transform) || h.transform.root == transform) continue;
            
            // Ignore other enemies
            if (h.transform.GetComponentInParent<EnemyAI>() != null || h.transform.GetComponent<EnemyAI>() != null) continue;
            
            // Ignore triggers and bullets
            if (h.collider.isTrigger && !h.transform.CompareTag("Player")) continue;
            if (h.transform.name.Contains("Bullet")) continue;

            // If we reached the player or any player child collider:
            if (h.transform == player || h.transform.IsChildOf(player) || h.transform.root == player || h.transform.CompareTag("Player") || h.transform.GetComponentInParent<PlayerHealth>() != null)
            {
                PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
                if (playerHealth == null) playerHealth = player.GetComponentInParent<PlayerHealth>();
                if (playerHealth == null) playerHealth = Object.FindFirstObjectByType<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(damage, transform.position);
                }
                break;
            }

            // Hit a solid wall / obstacle - bullet is blocked
            break;
        }
    }

    void ThrowGrenade()
    {
        SafeSetAnimTrigger(shootTrigger);
        
        if (enemyGrenadePrefab == null)
        {
            enemyGrenadePrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            enemyGrenadePrefab.name = "EnemyGrenade";
            enemyGrenadePrefab.transform.localScale = Vector3.one * 0.2f;
            var rend = enemyGrenadePrefab.GetComponent<Renderer>();
            if (rend != null) rend.material.color = Color.black;
            
            var rb = enemyGrenadePrefab.AddComponent<Rigidbody>();
            var col = enemyGrenadePrefab.GetComponent<SphereCollider>();
            if (col == null) col = enemyGrenadePrefab.AddComponent<SphereCollider>();
            
            var gScript = enemyGrenadePrefab.AddComponent<Grenade>();
            gScript.explosionDamage = 45f;
            gScript.explosionRadius = 4.5f;
            gScript.delay = 2.5f;
            
            enemyGrenadePrefab.SetActive(false);
        }

        Vector3 spawnPos = (firePoint != null) ? firePoint.position : (transform.position + Vector3.up * 1.5f + transform.forward);
        GameObject grenade = Instantiate(enemyGrenadePrefab, spawnPos, transform.rotation);
        grenade.SetActive(true);
        
        Rigidbody grb = grenade.GetComponent<Rigidbody>();
        if (grb != null && player != null)
        {
            Vector3 direction = (player.position - spawnPos);
            float distance = direction.magnitude;
            Vector3 force = direction.normalized * Mathf.Min(distance * 0.8f, 15f) + Vector3.up * 5f;
            grb.AddForce(force, ForceMode.Impulse);
            grb.AddTorque(Random.insideUnitSphere * 10f, ForceMode.Impulse);
        }
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;
        Debug.Log(">>> Enemy " + gameObject.name + " HIT! Damage: " + amount + " Health: " + health + " -> " + (health - amount));
        
        // ANTI-STUNLOCK (FLINCH MECHANIC): No longer slows them down. Only applies minor visual knockback if hit extremely hard.
        if (amount >= 25f && health > amount && Time.time > flinchCooldownTime)
        {
            flinchCooldownTime = Time.time + 3.0f; 
            
            if (agent != null && agent.isOnNavMesh) 
            {
                if (player != null) {
                    Vector3 knockbackDir = (transform.position - player.position).normalized;
                    knockbackDir.y = 0;
                    agent.Move(knockbackDir * 0.2f); // Very minor physical Knockback
                }
            }
        }
        
        health -= amount;
        
        // Removed SafeSetAnimTrigger(hitTrigger) completely! 
        // Playing the Hit animation forces the enemy to stop shooting and looks like they are surrendering.
        
        hasSpottedPlayer = true;
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
        hasSpottedPlayer = true;
        
        float distToTarget = Vector3.Distance(transform.position, player.position);
        if (distToTarget <= alertRadius * 2.0f) {
            sightRange = Mathf.Max(sightRange, Mathf.Min(distToTarget + 10f, 55f));
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

        // Trigger Vampiric Heal for Player
        if (PlayerAbilities.instance != null) PlayerAbilities.instance.OnEnemyKilled();

        // 100% chance to drop a health pickup so you can easily find it
        DropHealthPickup();

        Destroy(gameObject, destroyDelay);
    }

    void DropHealthPickup()
    {
        GameObject pickup = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pickup.name = "HealthPickup";
        pickup.transform.position = transform.position + Vector3.up * 1.5f;
        pickup.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
        
        var rend = pickup.GetComponent<Renderer>();
        if (rend != null) {
            rend.material.color = Color.green; // Green box for health
        }
        
        // Add Rigidbody so it drops to the ground
        var rb = pickup.AddComponent<Rigidbody>();
        rb.mass = 1f;
        
        // Add trigger collider for the player to touch
        var sc = pickup.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 2.0f; // Increased radius to ensure player hits it
        
        var healScript = pickup.AddComponent<HealthPickup>();
        healScript.healAmount = 35f;
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

    public bool IsInsideOrNearContainer(Vector3 pos)
    {
        Collider[] colliders = Physics.OverlapSphere(pos + Vector3.up * 1.0f, 1.8f);
        foreach (var col in colliders)
        {
            if (col == null || col.isTrigger) continue;
            string n = col.name.ToLower();
            string rootN = col.transform.root.name.ToLower();
            if (n.Contains("container") || n.Contains("cargo") || rootN.Contains("container") || rootN.Contains("cargo"))
            {
                return true;
            }
        }

        RaycastHit[] hits = Physics.RaycastAll(pos + Vector3.up * 0.1f, Vector3.up, 3.5f);
        foreach (var hit in hits)
        {
            string n = hit.collider.name.ToLower();
            string rootN = hit.transform.root.name.ToLower();
            if (n.Contains("container") || n.Contains("cargo") || rootN.Contains("container") || rootN.Contains("cargo"))
            {
                return true;
            }
        }

        return false;
    }

    public void EnsureOutsideContainer()
    {
        if (isDead) return;

        Collider[] cols = Physics.OverlapSphere(transform.position + Vector3.up * 1.0f, 1.2f);
        bool inside = false;
        Collider containerCol = null;

        foreach (var col in cols)
        {
            if (col == null || col.isTrigger) continue;
            string n = col.name.ToLower();
            string rootN = col.transform.root.name.ToLower();
            if (n.Contains("container") || n.Contains("cargo") || rootN.Contains("container") || rootN.Contains("cargo"))
            {
                inside = true;
                containerCol = col;
                break;
            }
        }

        if (!inside)
        {
            RaycastHit[] roofHits = Physics.RaycastAll(transform.position + Vector3.up * 0.1f, Vector3.up, 3.5f);
            foreach (var rh in roofHits)
            {
                string n = rh.collider.name.ToLower();
                string rootN = rh.transform.root.name.ToLower();
                if (n.Contains("container") || n.Contains("cargo") || rootN.Contains("container") || rootN.Contains("cargo"))
                {
                    inside = true;
                    containerCol = rh.collider;
                    break;
                }
            }
        }

        if (inside && containerCol != null)
        {
            Vector3 containerCenter = containerCol.bounds.center;
            Vector3 pushDir = (transform.position - containerCenter);
            pushDir.y = 0;
            if (pushDir.sqrMagnitude < 0.1f) pushDir = containerCol.transform.forward;
            pushDir.Normalize();

            float extentsSize = Mathf.Max(containerCol.bounds.extents.x, containerCol.bounds.extents.z) + 2.5f;
            Vector3 targetSafePos = containerCenter + pushDir * extentsSize;
            targetSafePos.y = transform.position.y;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(targetSafePos, out hit, 10.0f, NavMesh.AllAreas))
            {
                if (agent != null && agent.isOnNavMesh)
                {
                    agent.Warp(hit.position);
                }
                else
                {
                    transform.position = hit.position;
                }
                Debug.LogWarning("Enemy " + gameObject.name + " was inside container " + containerCol.name + "! Ejected safely to: " + hit.position);
            }
        }
    }
}

