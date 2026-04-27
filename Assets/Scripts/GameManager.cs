using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public int totalEnemies;
    public bool isGameOver = false;
    public bool isVictory = false;

    [Header("Wave System")]
    public int currentWave = 0;
    public int enemiesPerWaveBase = 3;
    public GameObject enemyPrefabTemplate;
    private Transform player;
    private bool waveActive = false;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        
        // Find a template enemy if not assigned
        if (enemyPrefabTemplate == null)
        {
            EnemyAI existing = Object.FindFirstObjectByType<EnemyAI>();
            if (existing != null) 
            {
                // We'll hide the template and use it to spawn others
                enemyPrefabTemplate = Instantiate(existing.gameObject);
                enemyPrefabTemplate.SetActive(false);
                enemyPrefabTemplate.name = "EnemyTemplate";
            }
        }

        // Add UIManager dynamically if not exists
        if (Object.FindFirstObjectByType<UIManager>() == null)
        {
            GameObject ui = new GameObject("UIManager");
            ui.AddComponent<UIManager>();
        }

        // Add CameraShake dynamically if not exists
        if (Camera.main != null && Camera.main.GetComponent<CameraShake>() == null)
        {
            Camera.main.gameObject.AddComponent<CameraShake>();
        }

        RefreshEnemyCount();
        StartCoroutine(WaveRoutine());
    }

    IEnumerator WaveRoutine()
    {
        // Removed endless wave system so the player can actually win
        yield break;
    }

    IEnumerator SpawnEnemies(int count)
    {
        if (enemyPrefabTemplate == null)
        {
            Debug.LogWarning("No enemy template found for Wave System. Add at least one enemy to the scene.");
            yield break;
        }

        for (int i = 0; i < count; i++)
        {
            if (isGameOver) break;

            Vector3 spawnPos = GetRandomSpawnPosition();
            GameObject newEnemy = Instantiate(enemyPrefabTemplate, spawnPos, Quaternion.identity);
            newEnemy.SetActive(true);
            newEnemy.name = "WaveEnemy_" + currentWave + "_" + i;
            
            totalEnemies++;
            yield return new WaitForSeconds(Random.Range(0.5f, 2f)); // Staggered spawn
        }
    }

    Vector3 GetRandomSpawnPosition()
    {
        if (player == null) return Vector3.zero;

        // Spawn in a circle around player, 15-25 units away
        float angle = Random.Range(0, 360) * Mathf.Deg2Rad;
        float distance = Random.Range(15f, 25f);
        Vector3 spawnPos = player.position + new Vector3(Mathf.Cos(angle) * distance, 5f, Mathf.Sin(angle) * distance);

        // Raycast down to find ground
        RaycastHit hit;
        if (Physics.Raycast(spawnPos, Vector3.down, out hit, 10f))
        {
            return hit.point + Vector3.up * 0.5f;
        }
        return spawnPos - Vector3.up * 4.5f; // Fallback
    }

    public void RefreshEnemyCount()
    {
        totalEnemies = 0;
        EnemyAI[] enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        foreach (var e in enemies)
        {
            if (e.gameObject.activeInHierarchy && e.gameObject != enemyPrefabTemplate) totalEnemies++;
        }
        Debug.Log("GameManager: Found " + totalEnemies + " active enemies.");
    }

    public void EnemyDied()
    {
        if (isGameOver) return;
        
        totalEnemies--;
        Debug.Log("Enemy Died! Remaining: " + totalEnemies);
        
        if (totalEnemies <= 0)
        {
            Victory();
        }
    }

    public void PlayerDied()
    {
        if (isGameOver) return;
        GameOver();
    }

    void Victory()
    {
        // Victory is now maybe endless, or reached at Wave X
        isGameOver = true;
        isVictory = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void GameOver()
    {
        isGameOver = true;
        isVictory = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnGUI()
    {
        if (!isGameOver)
        {
            GUIStyle waveStyle = new GUIStyle();
            waveStyle.fontSize = 30;
            waveStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(Screen.width / 2 - 50, 20, 200, 50), "WAVE " + currentWave, waveStyle);
        }

        if (isGameOver)
        {
            GUIStyle messageStyle = new GUIStyle();
            messageStyle.alignment = TextAnchor.MiddleCenter;
            messageStyle.fontSize = 70; // Made text bigger
            messageStyle.normal.textColor = isVictory ? Color.green : Color.red;
            
            string message = isVictory ? "You are win" : "You are faild";
            
            float w = 600;
            float h = 100;
            GUI.Label(new Rect(Screen.width/2 - w/2, Screen.height/2 - 150, w, h), message, messageStyle);

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 40;
            
            if (GUI.Button(new Rect(Screen.width/2 - 150, Screen.height/2 + 20, 300, 80), "try again", buttonStyle))
            {
                 SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }
    }
}
