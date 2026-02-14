using UnityEngine;
using UnityEditor;

public class GameLoopFixer : EditorWindow
{
    [MenuItem("EnemyAI/Fix Game Loop & Player Health")]
    public static void FixGameLoop()
    {
        // 1. Create GameManager
        var gm = Object.FindFirstObjectByType<GameManager>();
        if (gm == null)
        {
            GameObject go = new GameObject("GameManager");
            go.AddComponent<GameManager>();
            Debug.Log("Created GameManager.");
        }
        else
        {
            Debug.Log("GameManager already exists.");
            gm.RefreshEnemyCount();
        }

        // 2. Setup Player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var health = player.GetComponent<PlayerHealth>();
            if (health == null)
            {
                player.AddComponent<PlayerHealth>();
                Debug.Log("Added PlayerHealth to Player.");
            }
            else
            {
                Debug.Log("Player already has PlayerHealth.");
            }
            
            // Ensure Player has a collider we can hit
            if (player.GetComponent<Collider>() == null && player.GetComponentInChildren<Collider>() == null)
            {
                var cap = player.AddComponent<CapsuleCollider>();
                cap.height = 2f;
                cap.center = new Vector3(0, 1, 0);
                cap.isTrigger = false; // Ensure it's solid
                Debug.Log("Added CapsuleCollider to Player (was missing).");
            }
            
            // Double check existing collider
            var existingCap = player.GetComponent<CapsuleCollider>();
            if (existingCap != null) existingCap.isTrigger = false;
        }
        else
        {
            Debug.LogWarning("Could not find object with tag 'Player'! Please tag your player object.");
        }

        AssetDatabase.Refresh();
    }
}
