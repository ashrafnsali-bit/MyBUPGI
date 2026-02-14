using UnityEngine;
using UnityEditor;

public class EnemyVisualsFixer : EditorWindow
{
    [MenuItem("EnemyAI/Fix Visuals (Weapon & Bullet)")]
    public static void FixVisuals()
    {
        // 1. Load Prefabs directly
        GameObject gunPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Easy FPS/Resources/NewGun_auto.prefab");
        GameObject bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Easy FPS/Prefabs/Bullet.prefab");
        // Correct path found by find_by_name: Assets/Easy FPS/MuzzelFlash/MuzzlePrefabs/muzzelFlash 01.prefab
        GameObject flashPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Easy FPS/MuzzelFlash/MuzzlePrefabs/muzzelFlash 01.prefab");

        if (gunPrefab == null) Debug.LogError("Could not find Gun prefab!");
        if (bulletPrefab == null) Debug.LogError("Could not find Bullet prefab!");
        if (flashPrefab == null) Debug.LogError("Could not find Muzzle Flash prefab!");

        // 2. Find Enemies
        var enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        int fixedCount = 0;

        foreach (var enemy in enemies)
        {
            // A. Assign Bullet
            if (bulletPrefab != null)
            {
                enemy.bulletPrefab = bulletPrefab;
                Debug.Log("Assigned Bullet to " + enemy.name);
            }

            // B. Fix Weapon Setup
            var weaponSetup = enemy.GetComponent<EnemyWeaponSetup>();
            if (weaponSetup == null) weaponSetup = enemy.gameObject.AddComponent<EnemyWeaponSetup>();
            
            if (gunPrefab != null)
            {
                weaponSetup.weaponPrefab = gunPrefab;
                Debug.Log("Assigned Gun to " + enemy.name);
            }
            
            // C. Assign Muzzle Flash
            if (flashPrefab != null)
            {
                enemy.muzzleFlash = flashPrefab;
                Debug.Log("Assigned Muzzle Flash to " + enemy.name);
            }
            
            EditorUtility.SetDirty(enemy);
            EditorUtility.SetDirty(weaponSetup);
            fixedCount++;
        }

        
        Debug.Log("Fixed visuals for " + fixedCount + " enemies.");
    }
}
