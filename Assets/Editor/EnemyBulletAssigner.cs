using UnityEngine;
using UnityEditor;

public class EnemyBulletAssigner : EditorWindow
{
    [MenuItem("EnemyAI/Assign Bullet Prefab")]
    public static void AssignBullet()
    {
        string bulletPath = "Assets/Easy FPS/Prefabs/Bullet.prefab";
        GameObject bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(bulletPath);

        if (bulletPrefab == null)
        {
            Debug.LogError("Could not find Bullet prefab at: " + bulletPath);
            return;
        }

        var enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        int count = 0;
        foreach (var enemy in enemies)
        {
            enemy.bulletPrefab = bulletPrefab;
            EditorUtility.SetDirty(enemy);
            count++;
        }
        Debug.Log("Assigned Bullet Prefab to " + count + " enemies.");
    }
}
