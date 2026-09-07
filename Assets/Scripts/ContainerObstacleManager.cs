using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// ContainerObstacleManager ensures that cargo containers, oil tanks, silos, and industrial
/// containers are 100% impenetrable to enemies. It carves NavMesh obstacles, creates physical
/// barriers and triggers, prevents enemies from spawning inside, and immediately ejects any
/// enemy that enters, touches, or spawns inside them.
/// </summary>
public class ContainerObstacleManager : MonoBehaviour
{
    private static ContainerObstacleManager instance;
    public static readonly List<BoxCollider> ContainerBoxes = new List<BoxCollider>();
    private static bool isInitialized = false;

    private float nextSweepTime = 0f;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Initialize();
    }

    void Start()
    {
        Initialize();
        EjectAllEnemiesFromAllContainers();
    }

    void Update()
    {
        // Continuous failsafe: sweep every 0.25s to guarantee no enemy can stay inside a container or tank
        if (Time.time >= nextSweepTime)
        {
            nextSweepTime = Time.time + 0.25f;
            EjectAllEnemiesFromAllContainers();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void AutoSetupContainersOnPlay()
    {
        EnsureManagerExists();
        Initialize();
        EjectAllEnemiesFromAllContainers();
    }

    public static void EnsureManagerExists()
    {
        if (instance == null)
        {
            GameObject go = GameObject.Find("[ContainerObstacleManager]");
            if (go == null)
            {
                go = new GameObject("[ContainerObstacleManager]");
                DontDestroyOnLoad(go);
            }
            instance = go.GetComponent<ContainerObstacleManager>();
            if (instance == null)
            {
                instance = go.AddComponent<ContainerObstacleManager>();
            }
        }
    }

    public static void Initialize()
    {
        SetupAllContainersInScene();
        isInitialized = true;
    }

    public static void EnsureContainersCached()
    {
        if (!isInitialized || ContainerBoxes.Count == 0)
        {
            SetupAllContainersInScene();
            isInitialized = true;
        }
        else
        {
            // Remove any destroyed references
            ContainerBoxes.RemoveAll(b => b == null);
        }
    }

    public static int SetupAllContainersInScene()
    {
        ContainerBoxes.Clear();
        int count = 0;

        // Search all transforms including inactive
        Transform[] allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Transform t in allTransforms)
        {
            if (t == null) continue;
            GameObject go = t.gameObject;
            if (IsContainerObject(go))
            {
                BoxCollider box = SetupContainerObstacle(go);
                if (box != null)
                {
                    if (!ContainerBoxes.Contains(box))
                    {
                        ContainerBoxes.Add(box);
                    }
                    count++;
                }
            }
        }

        Debug.Log($"[ContainerObstacleManager] Successfully registered and protected {count} containers and tanks.");
        return count;
    }

    public static bool IsContainerObject(GameObject go)
    {
        if (go == null) return false;
        // Don't treat trigger helpers as containers
        if (go.name.Contains("_ContainerEjectorTrigger")) return false;

        // Strictly ignore UI objects, Canvases, and RectTransforms
        if (go.GetComponent<RectTransform>() != null || 
            go.GetComponent<CanvasRenderer>() != null || 
            go.GetComponentInParent<Canvas>() != null)
        {
            return false;
        }

        // Strictly ignore Player, Weapons, Bullets, Enemies, Cameras
        if (go.CompareTag("Player") || go.GetComponentInParent<PlayerHealth>() != null ||
            go.GetComponentInParent<EnemyAI>() != null || go.GetComponent<EnemyAI>() != null ||
            go.GetComponent<Camera>() != null)
        {
            return false;
        }

        string name = go.name.ToLower();
        string rootName = go.transform.root.name.ToLower();

        // Strictly ignore UI / HUD
        if (name.Contains("joystick") || name.Contains("ui") || name.Contains("hud") || 
            name.Contains("canvas") || name.Contains("panel") || name.Contains("button"))
        {
            return false;
        }

        // Must be a 3D cargo / shipping container
        bool isCargo = name.Contains("cargo_container") || 
                       (name.Contains("cargo") && name.Contains("container")) ||
                       rootName.Contains("cargo_container") ||
                       (rootName.Contains("cargo") && rootName.Contains("container"));

        // Must be an industrial oil tank / silo / storage tank / cistern
        bool isOilTank = name.Contains("oil_tank") || name.Contains("oiltank") ||
                         rootName.Contains("oil_tank") || rootName.Contains("oiltank") ||
                         name.StartsWith("oil_tank") || rootName.StartsWith("oil_tank") ||
                         (name.Contains("tank") && !name.Contains("archetype")) ||
                         name.Contains("silo") || name.Contains("cistern");

        // Must be an industrial dumpster / waste container
        bool isDumpster = name.Contains("dumpster") || rootName.Contains("dumpster");

        // Must be an industrial hangar / shed / warehouse / building
        bool isHangar = name.Contains("hangar") || rootName.Contains("hangar") ||
                        name.Contains("shed") || rootName.Contains("shed") ||
                        name.Contains("warehouse") || rootName.Contains("warehouse") ||
                        name.Contains("building") || rootName.Contains("building");

        if (!isCargo && !isOilTank && !isDumpster && !isHangar) return false;

        // Must have a 3D mesh or existing renderer
        return go.GetComponent<MeshFilter>() != null || go.GetComponent<Renderer>() != null;
    }

    public static BoxCollider SetupContainerObstacle(GameObject go)
    {
        if (go == null) return null;

        // Ensure solid BoxCollider
        BoxCollider boxCol = go.GetComponent<BoxCollider>();
        if (boxCol == null)
        {
            boxCol = go.AddComponent<BoxCollider>();
        }

        MeshFilter mf = go.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            boxCol.center = mf.sharedMesh.bounds.center;
            boxCol.size = mf.sharedMesh.bounds.size;
        }
        else
        {
            Renderer rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                Vector3 ls = go.transform.lossyScale;
                boxCol.center = go.transform.InverseTransformPoint(rend.bounds.center);
                boxCol.size = new Vector3(
                    ls.x != 0 ? rend.bounds.size.x / Mathf.Abs(ls.x) : rend.bounds.size.x,
                    ls.y != 0 ? rend.bounds.size.y / Mathf.Abs(ls.y) : rend.bounds.size.y,
                    ls.z != 0 ? rend.bounds.size.z / Mathf.Abs(ls.z) : rend.bounds.size.z
                );
            }
            else
            {
                return null;
            }
        }

        boxCol.isTrigger = false; // Solid physical blocker

        // Setup carving NavMeshObstacle to completely remove interior NavMesh
        NavMeshObstacle obstacle = go.GetComponent<NavMeshObstacle>();
        if (obstacle == null)
        {
            obstacle = go.AddComponent<NavMeshObstacle>();
        }

        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = boxCol.center;
        // Expand carving volume by 1.6m laterally and full height to completely carve out interior & edges
        obstacle.size = new Vector3(
            boxCol.size.x + 1.6f,
            Mathf.Max(boxCol.size.y + 1.5f, 6.0f),
            boxCol.size.z + 1.6f
        );
        obstacle.carving = true;
        obstacle.carveOnlyStationary = false;

        // Add trigger helper to instantly catch any enemies entering or inside
        Transform existingTrigger = go.transform.Find("_ContainerEjectorTrigger");
        GameObject triggerObj = existingTrigger != null ? existingTrigger.gameObject : null;
        if (triggerObj == null)
        {
            triggerObj = new GameObject("_ContainerEjectorTrigger");
            triggerObj.transform.SetParent(go.transform, false);
            triggerObj.transform.localPosition = Vector3.zero;
            triggerObj.transform.localRotation = Quaternion.identity;
            triggerObj.transform.localScale = Vector3.one;
        }

        BoxCollider triggerCol = triggerObj.GetComponent<BoxCollider>();
        if (triggerCol == null) triggerCol = triggerObj.AddComponent<BoxCollider>();
        triggerCol.isTrigger = true;
        triggerCol.center = boxCol.center;
        triggerCol.size = boxCol.size + new Vector3(1.0f, 1.0f, 1.0f);

        ContainerEjectionTrigger ejector = triggerObj.GetComponent<ContainerEjectionTrigger>();
        if (ejector == null) ejector = triggerObj.AddComponent<ContainerEjectionTrigger>();
        ejector.parentContainerBox = boxCol;

        return boxCol;
    }

    /// <summary>
    /// Exact mathematical containment check using BoxCollider local space.
    /// Works for any rotation, position, scale, and cavity.
    /// </summary>
    public static bool IsInsideBoxCollider(BoxCollider box, Vector3 worldPos, float padding = 0.5f)
    {
        if (box == null || !box.enabled) return false;

        Vector3 localPoint = box.transform.InverseTransformPoint(worldPos) - box.center;
        Vector3 halfSize = box.size * 0.5f;

        // Height check with tolerance
        if (Mathf.Abs(localPoint.y) > (halfSize.y + padding + 1.0f))
            return false;

        return Mathf.Abs(localPoint.x) <= (halfSize.x + padding) &&
               Mathf.Abs(localPoint.z) <= (halfSize.z + padding);
    }

    public static bool IsPointInsideAnyContainer(Vector3 worldPos, float buffer = 0.5f)
    {
        BoxCollider hit;
        Vector3 safe;
        return IsPointInsideAnyContainer(worldPos, out hit, out safe, buffer);
    }

    public static bool IsPointInsideAnyContainer(Vector3 worldPos, out BoxCollider hitContainer, out Vector3 safePos, float buffer = 0.5f)
    {
        hitContainer = null;
        safePos = worldPos;
        EnsureContainersCached();

        for (int i = 0; i < ContainerBoxes.Count; i++)
        {
            BoxCollider b = ContainerBoxes[i];
            if (b == null) continue;

            if (IsInsideBoxCollider(b, worldPos, buffer))
            {
                hitContainer = b;
                safePos = GetSafePositionOutside(b, worldPos);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Computes a safe outside position on the NavMesh clear of the container or tank.
    /// </summary>
    public static Vector3 GetSafePositionOutside(BoxCollider box, Vector3 worldPos)
    {
        if (box == null) return worldPos;

        Vector3 halfSize = box.size * 0.5f;
        Vector3 worldCenter = box.transform.TransformPoint(box.center);

        // Vector pointing outward from center towards current world position
        Vector3 outward = worldPos - worldCenter;
        outward.y = 0;
        if (outward.sqrMagnitude < 0.1f)
        {
            outward = box.transform.forward;
        }
        outward.Normalize();

        float maxRadius = Mathf.Max(
            Mathf.Abs(halfSize.x * box.transform.lossyScale.x),
            Mathf.Abs(halfSize.z * box.transform.lossyScale.z)
        );

        // 1. Try directly outward along heading (+4.0m clearance)
        Vector3 cand1 = worldCenter + outward * (maxRadius + 4.0f);
        cand1.y = worldPos.y;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(cand1, out hit, 10f, NavMesh.AllAreas))
        {
            if (!IsInsideBoxCollider(box, hit.position, 0.4f))
            {
                return hit.position;
            }
        }

        // 2. Radial sweep around obstacle in 16 directions with generous clearance
        for (int angle = 0; angle < 360; angle += 22)
        {
            Vector3 radialDir = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            Vector3 cand = worldCenter + radialDir * (maxRadius + 4.5f);
            cand.y = worldPos.y;
            if (NavMesh.SamplePosition(cand, out hit, 10f, NavMesh.AllAreas))
            {
                if (!IsInsideBoxCollider(box, hit.position, 0.4f))
                {
                    return hit.position;
                }
            }
        }

        // 3. Fallback: warp towards player on open ground
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            Vector3 toPlayer = (playerObj.transform.position - worldCenter);
            toPlayer.y = 0;
            if (toPlayer.sqrMagnitude > 1f)
            {
                Vector3 candPlayer = worldCenter + toPlayer.normalized * (maxRadius + 4.5f);
                candPlayer.y = playerObj.transform.position.y;
                if (NavMesh.SamplePosition(candPlayer, out hit, 12f, NavMesh.AllAreas))
                {
                    return hit.position;
                }
                return candPlayer;
            }
        }

        return worldCenter + outward * (maxRadius + 4.5f);
    }

    /// <summary>
    /// Finds all enemies in the scene and warps any enemy trapped inside a container or tank to safety.
    /// </summary>
    public static int EjectAllEnemiesFromAllContainers()
    {
        EnsureContainersCached();
        EnemyAI[] enemies = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        int count = 0;

        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.IsDead) continue;

            BoxCollider container;
            Vector3 safePos;
            if (IsPointInsideAnyContainer(enemy.transform.position, out container, out safePos, 0.4f))
            {
                enemy.WarpTo(safePos);
                count++;
                Debug.LogWarning($"[ContainerObstacleManager] Ejected {enemy.gameObject.name} from inside {(container != null ? container.gameObject.name : "container/tank")} to safe position {safePos}!");
            }
        }

        return count;
    }

