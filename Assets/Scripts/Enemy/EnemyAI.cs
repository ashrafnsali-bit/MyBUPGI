using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public enum EnemyArchetype { Assaulter, Sniper, Rusher, Tank, Grenadier }
public enum EnemyState { Patrol, Alert, Investigate, Engage, Reposition, Retreat, Dead }

public class EnemyAI : MonoBehaviour
{
    [Header("AI Archetype")]
    public EnemyArchetype archetype = EnemyArchetype.Assaulter;

    [Header("Stats")]
    public float health = 100;
    private float maxHealth;
    public float damage = 4;
    public float sightRange = 30f;
    public float attackRange = 18f;
    public float attackHysteresis = 4f;
    public float moveSpeed = 6.0f;
    public float rotationSpeed = 14f;
    public float fireRate = 2.5f;

    [Header("Perception")]
    [Tooltip("Full cone angle in degrees. Alerted enemies get a wider awareness cone.")]
    public float fieldOfView = 110f;
    [Tooltip("Radius within which a moving player can be heard through walls.")]
    public float hearingRadius = 14f;
    [Tooltip("Seconds of continuous detection before the enemy commits to an attack.")]
    public float reactionTime = 0.35f;
    [Tooltip("How long the enemy remembers the player after losing contact.")]
    public float memoryDuration = 8f;
    [Tooltip("How long the enemy searches the last known position before giving up.")]
    public float searchDuration = 5f;

    [Header("Tactics")]
    public bool enableHiveMind = true;
    public float alertRadius = 35f;
    public float retreatHealthThreshold = 0.18f;
    [Tooltip("Preferred engagement distance for this archetype.")]
    public float preferredRange = 16f;
    [Tooltip("How far a flanking enemy offsets to the side of the player.")]
    public float flankDistance = 6f;
    [Tooltip("Radius searched when looking for cover.")]
    public float coverSearchRadius = 8f;
    public bool useCover = true;

    [Header("Natural Movement")]
    public float wanderRadius = 12f;
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
    public bool autoFixHeight = true;
    public string speedParameter = "Speed";
    public string shootTrigger = "Shoot";
    public string hitTrigger = "Hit";
    public string dieTrigger = "Die";
    public bool useLegacyAnimation = false;

    [Header("Loot")]
    [Range(0f, 1f)] public float healthDropChance = 1f;

    [Header("Debug")]
    public bool showDebugOverlay = false;
    public bool showDebugLines = false;

    private NavMeshAgent agent;
    private Animator anim;
    private Animation legacyAnim;
    private string legacyIdle = "idle";
    private string legacyRun = "run";
    private Transform player;
    private float lastFireTime;
    private float nextPathUpdateTime;
    private bool isDead = false;
    public bool IsDead => isDead;

    // ---- New tactical AI runtime state ----
    private EnemyState state = EnemyState.Patrol;
    public EnemyState State => state;
    private float stateEnterTime;
    private float suspicionTimer;
    private float timeSinceContact = 999f;
    private float lastPlayerVelocitySample;
    private Vector3 lastKnownPlayerPosition;
    private Vector3 tacticalPoint;
    private int flankSlot;
    private static readonly List<EnemyAI> activeEnemies = new List<EnemyAI>();

    // ---- Combat runtime state (kept for compatibility) ----
    private bool isCurrentlyAttacking = false;
    private float nextStrafeTime;
    private bool isReloadingBurst = false;
    private float burstReloadTime;
    private int burstShotsFired;
    private float flinchCooldownTime;
    private bool hasAlertedOthers = false;
    private Vector3 lastPlayerPos;
    private Vector3 playerVelocity;
    private float nextGrenadeTime;
    private GameObject enemyGrenadePrefab;
    private float nextContainerCheckTime;
    private bool hasSpottedPlayer = false;
    private float spawnTime;
    private float nextPerceptionCheckTime;
    private bool cachedCanSeePlayer;
    private float orbitDirection = 1f; // persistent strafe side -> smooth orbit instead of random jitter

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();

        Animator[] anims = GetComponentsInChildren<Animator>();
        foreach (var a in anims)
        {
            if (a.avatar != null || a.runtimeAnimatorController != null) { anim = a; break; }
        }
        if (anim == null && anims.Length > 0) anim = anims[0];

        legacyAnim = GetComponentInChildren<Animation>();
        if (legacyAnim != null && (anim == null || anim.runtimeAnimatorController == null))
        {
            useLegacyAnimation = true;
            if (anim != null) anim.enabled = false;
            foreach (AnimationState s in legacyAnim)
            {
                string n = s.name.ToLower();
                if (n.Contains("idle")) legacyIdle = s.name;
                if (n.Contains("run") || n.Contains("walk")) legacyRun = s.name;
            }
        }

