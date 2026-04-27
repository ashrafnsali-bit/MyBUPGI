using UnityEngine;
using UnityEditor;

public class AddEnemiesEditor
{
    [MenuItem("Tools/Add 2 Enemies")]
    public static void AddEnemies()
    {
        // Try to find the specific Vanguard prefab
        string[] guids = AssetDatabase.FindAssets("Vanguard By T. Choonyung Variant t:Prefab");
        if (guids.Length == 0)
        {
            // Fallback search in case the name changed slightly
            guids = AssetDatabase.FindAssets("Vanguard t:Prefab");
            if (guids.Length == 0)
            {
                Debug.LogError("Could not find any enemy prefab containing 'Vanguard'.");
                return;
            }
        }

        string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
        GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

        if (enemyPrefab != null)
        {
            GameObject player = GameObject.Find("Player");
            if (player == null) player = GameObject.FindGameObjectWithTag("Player");

            Vector3 spawnPos = player != null ? player.transform.position + player.transform.forward * 8f + Vector3.up * 0.5f : new Vector3(0, 1, 0);

            GameObject enemy1 = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab);
            enemy1.transform.position = spawnPos + Vector3.right * 2f;
            enemy1.transform.LookAt(player != null ? player.transform.position : spawnPos - Vector3.forward);
            enemy1.name = "Enemy_1";

            GameObject enemy2 = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab);
            enemy2.transform.position = spawnPos + Vector3.left * 2f;
            enemy2.transform.LookAt(player != null ? player.transform.position : spawnPos - Vector3.forward);
            enemy2.name = "Enemy_2";

            Undo.RegisterCreatedObjectUndo(enemy1, "Add Enemy 1");
            Undo.RegisterCreatedObjectUndo(enemy2, "Add Enemy 2");

            Debug.Log("Successfully added 2 enemies to the scene.");
        }
        else
        {
            Debug.LogError("Failed to load the enemy prefab.");
        }
    }
}