#if UNITY_EDITOR
    [MenuItem("Tools/Block Containers & Tanks & Eject Enemies")]
    public static void MenuBlockContainers()
    {
        int containerCount = SetupAllContainersInScene();
        int ejectedCount = EjectAllEnemiesFromAllContainers();
        EditorUtility.DisplayDialog(
            "Containers & Tanks Protected",
            $"Configured {containerCount} containers & oil tanks with carving NavMeshObstacles and physical barriers.\nEjected {ejectedCount} enemies to safe ground outside.",
            "OK"
        );
    }
#endif
}

/// <summary>
/// Trigger helper attached to containers and oil tanks to physically catch and instantly eject
/// any enemy that enters or touches the volume.
/// </summary>
public class ContainerEjectionTrigger : MonoBehaviour
{
    public BoxCollider parentContainerBox;

    void OnTriggerStay(Collider other)
    {
        if (other == null) return;
        EnemyAI enemy = other.GetComponentInParent<EnemyAI>();
        if (enemy != null && !enemy.IsDead)
        {
            BoxCollider box = parentContainerBox;
            if (box == null) box = GetComponentInParent<BoxCollider>();

            Vector3 safe = ContainerObstacleManager.GetSafePositionOutside(box, enemy.transform.position);
            enemy.WarpTo(safe);
            Debug.LogWarning($"[ContainerEjectionTrigger] Instantly ejected {enemy.gameObject.name} out of container/tank trigger to {safe}!");
        }
    }
}
