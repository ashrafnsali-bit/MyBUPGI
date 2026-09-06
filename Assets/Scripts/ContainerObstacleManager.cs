using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Automatically finds all cargo containers in the scene, adds carving NavMeshObstacles 
/// and colliders to prevent enemies from pathfinding, walking, or hiding inside them.
/// </summary>
public class ContainerObstacleManager : MonoBehaviour
{
    private static bool hasInitialized = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void AutoSetupContainersOnPlay()
    {
        SetupAllContainersInScene();
    }

    public static int SetupAllContainersInScene()
    {
        int count = 0;
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);

        foreach (GameObject go in allObjects)
        {
            if (IsContainerObject(go))
            {
                if (SetupContainerObstacle(go))
                {
                    count++;
                }
            }
        }

        Debug.Log("[ContainerObstacleManager] Successfully protected " + count + " containers from enemy entry.");
        return count;
    }

    public static bool IsContainerObject(GameObject go)
    {
        if (go == null) return false;
        string name = go.name.ToLower();
        string rootName = go.transform.root.name.ToLower();

        return name.Contains("container") || name.Contains("cargo") ||
               rootName.Contains("container") || rootName.Contains("cargo");
    }

    public static bool SetupContainerObstacle(GameObject go)
    {
        if (go == null) return false;

        // Try to find or add NavMeshObstacle
        NavMeshObstacle obstacle = go.GetComponent<NavMeshObstacle>();
        if (obstacle == null)
        {
            obstacle = go.AddComponent<NavMeshObstacle>();
        }

        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.carving = true;
        obstacle.carveOnlyStationary = false;

        // Size obstacle based on BoxCollider or Renderer bounds
        BoxCollider boxCol = go.GetComponent<BoxCollider>();
        if (boxCol != null)
        {
            obstacle.center = boxCol.center;
            // Slightly expand carving volume to prevent agents from clipping exterior walls
            obstacle.size = new Vector3(
                boxCol.size.x + 0.5f,
                Mathf.Max(boxCol.size.y, 3.2f),
                boxCol.size.z + 0.5f
            );
            boxCol.isTrigger = false; // Ensure physical barrier
        }
        else
        {
            Renderer rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                Vector3 localCenter = go.transform.InverseTransformPoint(rend.bounds.center);
                Vector3 localSize = rend.bounds.size;
                obstacle.center = localCenter;
                obstacle.size = new Vector3(localSize.x + 0.5f, Mathf.Max(localSize.y, 3.2f), localSize.z + 0.5f);

                // Add solid collider if missing
                BoxCollider addedBox = go.AddComponent<BoxCollider>();
                addedBox.center = localCenter;
                addedBox.size = localSize;
                addedBox.isTrigger = false;
            }
            else
            {
                // Default container dimensions (standard 20ft/40ft shipping container)
                obstacle.center = new Vector3(0, 1.5f, 0);
                obstacle.size = new Vector3(8.5f, 3.5f, 3.5f);
            }
        }

        // For open or through containers, ensure entrance is completely blocked for enemies
        string lowerName = go.name.ToLower();
        if (lowerName.Contains("open") || lowerName.Contains("through") || lowerName.Contains("door"))
        {
            // The carving NavMeshObstacle already completely carves out the interior cavity,
            // making it impossible for NavMeshAgents to enter.
            obstacle.carving = true;
        }

        return true;
    }

#if UNITY_EDITOR
    [MenuItem("Tools/Block Containers From Enemies")]
    public static void MenuBlockContainers()
    {
        int count = SetupAllContainersInScene();
        EditorUtility.DisplayDialog("Containers Protected", $"Successfully blocked {count} containers from enemy entry and carved NavMesh obstacles.", "OK");
    }
#endif
}
