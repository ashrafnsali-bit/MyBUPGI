using UnityEngine;
using UnityEditor;
using UnityEngine.AI;

public class EnemyMovementDiagnostic : EditorWindow
{
    [MenuItem("EnemyAI/Diagnose Movement Issues")]
    public static void DiagnoseMovement()
    {
        Debug.Log("=== ENEMY MOVEMENT DIAGNOSTIC ===");
        
        // 1. Check for Player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogError("PROBLEM: No GameObject with 'Player' tag found!");
            Debug.LogError("SOLUTION: Select your player object and set Tag to 'Player' in Inspector.");
        }
        else
        {
            Debug.Log("✓ Player found: " + player.name);
        }
        
        // 2. Check Enemies
        var enemies = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        Debug.Log("Found " + enemies.Length + " enemies in scene.");
        
        foreach (var enemy in enemies)
        {
            Debug.Log("\n--- Checking Enemy: " + enemy.name + " ---");
            
            // Check NavMeshAgent
            var agent = enemy.GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                Debug.LogError("  ✗ Missing NavMeshAgent component!");
            }
            else
            {
                Debug.Log("  ✓ Has NavMeshAgent");
                Debug.Log("    - Speed: " + agent.speed);
                Debug.Log("    - Is on NavMesh: " + agent.isOnNavMesh);
                if (!agent.isOnNavMesh)
                {
                    Debug.LogWarning("  ⚠ Enemy is NOT on NavMesh! Enemy cannot move.");
                    Debug.LogWarning("    SOLUTION: Bake NavMesh (Window -> AI -> Navigation)");
                }
            }
            
            // Check sight range
            Debug.Log("  - Sight Range: " + enemy.sightRange);
            Debug.Log("  - Attack Range: " + enemy.attackRange);
            Debug.Log("  - Move Speed: " + enemy.moveSpeed);
            
            if (player != null)
            {
                float distance = Vector3.Distance(enemy.transform.position, player.transform.position);
                Debug.Log("  - Distance to Player: " + distance);
                
                if (distance > enemy.sightRange)
                {
                    Debug.LogWarning("  ⚠ Player is outside sight range (" + enemy.sightRange + ")!");
                    Debug.LogWarning("    SOLUTION: Increase sightRange or move player closer.");
                }
            }
        }
        
        Debug.Log("\n=== DIAGNOSTIC COMPLETE ===");
    }
}
