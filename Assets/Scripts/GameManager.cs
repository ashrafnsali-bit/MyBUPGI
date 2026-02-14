using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public int totalEnemies;
    public bool isGameOver = false;
    public bool isVictory = false;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        RefreshEnemyCount();
    }

    public void RefreshEnemyCount()
    {
        // Count enemies at start and can be called to refresh
        totalEnemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None).Length;
        Debug.Log("GameManager: Found " + totalEnemies + " enemies.");
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
        isGameOver = true;
        isVictory = true;
        // Unlock cursor for menu
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void GameOver()
    {
        isGameOver = true;
        isVictory = false;
        // Unlock cursor for menu
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnGUI()
    {
        if (isGameOver)
        {
            // Message Style
            GUIStyle messageStyle = new GUIStyle();
            messageStyle.alignment = TextAnchor.MiddleCenter;
            messageStyle.fontSize = 50;
            messageStyle.normal.textColor = isVictory ? Color.green : Color.red;
            
            string message = isVictory ? "YOU WIN!" : "YOU FAILED";
            
            float w = 600;
            float h = 100;
            GUI.Label(new Rect(Screen.width/2 - w/2, Screen.height/2 - 100, w, h), message, messageStyle);

            // Button Style
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 30;
            
            if (GUI.Button(new Rect(Screen.width/2 - 150, Screen.height/2 + 20, 300, 80), "TRY AGAIN", buttonStyle))
            {
                 SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }
    }
}
