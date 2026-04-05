using UnityEngine;
using UnityEditor;
using UnityEngine.AI;

public class EnemyFixer : EditorWindow
{
    [MenuItem("Tools/Fix All Enemies")]
    public static void FixEnemies()
    {
        int fixedCount = 0;
        // Find all objects tagged "Enemy" or "Dummie"
        GameObject[] allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        
        foreach (GameObject go in allObjects)
        {
            if (go.CompareTag("Enemy") || go.CompareTag("Dummie"))
            {
                // Check if it's the root or has EnemyAI
                EnemyAI ai = go.GetComponentInParent<EnemyAI>();
                if (ai == null)
                {
                    // Add EnemyAI to the root or the object itself
                    ai = go.AddComponent<EnemyAI>();
                    fixedCount++;
                    Debug.Log("Added EnemyAI to: " + go.name);
                }

                // Check if it has a collider for bullets to hit
                Collider col = go.GetComponentInChildren<Collider>();
                if (col == null)
                {
                    // Dummies and standard enemies usually need a CapsuleCollider
                    CapsuleCollider cap = go.AddComponent<CapsuleCollider>();
                    
                    // Default humanoid bounds
                    cap.height = 2f;
                    cap.radius = 0.5f;
                    cap.center = new Vector3(0, 1f, 0);
                    cap.isTrigger = false; // Must be solid to block raycasts/spherecasts

                    fixedCount++;
                    Debug.Log("Added CapsuleCollider to: " + go.name);
                }
                else if (col.isTrigger)
                {
                    // If it only has a trigger, bullets will pass through
                    col.isTrigger = false;
                    fixedCount++;
                    Debug.Log("Fixed Collider (was Trigger) on: " + go.name);
                }
            }
        }
        
        EditorUtility.DisplayDialog("Enemy Fixer", $"Fixed {fixedCount} enemies/dummies in the scene.", "OK");
    }
}