        if (GetComponent<EnemyWeaponSetup>() == null) gameObject.AddComponent<EnemyWeaponSetup>();
        EnsureFirePoint();
        ApplyArchetype();
    }

    void OnEnable()
    {
        if (!activeEnemies.Contains(this)) activeEnemies.Add(this);
    }

    void OnDisable()
    {
        activeEnemies.Remove(this);
    }

    /// <summary>Re-applies archetype stats. Called after GameManager changes the archetype on a spawned clone.</summary>
    public void ApplyArchetype()
    {
        health = Mathf.Max(health, 70f);
        damage = Mathf.Clamp(damage, 2f, 8f);
        retreatHealthThreshold = Mathf.Clamp(retreatHealthThreshold, 0f, 0.5f);

        switch (archetype)
        {
            case EnemyArchetype.Sniper:
                damage = 7f; attackRange = 35f; sightRange = 55f; fireRate = 1.0f; moveSpeed = 5.0f;
                fieldOfView = 60f; hearingRadius = 10f; preferredRange = 28f; reactionTime = 0.55f;
                coverSearchRadius = 10f; flankDistance = 3f;
                break;
            case EnemyArchetype.Rusher:
                health = 80f; damage = 2f; attackRange = 14f; sightRange = 45f; fireRate = 3.8f; moveSpeed = 8.0f;
                fieldOfView = 140f; hearingRadius = 20f; preferredRange = 8f; reactionTime = 0.20f;
                retreatHealthThreshold = 0f; coverSearchRadius = 3f; flankDistance = 4f;
                break;
            case EnemyArchetype.Tank:
                health = 150f; damage = 4f; attackRange = 16f; sightRange = 40f; fireRate = 2.2f; moveSpeed = 4.5f;
                fieldOfView = 100f; hearingRadius = 12f; preferredRange = 11f; reactionTime = 0.45f;
                retreatHealthThreshold = 0f; coverSearchRadius = 4f; flankDistance = 2f;
                break;
            case EnemyArchetype.Grenadier:
                health = 75f; damage = 3f; attackRange = 20f; sightRange = 45f; fireRate = 2.5f; moveSpeed = 6.0f;
                fieldOfView = 110f; hearingRadius = 14f; preferredRange = 17f; reactionTime = 0.35f;
                coverSearchRadius = 11f; flankDistance = 5f;
                break;
            case EnemyArchetype.Assaulter:
            default:
                damage = 3f; attackRange = 22f; sightRange = 48f; fireRate = 2.8f; moveSpeed = 6.5f;
                fieldOfView = 110f; hearingRadius = 14f; preferredRange = 15f; reactionTime = 0.30f;
                coverSearchRadius = 8f; flankDistance = 6f;
                break;
        }

        attackHysteresis = 5f;
        alertRadius = 35f;
        maxHealth = health;

        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.angularSpeed = 300f;
            agent.acceleration = 20f;
            agent.stoppingDistance = 2.5f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        }
    }

    void EnsureFirePoint()
    {
        if (firePoint == null)
        {
            var ws = GetComponent<EnemyWeaponSetup>();
            if (ws != null && ws.firePoint != null) firePoint = ws.firePoint;
        }
        if (firePoint == null)
        {
            Transform existing = transform.Find("FirePoint_Auto");
            if (existing != null) firePoint = existing;
            else
            {
                GameObject fp = new GameObject("FirePoint_Auto");
                fp.transform.SetParent(transform);
                fp.transform.localPosition = new Vector3(0.2f, 1.4f, 0.8f);
                fp.transform.localRotation = Quaternion.identity;
                firePoint = fp.transform;
            }
        }
    }

    void Start()
    {
        spawnTime = Time.time;
        FindPlayer();
        EnsureFirePoint();
        ApplyArchetype();

        if (bulletPrefab == null)
        {
            bulletPrefab = Resources.Load<GameObject>("Bullet");
            if (bulletPrefab == null) GenerateFallbackBullet();
        }

        if (shootSound == null)
        {
            foreach (var g in Resources.FindObjectsOfTypeAll<GunScript>())
            {
                if (g.shoot_sound_source != null && g.shoot_sound_source.clip != null)
                {
                    shootSound = g.shoot_sound_source.clip;
                    break;
                }
            }
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }

        if (agent != null)
        {
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(transform.position, out navHit, 10f, NavMesh.AllAreas)) agent.Warp(navHit.position);
        }

        if (autoFixHeight) FixModelHeight();
        EnsureOutsideContainer();

        lastKnownPlayerPosition = transform.position;
        tacticalPoint = transform.position;
        flankSlot = AssignFlankSlot();
        orbitDirection = flankSlot == 0 ? (Random.value < 0.5f ? -1f : 1f) : (flankSlot < 0 ? -1f : 1f);
        SetState(EnemyState.Patrol);
    }

    void FindPlayer()
    {
        if (player != null) return;
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null)
        {
            var pm = FindFirstObjectByType<PlayerMovementScript>();
            if (pm != null) p = pm.gameObject;
        }
        if (p == null)
        {
            var ph = FindFirstObjectByType<PlayerHealth>();
            if (ph != null) p = ph.gameObject;
        }
        if (p != null) player = p.transform;
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
        if (rend != null)
        {
            rend.material = new Material(Shader.Find("Sprites/Default"));
            rend.material.color = Color.red;
        }
        var trail = bulletPrefab.AddComponent<TrailRenderer>();
        trail.startWidth = 0.15f; trail.endWidth = 0f; trail.time = 0.4f;
        if (rend != null) trail.material = rend.material;
        bulletPrefab.SetActive(false);
    }

    void Update()
    {
        if (isDead) return;
        if (transform.position.y < -50f) { Die(); return; }

        if (player == null)
        {
            FindPlayer();
            if (player == null) { Wander(); return; }
        }

        EnsureFirePoint();

        if (Time.time >= nextContainerCheckTime)
        {
            nextContainerCheckTime = Time.time + 0.25f;
            EnsureOutsideContainer();
        }

        if (Time.deltaTime > 0f && lastPlayerPos != Vector3.zero)
            playerVelocity = (player.position - lastPlayerPos) / Time.deltaTime;
        lastPlayerPos = player.position;

        float distance = Vector3.Distance(transform.position, player.position);

        // Throttle the expensive line-of-sight raycast to ~10 Hz per enemy.
        if (Time.time >= nextPerceptionCheckTime)
        {
            nextPerceptionCheckTime = Time.time + 0.1f;
            cachedCanSeePlayer = CanSeePlayer(distance);
        }
        bool sees = cachedCanSeePlayer;
        bool hears = CanHearPlayer(distance); // cheap distance-only check

        if (sees || hears)
        {
            timeSinceContact = 0f;
            lastKnownPlayerPosition = player.position;
            suspicionTimer += Time.deltaTime;
        }
        else
        {
            timeSinceContact += Time.deltaTime;
            suspicionTimer = Mathf.Max(0f, suspicionTimer - Time.deltaTime * 1.5f);
        }

        UpdateStateMachine(distance, sees);
        UpdateAnimation();
    }

    // ------------------------------------------------------------------
    // PERCEPTION
    // ------------------------------------------------------------------

    bool CanSeePlayer(float distance)
    {
        if (player == null || distance > sightRange) return false;

        float cone = hasSpottedPlayer ? Mathf.Min(360f, fieldOfView * 2f) : fieldOfView;
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.01f && Vector3.Angle(transform.forward, toPlayer) > cone * 0.5f)
            return false;

        return HasLineOfSightToPlayer();
    }

    bool CanHearPlayer(float distance)
    {
        if (player == null) return false;
        float radius = hearingRadius;
        var pm = player.GetComponent<PlayerMovementScript>();
        if (pm != null && pm.currentSpeed > 4f) radius *= 1.7f; // sprinting is louder
        return distance <= radius;
    }

    public bool HasLineOfSightToPlayer()
    {
        return HasLineOfSightFrom(transform.position, player != null ? player.position : transform.position);
    }

    bool HasLineOfSightFrom(Vector3 from, Vector3 to)
    {
        if (ContainerObstacleManager.IsPointInsideAnyContainer(from, 0.2f)) return false;

        Vector3 eye = from + Vector3.up * 1.5f;
        Vector3 chest = to + Vector3.up * 1.0f;
        Vector3 delta = chest - eye;
        float dist = delta.magnitude;
        if (dist < 0.01f) return true;

        RaycastHit[] hits = Physics.RaycastAll(eye, delta.normalized, dist, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit h in hits)
        {
            if (h.transform == transform || h.transform.IsChildOf(transform) || h.transform.root == transform) continue;
            if (h.transform.GetComponentInParent<EnemyAI>() != null) continue;
            if (h.collider.isTrigger && !h.transform.CompareTag("Player")) continue;
            if (h.transform.name.Contains("Bullet")) continue;
            if (h.transform == player || h.transform.IsChildOf(player) || h.transform.root == player ||
                h.transform.CompareTag("Player") || h.transform.GetComponentInParent<PlayerHealth>() != null)
                return true;
            return false;
        }
        return true;
    }

    // ------------------------------------------------------------------
    // STATE MACHINE
    // ------------------------------------------------------------------

    void SetState(EnemyState newState)
    {
        if (state == newState || state == EnemyState.Dead) return;
        state = newState;
        stateEnterTime = Time.time;
        if (newState == EnemyState.Engage) isCurrentlyAttacking = true;
        else if (newState != EnemyState.Reposition) isCurrentlyAttacking = false;
    }

    void UpdateStateMachine(float distance, bool sees)
    {
        switch (state)
        {
            case EnemyState.Patrol:
                if (sees && suspicionTimer >= reactionTime)
                {
                    if (!hasAlertedOthers && enableHiveMind) AlertNearbyEnemies();
                    SetState(EnemyState.Engage);
                }
                else if (suspicionTimer > 0.08f)
                {
                    SetState(EnemyState.Alert);
                }
                else Wander();
                break;

            case EnemyState.Alert:
                StopMovement();
                FacePosition(lastKnownPlayerPosition, 8f);
                if (sees && suspicionTimer >= reactionTime) SetState(EnemyState.Engage);
                else if (Time.time - stateEnterTime > 0.9f) SetState(EnemyState.Investigate);
                break;

            case EnemyState.Investigate:
                MoveTo(lastKnownPlayerPosition, moveSpeed * 0.85f, 2.5f, 0.2f);
                FaceMovementOrTarget(lastKnownPlayerPosition);
                if (sees && distance <= attackRange + attackHysteresis) SetState(EnemyState.Engage);
                else if (timeSinceContact > memoryDuration || Time.time - stateEnterTime > searchDuration + memoryDuration)
                {
                    ForgetPlayer();
                    SetState(EnemyState.Patrol);
                }
                else if (agent != null && agent.remainingDistance <= 2.5f && !agent.pathPending)
                {
                    SearchAround();
                }
                break;

            case EnemyState.Engage:
                if (!sees && timeSinceContact > 1.5f)
                {
                    tacticalPoint = lastKnownPlayerPosition;
                    SetState(EnemyState.Investigate);
                    break;
                }
                if (health / Mathf.Max(maxHealth, 1f) <= retreatHealthThreshold && archetype != EnemyArchetype.Tank)
                {
                    SetState(EnemyState.Retreat);
                    break;
                }
                if (ShouldReposition(distance))
                {
                    tacticalPoint = ChooseTacticalPoint(distance);
                    SetState(EnemyState.Reposition);
                    break;
                }
                EngageMovement(distance);
                FacePosition(player.position, 14f);
                TryFire();
                if (showDebugLines) Debug.DrawLine(transform.position, player.position, Color.red);
                break;

            case EnemyState.Reposition:
                FacePosition(player.position, 9f);
                if (sees) TryFire();
                if (MoveTo(tacticalPoint, moveSpeed, 1.5f, 0.2f) || Time.time - stateEnterTime > 3.5f)
                    SetState(EnemyState.Engage);
                break;

            case EnemyState.Retreat:
                Vector3 away = transform.position - lastKnownPlayerPosition;
                away.y = 0f;
                if (away.sqrMagnitude < 0.01f) away = -transform.forward;
                MoveTo(transform.position + away.normalized * 12f, moveSpeed, 1f, 0.25f);
                FacePosition(lastKnownPlayerPosition, 7f);
                if (Time.time - stateEnterTime > 3.5f || timeSinceContact > 2.5f)
                    SetState(EnemyState.Engage);
                break;
        }
    }

    void ForgetPlayer()
    {
        hasSpottedPlayer = false;
        hasAlertedOthers = false;
        suspicionTimer = 0f;
        timeSinceContact = 0f;
        // Do NOT call ApplyArchetype() here - it would refill health on a wounded enemy.
    }

    bool ShouldReposition(float distance)
    {
        if (Time.time - stateEnterTime < 2.2f) return false;
        if (Random.value > Time.deltaTime * 0.35f) return false;

        if (archetype == EnemyArchetype.Sniper && distance < preferredRange * 0.6f) return true;
        if (archetype == EnemyArchetype.Rusher && distance > preferredRange * 1.6f) return true;
        if (archetype == EnemyArchetype.Grenadier && Time.time >= nextGrenadeTime) return true;
        if (useCover && !HasLineOfSightFrom(transform.position, player.position)) return true;
        return Random.value < 0.35f && distance > preferredRange;
    }

    Vector3 ChooseTacticalPoint(float distance)
    {
        if (useCover)
        {
            Vector3 cover = FindCoverPoint(player.position);
            if (cover != transform.position) return cover;
        }

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.01f) toPlayer = transform.forward;
        Vector3 side = Vector3.Cross(toPlayer.normalized, Vector3.up);
        float offset = flankSlot * flankDistance;
        Vector3 candidate = player.position - toPlayer.normalized * preferredRange + side * offset;
        return SnapToNavMesh(candidate, 8f);
    }

    Vector3 FindCoverPoint(Vector3 threat)
    {
        Vector3 best = transform.position;
        float bestScore = -1f;
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.PI * 2f / 10f + Random.Range(-0.25f, 0.25f);
            Vector3 candidate = transform.position +
                new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * coverSearchRadius;
            NavMeshHit nh;
            if (!NavMesh.SamplePosition(candidate, out nh, 2.5f, NavMesh.AllAreas)) continue;
            if (IsInsideOrNearContainer(nh.position)) continue;

            bool blocked = !HasLineOfSightFrom(nh.position, threat);
            float dist = Vector3.Distance(nh.position, threat);
            float score = (blocked ? 2.5f : 0f) - Mathf.Abs(dist - preferredRange) * 0.06f;
            if (score > bestScore) { bestScore = score; best = nh.position; }
        }
        return best;
    }

    int AssignFlankSlot()
    {
        int[] counts = new int[3];
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            if (activeEnemies[i] == null) { activeEnemies.RemoveAt(i); continue; }
            if (activeEnemies[i].state == EnemyState.Engage || activeEnemies[i].state == EnemyState.Reposition)
                counts[activeEnemies[i].flankSlot + 1]++;
        }
        int min = Mathf.Min(counts[0], counts[1], counts[2]);
        if (min == counts[0]) return -1;
        if (min == counts[2]) return 1;
        return 0;
    }

    // ------------------------------------------------------------------
    // MOVEMENT
    // ------------------------------------------------------------------

    void Wander()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (isWandering)
            {
                isWandering = false;
                nextWanderTime = Time.time + Random.Range(wanderWaitTime * 0.6f, wanderWaitTime);
            }
            if (Time.time >= nextWanderTime)
            {
                Vector3 randomDir = Random.insideUnitSphere * wanderRadius;
                randomDir.y = 0f;
                Vector3 dest = SnapToNavMesh(transform.position + randomDir, wanderRadius);
                if (dest != transform.position && !IsInsideOrNearContainer(dest))
                {
                    agent.isStopped = false;
                    agent.speed = moveSpeed * 0.45f;
                    if (Time.time >= nextPathUpdateTime)
                    {
                        agent.SetDestination(dest);
                        nextPathUpdateTime = Time.time + 0.3f;
                    }
                    isWandering = true;
                }
            }
        }
    }

    bool MoveTo(Vector3 destination, float speed, float stopDistance, float pathInterval)
    {
        if (agent == null || !agent.isOnNavMesh) return false;
        Vector3 dest = SnapToNavMesh(destination, 8f);
        agent.isStopped = false;
        agent.speed = speed;
        agent.stoppingDistance = stopDistance;
        if (Time.time >= nextPathUpdateTime)
        {
            agent.SetDestination(dest);
            nextPathUpdateTime = Time.time + pathInterval;
        }
        return !agent.pathPending && agent.remainingDistance <= stopDistance + 0.2f;
    }

    void MoveToValid(Vector3 destination, float speed)
    {
        MoveTo(destination, speed, 1f, 0.25f);
    }

    void StopMovement()
    {
        if (agent == null || !agent.isOnNavMesh) return;
        agent.isStopped = true;
        agent.ResetPath();
    }

    void EngageMovement(float distance)
    {
        if (agent == null || !agent.isOnNavMesh) return;
        agent.isStopped = false;

        if (distance > preferredRange * 1.15f)
        {
            // Predictive pursuit: intercept where the player is heading, not where they were.
            Vector3 lead = player.position + Vector3.ClampMagnitude(playerVelocity, 12f) * 0.35f;
            agent.speed = moveSpeed;
            agent.stoppingDistance = preferredRange * 0.8f;
            if (Time.time >= nextPathUpdateTime)
            {
                agent.SetDestination(SnapToNavMesh(lead, 4f));
                nextPathUpdateTime = Time.time + 0.18f;
            }
        }
        else if (distance < preferredRange * 0.6f && archetype != EnemyArchetype.Rusher)
        {
            // Back off to the preferred range instead of crowding the player.
            Vector3 away = transform.position - player.position;
            away.y = 0f;
            agent.speed = moveSpeed * 0.8f;
            if (Time.time >= nextPathUpdateTime)
            {
                agent.SetDestination(SnapToNavMesh(transform.position + away.normalized * 5f, 6f));
                nextPathUpdateTime = Time.time + 0.3f;
            }
        }
        else
        {
            // Continuous orbit: small, frequent course corrections keep the enemy circling
            // smoothly instead of stopping and restarting at a random point every second.
            if (Time.time >= nextStrafeTime)
            {
                nextStrafeTime = Time.time + 0.45f;
                Vector3 radial = transform.position - player.position;
                radial.y = 0f;
                if (radial.sqrMagnitude < 0.01f) radial = transform.forward;
                radial.Normalize();
                Vector3 tangent = Vector3.Cross(radial, Vector3.up) * orbitDirection;
                Vector3 desired = player.position + (radial + tangent * 0.4f).normalized * preferredRange;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(desired, out hit, 5f, NavMesh.AllAreas) &&
                    !IsInsideOrNearContainer(hit.position))
                {
                    agent.speed = moveSpeed * 0.75f;
                    agent.SetDestination(hit.position);
                }
                else
                {
                    orbitDirection = -orbitDirection; // blocked -> circle the other way
                }
            }
        }

        ApplySeparation();
    }

    void ApplySeparation()
    {
        if (agent == null || !agent.isOnNavMesh || agent.velocity.sqrMagnitude < 0.1f) return;
        Vector3 push = Vector3.zero;
        for (int i = 0; i < activeEnemies.Count; i++)
        {
            EnemyAI other = activeEnemies[i];
            if (other == null || other == this || other.isDead) continue;
            Vector3 d = transform.position - other.transform.position;
            d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq > 0.01f && sq < 9f)
            {
                float dist = Mathf.Sqrt(sq);
                push += d / dist * (3f - dist);
            }
        }
        if (push.sqrMagnitude > 0.01f)
            agent.Move(Vector3.ClampMagnitude(push, 0.6f) * Time.deltaTime);
    }

    void SearchAround()
    {
        if (Time.time < nextPathUpdateTime) return;
        nextPathUpdateTime = Time.time + Random.Range(1.2f, 2.2f);
        Vector3 p = lastKnownPlayerPosition + Random.insideUnitSphere * 6f;
        p.y = 0f;
        MoveTo(p, moveSpeed * 0.6f, 1.5f, 999f);
    }

    Vector3 SnapToNavMesh(Vector3 point, float radius)
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(point, out hit, radius, NavMesh.AllAreas)) return hit.position;
        return transform.position;
    }

    void FacePosition(Vector3 target, float speed)
    {
        Vector3 dir = target - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;
        Quaternion wanted = Quaternion.LookRotation(dir.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, wanted, Time.deltaTime * Mathf.Max(speed, 1f));
    }

    void FaceMovementOrTarget(Vector3 fallback)
    {
        if (agent != null && agent.hasPath && agent.velocity.sqrMagnitude > 0.2f)
            FacePosition(transform.position + agent.velocity, 8f);
        else
            FacePosition(fallback, 8f);
    }

    void UpdateAnimation()
    {
        float speed = 0f;
        if (agent != null && agent.isOnNavMesh)
            speed = Mathf.Clamp01(agent.velocity.magnitude / Mathf.Max(moveSpeed, 1f));
        SafeSetAnimFloat(speedParameter, speed, 0.15f);
    }

    // ------------------------------------------------------------------
    // COMBAT
    // ------------------------------------------------------------------

    void TryFire()
    {
        if (Time.time < spawnTime + 0.3f) return;
        if (isReloadingBurst && Time.time < burstReloadTime) return;

        if (isReloadingBurst)
        {
            isReloadingBurst = false;
            burstShotsFired = 0;
        }

        float actualFireDelay = 1f / Mathf.Max(fireRate, 0.5f);
        if (Time.time < lastFireTime + actualFireDelay) return;

        if (archetype == EnemyArchetype.Grenadier && Time.time >= nextGrenadeTime && Random.value < 0.35f)
        {
            ThrowGrenade();
            lastFireTime = Time.time;
            nextGrenadeTime = Time.time + Random.Range(5f, 8f);
            return;
        }

        Shoot();
        lastFireTime = Time.time;
        burstShotsFired++;
        if (burstShotsFired >= Random.Range(4, 7))
        {
            isReloadingBurst = true;
            burstReloadTime = Time.time + Random.Range(0.8f, 1.3f);
        }
    }

    void Shoot()
    {
        if (player == null) return;
        if (ContainerObstacleManager.IsPointInsideAnyContainer(transform.position, 0.2f))
        {
            EnsureOutsideContainer();
            return;
        }
        EnsureFirePoint();
        SafeSetAnimTrigger(shootTrigger);
        if (shootSound != null) AudioSource.PlayClipAtPoint(shootSound, transform.position);

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position + Vector3.up * 1.4f + transform.forward * 0.5f;
        Quaternion spawnRot = firePoint != null ? firePoint.rotation : transform.rotation;

        if (muzzleFlash != null)
        {
            GameObject flash = Instantiate(muzzleFlash, spawnPos, spawnRot);
            Destroy(flash, 0.05f);
        }

        Vector3 target = player.position + Vector3.up * 1.0f;

        // Predictive aim: lead the player by a fraction of the bullet flight time.
        float flight = Vector3.Distance(spawnPos, target) / Mathf.Max(bulletSpeed, 1f);
        target += playerVelocity * Mathf.Clamp(flight, 0f, 0.35f) * 0.7f;

        Vector3 baseDir = (target - spawnPos).normalized;
        float spread = archetype == EnemyArchetype.Sniper ? 0.015f :
                       archetype == EnemyArchetype.Rusher ? 0.08f :
                       archetype == EnemyArchetype.Tank ? 0.065f : 0.045f;
        if (playerVelocity.magnitude > 1.2f) spread += 0.02f;

        // View-consistent spread (right/up of the shot direction, not world axes).
        Vector3 right = Vector3.Cross(baseDir, Vector3.up).normalized;
        if (right.sqrMagnitude < 0.01f) right = transform.right;
        Vector3 up = Vector3.Cross(right, baseDir).normalized;
        Vector3 fireDirection = (baseDir + right * Random.Range(-spread, spread) +
                                 up * Random.Range(-spread, spread)).normalized;

        if (bulletPrefab != null)
        {
            GameObject bullet = Instantiate(bulletPrefab, spawnPos, Quaternion.LookRotation(fireDirection));
            bullet.SetActive(true);
            Rigidbody rb = bullet.GetComponent<Rigidbody>();
            if (rb != null) rb.linearVelocity = fireDirection * bulletSpeed;
            BulletScript bs = bullet.GetComponent<BulletScript>();
            if (bs != null)
            {
                bs.damage = 0f; // damage is applied by the synchronized raycast below
                bs.isEnemyBullet = true;
                bs.owner = gameObject;
            }
        }

        float maxDist = attackRange + attackHysteresis + 3f;
        RaycastHit[] hits = Physics.RaycastAll(spawnPos, fireDirection, maxDist, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit h in hits)
        {
            if (h.transform == transform || h.transform.IsChildOf(transform) || h.transform.root == transform) continue;
            if (h.transform.GetComponentInParent<EnemyAI>() != null) continue;
            if (h.collider.isTrigger && !h.transform.CompareTag("Player")) continue;
            if (h.transform.name.Contains("Bullet")) continue;

            if (h.transform == player || h.transform.IsChildOf(player) || h.transform.root == player ||
                h.transform.CompareTag("Player") || h.transform.GetComponentInParent<PlayerHealth>() != null)
            {
                PlayerHealth ph = player.GetComponent<PlayerHealth>();
                if (ph == null) ph = player.GetComponentInParent<PlayerHealth>();
                if (ph == null) ph = FindFirstObjectByType<PlayerHealth>();
                if (ph != null) ph.TakeDamage(damage, transform.position);
            }
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
            if (enemyGrenadePrefab.GetComponent<Rigidbody>() == null) enemyGrenadePrefab.AddComponent<Rigidbody>();
            if (enemyGrenadePrefab.GetComponent<SphereCollider>() == null) enemyGrenadePrefab.AddComponent<SphereCollider>();

            Grenade g = enemyGrenadePrefab.AddComponent<Grenade>();
            g.explosionDamage = 45f;
            g.explosionRadius = 4.5f;
            g.delay = 2.5f;
            g.hurtsEnemies = false; // no friendly fire
            g.owner = gameObject;
            enemyGrenadePrefab.SetActive(false);
        }

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position + Vector3.up * 1.5f + transform.forward;
        GameObject grenade = Instantiate(enemyGrenadePrefab, spawnPos, transform.rotation);
        grenade.SetActive(true);
        Rigidbody grb = grenade.GetComponent<Rigidbody>();
        if (grb != null && player != null)
        {
            Vector3 dir = player.position - spawnPos;
            float d = dir.magnitude;
            grb.AddForce(dir.normalized * Mathf.Min(d * 0.8f, 15f) + Vector3.up * 5f, ForceMode.Impulse);
            grb.AddTorque(Random.insideUnitSphere * 10f, ForceMode.Impulse);
        }
    }

    // ------------------------------------------------------------------
    // DAMAGE / ALERTS / DEATH
    // ------------------------------------------------------------------

    public void TakeDamage(float amount)
    {
        if (isDead || amount <= 0f) return;

        if (amount >= 25f && health > amount && Time.time > flinchCooldownTime && agent != null && agent.isOnNavMesh)
        {
            flinchCooldownTime = Time.time + 3f;
            if (player != null)
            {
                Vector3 kb = (transform.position - player.position).normalized;
                kb.y = 0f;
                agent.Move(kb * 0.2f);
            }
        }

        health -= amount;
        if (player != null) lastKnownPlayerPosition = player.position;
        hasSpottedPlayer = true;
        suspicionTimer = Mathf.Max(suspicionTimer, reactionTime);
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
                ally.ReceiveAlert(player, lastKnownPlayerPosition);
        }
    }

    public void ReceiveAlert(Transform targetPlayer)
    {
        ReceiveAlert(targetPlayer, targetPlayer != null ? targetPlayer.position : transform.position);
    }

    public void ReceiveAlert(Transform targetPlayer, Vector3 position)
    {
        if (isDead || targetPlayer == null) return;
        if (player == null) player = targetPlayer;
        lastKnownPlayerPosition = position;
        suspicionTimer = Mathf.Max(suspicionTimer, reactionTime * 0.6f);
        if (state == EnemyState.Patrol) SetState(EnemyState.Alert);
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;
        isCurrentlyAttacking = false;
        SetState(EnemyState.Dead);
        activeEnemies.Remove(this);

        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
        SafeSetAnimTrigger(dieTrigger);
        if (deathEffect != null) Instantiate(deathEffect, transform.position, transform.rotation);

        if (GameManager.instance != null) GameManager.instance.EnemyDied();
        if (PlayerAbilities.instance != null) PlayerAbilities.instance.OnEnemyKilled();

        if (Random.value <= healthDropChance) DropHealthPickup();
        Destroy(gameObject, destroyDelay);
    }

    void DropHealthPickup()
    {
        GameObject pickup = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pickup.name = "HealthPickup";
        pickup.transform.position = transform.position + Vector3.up * 1.5f;
        pickup.transform.localScale = Vector3.one * 0.6f;
        var rend = pickup.GetComponent<Renderer>();
        if (rend != null) rend.material.color = Color.green;
        var rb = pickup.AddComponent<Rigidbody>();
        rb.mass = 1f;
        var sc = pickup.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 2f;
        pickup.AddComponent<HealthPickup>().healAmount = 35f;
    }

    // ------------------------------------------------------------------
    // HELPERS (unchanged public API)
    // ------------------------------------------------------------------

    public void WarpTo(Vector3 targetPos)
    {
        if (agent != null && agent.enabled)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(targetPos, out hit, 8f, NavMesh.AllAreas)) agent.Warp(hit.position);
            else agent.Warp(targetPos);
        }
        else transform.position = targetPos;
    }

    public bool IsInsideOrNearContainer(Vector3 pos)
    {
        return ContainerObstacleManager.IsPointInsideAnyContainer(pos, 0.8f);
    }

    public void EnsureOutsideContainer()
    {
        if (isDead) return;

        BoxCollider container;
        Vector3 safePos;
        if (ContainerObstacleManager.IsPointInsideAnyContainer(transform.position, out container, out safePos, 0.4f))
        {
            WarpTo(safePos);
            return;
        }

        if (player != null && agent != null && agent.isOnNavMesh)
        {
            NavMeshPath path = new NavMeshPath();
            if (!NavMesh.CalculatePath(transform.position, player.position, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete)
            {
                Vector3 toPlayer = (player.position - transform.position).normalized;
                Vector3 candidate = player.position - toPlayer * 16f;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(candidate, out hit, 10f, NavMesh.AllAreas) &&
                    !ContainerObstacleManager.IsPointInsideAnyContainer(hit.position, 1.5f))
                    WarpTo(hit.position);
            }
        }
    }

    private void FixModelHeight()
    {
        if (agent == null) return;
        foreach (Transform child in transform)
        {
            if (child.GetComponent<Animator>() != null || child.name.ToLower().Contains("mesh"))
            {
                Renderer[] renderers = child.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds b = renderers[0].bounds;
                    foreach (var r in renderers) b.Encapsulate(r.bounds);
                    if (b.min.y < transform.position.y - 0.1f)
                        agent.baseOffset += (transform.position.y - b.min.y + modelYOffset);
                    else if (modelYOffset != 0f)
                        agent.baseOffset += modelYOffset;
                }
                break;
            }
        }
    }

    private void SafeSetAnimFloat(string param, float value, float damp = 0f)
    {
        if (useLegacyAnimation && legacyAnim != null)
        {
            if (param == speedParameter)
                legacyAnim.CrossFade(value > 0.1f ? legacyRun : legacyIdle, 0.2f);
            return;
        }
        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return;
        try
        {
            if (damp > 0f) anim.SetFloat(param, value, damp, Time.deltaTime);
            else anim.SetFloat(param, value);
        }
        catch { }
    }

    private void SafeSetAnimTrigger(string param)
    {
        if (useLegacyAnimation && legacyAnim != null) return;
        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return;
        try { anim.SetTrigger(param); } catch { }
    }

#if UNITY_EDITOR
    void OnGUI()
    {
        if (!showDebugOverlay || Camera.main == null || isDead) return;
        Vector3 sp = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2.2f);
        if (sp.z <= 0f || Vector3.Distance(transform.position, Camera.main.transform.position) > 30f) return;

        float w = 120f;
        float x = sp.x - w / 2f;
        float y = Screen.height - sp.y;
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(new Rect(x, y, w, 35f), Texture2D.whiteTexture);
        GUI.color = Color.red;
        float fill = Mathf.Clamp01(health / Mathf.Max(maxHealth, 1f));
        GUI.DrawTexture(new Rect(x + 2f, y + 2f, (w - 4f) * fill, 12f), Texture2D.whiteTexture);
        GUI.color = state == EnemyState.Engage ? Color.yellow : Color.white;
        GUI.Label(new Rect(x + 4f, y + 16f, w, 18f), state.ToString());
    }
#endif
}
