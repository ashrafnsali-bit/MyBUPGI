using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// ContainerObstacleManager ensures that cargo/shipping containers are 100% impenetrable
/// to enemies. It carves NavMesh obstacles, creates physical and trigger barriers,
/// and instantly ejects any enemy that touches, enters, or spawns inside containers.
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
        // Continuous failsafe: periodic sweep to guarantee no enemy can remain inside a container
        if (Time.time >= nextSweepTime)
        {
            nextSweepTime = Time.time + 0.5f;
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

        Debug.Log($"[ContainerObstacleManager] Successfully registered and protected {count} containers.");
        return count;
    }

    public static bool IsContainerObject(GameObject go)
    {
        if (go == null) return false;
        // Don't treat trigger helpers as containers
        if (go.name.Contains("_ContainerEjectorTrigger")) return false;

        string name = go.name.ToLower();
        string rootName = go.transform.root.name.ToLower();

        return name.Contains("container") || name.Contains("cargo") ||
               rootName.Contains("container") || rootName.Contains("cargo");
    }

    public static BoxCollider SetupContainerObstacle(GameObject go)
    {
        if (go == null) return null;

        // Ensure solid BoxCollider
        BoxCollider boxCol = go.GetComponent<BoxCollider>();
        if (boxCol == null)
        {
            Renderer rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                boxCol = go.AddComponent<BoxCollider>();
                boxCol.center = go.transform.InverseTransformPoint(rend.bounds.center);
                boxCol.size = rend.bounds.size;
            }
            else
            {
                boxCol = go.AddComponent<BoxCollider>();
                boxCol.center = new Vector3(0, 1.5f, 0);
                boxCol.size = new Vector3(8f, 3f, 3f);
            }
        }

        boxCol.isTrigger = false; // Solid physical blocker

        // Setup carving NavMeshObstacle
        NavMeshObstacle obstacle = go.GetComponent<NavMeshObstacle>();
        if (obstacle == null)
        {
            obstacle = go.AddComponent<NavMeshObstacle>();
        }

        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = boxCol.center;
        // Expand carving volume by 1.2m laterally and in height to completely carve out interior & edges
        obstacle.size = new Vector3(
            boxCol.size.x + 1.2f,
            Mathf.Max(boxCol.size.y + 1.0f, 4.0f),
            boxCol.size.z + 1.2f
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
        triggerCol.size = boxCol.size + new Vector3(0.6f, 0.6f, 0.6f);

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

        return Mathf.Abs(localPoint.x) <= (halfSize.x + padding) &&
               Mathf.Abs(localPoint.y) <= (halfSize.y + padding) &&
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
    /// Computes a safe outside position on the NavMesh clear of the container.
    /// </summary>
    public static Vector3 GetSafePositionOutside(BoxCollider box, Vector3 worldPos)
    {
        if (box == null) return worldPos;

        Vector3 localPoint = box.transform.InverseTransformPoint(worldPos) - box.center;
        Vector3 halfSize = box.size * 0.5f;

        // Try lateral exit (sides of container)
        float exitX = (localPoint.x >= 0 ? 1 : -1) * (halfSize.x + 2.5f);
        Vector3 candLocalX = box.center + new Vector3(exitX, localPoint.y, localPoint.z);
        Vector3 candWorldX = box.transform.TransformPoint(candLocalX);
        candWorldX.y = worldPos.y;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(candWorldX, out hit, 8f, NavMesh.AllAreas))
        {
            return hit.position;
        }

        // Try longitudinal exit (ends of container)
        float exitZ = (localPoint.z >= 0 ? 1 : -1) * (halfSize.z + 2.5f);
        Vector3 candLocalZ = box.center + new Vector3(localPoint.x, localPoint.y, exitZ);
        Vector3 candWorldZ = box.transform.TransformPoint(candLocalZ);
        candWorldZ.y = worldPos.y;

        if (NavMesh.SamplePosition(candWorldZ, out hit, 8f, NavMesh.AllAreas))
        {
            return hit.position;
        }

        // Radial fallback: test 12 directions around container
        for (int angle = 0; angle < 360; angle += 30)
        {
            Vector3 radialDir = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            Vector3 sampleTarget = box.transform.TransformPoint(box.center) + radialDir * (Mathf.Max(halfSize.x, halfSize.z) + 3.0f);
            sampleTarget.y = worldPos.y;
            if (NavMesh.SamplePosition(sampleTarget, out hit, 10f, NavMesh.AllAreas))
            {
                return hit.position;
            }
        }

        return candWorldX;
    }

    /// <summary>
    /// Finds all enemies in the scene and warps any enemy trapped inside a container to safety.
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
                Debug.LogWarning($"[ContainerObstacleManager] Ejected {enemy.gameObject.name} from inside {container.gameObject.name} to safe position {safePos}!");
            }
        }

        return count;
    }

#if UNITY_EDITOR
    [MenuItem("Tools/Block Containers & Eject Enemies")]
    public static void MenuBlockContainers()
    {
        int containerCount = SetupAllContainersInScene();
        int ejectedCount = EjectAllEnemiesFromAllContainers();
        EditorUtility.DisplayDialog(
            "Containers Protected",
            $"Configured {containerCount} containers with carving NavMeshObstacles and physical barriers.\nEjected {ejectedCount} enemies to safe ground outside containers.",
            "OK"
        );
    }
#endif
}

/// <summary>
/// Trigger helper attached to containers to physically catch and instantly eject
/// any enemy that enters or touches the container volume.
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
            Debug.LogWarning($"[ContainerEjectionTrigger] Instantly ejected {enemy.gameObject.name} out of container trigger to {safe}!");
        }
    }
}
