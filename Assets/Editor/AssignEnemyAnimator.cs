using UnityEngine;
using UnityEditor;

public class AssignEnemyAnimator : EditorWindow
{
    [MenuItem("EnemyAI/Assign Animator Controller")]
    public static void AssignAnimator()
    {
        // 1. Load the controller
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/EnemyAnimator.controller");
        
        if (controller == null)
        {
            Debug.LogError("EnemyAnimator.controller not found! Run 'EnemyAI -> Fix Enemy Animator' first.");
            return;
        }
        
        Debug.Log("Found EnemyAnimator.controller");
        
        // 2. Find all enemies in scene
        var enemies = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        int assignedCount = 0;
        
        foreach (var enemy in enemies)
        {
            // Find animator in children (the actual character model)
            var animator = enemy.GetComponentInChildren<Animator>();
            
            if (animator == null)
            {
                Debug.LogWarning("Enemy " + enemy.name + " has no Animator component!");
                continue;
            }
            
            // Assign controller
            animator.runtimeAnimatorController = controller;
            Debug.Log("✓ Assigned animator to: " + enemy.name + " -> " + animator.name);
            assignedCount++;
            
            // Mark as dirty so Unity saves the change
            EditorUtility.SetDirty(animator);
        }
        
        Debug.Log("=== COMPLETE: Assigned animator to " + assignedCount + " enemies ===");
        
        if (assignedCount == 0)
        {
            Debug.LogWarning("No enemies found in scene! Make sure you have EnemyAI objects in your scene.");
        }
    }
}
