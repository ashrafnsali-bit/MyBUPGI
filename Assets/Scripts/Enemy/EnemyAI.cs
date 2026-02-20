using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    [Header("Stats")]
    public float health = 100;
    public float damage = 1; // Minimum damage
    public float sightRange = 50;
    public float attackRange = 8;
    public float moveSpeed = 5;
    public float rotationSpeed = 10;
    public float fireRate = 0.4f; // Slower shooting

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
    private bool isDead = false;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();
        
        // AUTO-SETUP: Animator (Recursive Search)
        anim = GetComponentInChildren<Animator>();
        
        agent.speed = moveSpeed;
        agent.angularSpeed = rotationSpeed * 10;

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
        
        // DEBUG: Log current state
        // Debug.Log("Enemy distance to player: " + distance + ", sightRange: " + sightRange);

        if (distance <= attackRange)
        {
            Attack();
        }
        else if (distance <= sightRange)
        {
            Chase();
        }
        else
        {
            Idle();
        }
    }

    void Idle()
    {
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
        // Damp time 0.1f for smoother transition to stop
        SafeSetAnimFloat(speedParameter, 0, 0.1f);
    }

    void Chase()
    {
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(player.position);
            
            // CRITICAL FIX: Normalize speed to 0-1 for BlendTree
            // BlendTree expects: 0 = idle, 0.5 = walk, 1.0 = run
            float speed = agent.velocity.magnitude / moveSpeed; // Normalize to 0-1
            SafeSetAnimFloat(speedParameter, speed, 0.1f);
        }
    }

    void Attack()
    {
        // Debug.Log("Enemy State: ATTACKING"); // Very spammy, uncomment if needed
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
        
        // Face player
        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * rotationSpeed);
        }

        SafeSetAnimFloat(speedParameter, 0, 0.1f);

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
                    // Removed: bs.ignoreLayer = 0; -> Let the prefab handle layers, 
                    // or it will be set by the Inspector.
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
        health -= amount;
        SafeSetAnimTrigger(hitTrigger);
        if (health <= 0) Die();
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
        SafeSetAnimTrigger(dieTrigger);
        if (deathEffect != null) Instantiate(deathEffect, transform.position, transform.rotation);
        
        // Notify GameManager
        if (GameManager.instance != null) GameManager.instance.EnemyDied();

        Destroy(gameObject, destroyDelay);
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
        // Display Enemy Health above head
        if (Camera.main == null || health <= 0) return;

        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2f);
        if (screenPos.z > 0 && Vector3.Distance(transform.position, Camera.main.transform.position) < 20)
        {
            float barWidth = 100;
            float barHeight = 15;
            float x = screenPos.x - barWidth / 2;
            float y = Screen.height - screenPos.y;

            // Background
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(x, y, barWidth, barHeight), Texture2D.whiteTexture);

            // Health
            GUI.color = Color.red;
            float fill = Mathf.Clamp01(health / 100f); // Assuming 100 max health
            GUI.DrawTexture(new Rect(x, y, barWidth * fill, barHeight), Texture2D.whiteTexture);
        }
    }
}
