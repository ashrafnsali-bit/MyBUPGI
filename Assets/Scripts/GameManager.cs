using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public int totalEnemies;
    public bool isGameOver = false;

    [Header("Level System")]
    public int currentLevel = 1;
    public int totalKills = 0; // Added to track overall score/kills
    public int enemiesPerWaveBase = 3;
    public int extraEnemiesPerLevel = 2; // How many more enemies to add each level
    public GameObject enemyPrefabTemplate;
    private Transform player;
    private bool levelTransitioning = false;

    // Modern UI Elements
    private GameObject gameCanvasObj;
    private Text waveText;
    private GameObject gameOverPanel;
    private Text gameOverText;
    private Button tryAgainButton;
    private CanvasGroup gameOverCanvasGroup;
    
    // Level Announcement UI
    private Text announcementText;
    private CanvasGroup announcementCanvasGroup;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        
        // Safely duplicate the template if it's a scene object that could be killed
        if (enemyPrefabTemplate != null && enemyPrefabTemplate.activeInHierarchy)
        {
            GameObject safeCopy = Instantiate(enemyPrefabTemplate);
            safeCopy.SetActive(false);
            safeCopy.name = "EnemyTemplate_SafeCopy";
            enemyPrefabTemplate = safeCopy;
        }
        else if (enemyPrefabTemplate == null)
        {
            EnemyAI existing = Object.FindFirstObjectByType<EnemyAI>();
            if (existing != null) 
            {
                enemyPrefabTemplate = Instantiate(existing.gameObject);
                enemyPrefabTemplate.SetActive(false);
                enemyPrefabTemplate.name = "EnemyTemplate_SafeCopy";
            }
        }

        // Add UIManager dynamically if not exists
        if (Object.FindFirstObjectByType<UIManager>() == null)
        {
            GameObject ui = new GameObject("UIManager");
            ui.AddComponent<UIManager>();
        }

        // Ensure EventSystem exists for UI interactions (Buttons)
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // Add CameraShake dynamically if not exists
        if (Camera.main != null && Camera.main.GetComponent<CameraShake>() == null)
        {
            Camera.main.gameObject.AddComponent<CameraShake>();
        }

        SetupGameUI();
        RefreshEnemyCount();
        
        // If there are no enemies in the scene, start level 1 immediately
        if (totalEnemies == 0)
        {
            StartCoroutine(SpawnEnemies(enemiesPerWaveBase));
        }
        else
        {
            ShowAnnouncement("LEVEL " + currentLevel + " START", 3f);
        }
    }

    void SetupGameUI()
    {
        gameCanvasObj = new GameObject("GameManagerCanvas");
        Canvas canvas = gameCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20; // Higher than UIManager
        CanvasScaler scaler = gameCanvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        gameCanvasObj.AddComponent<GraphicRaycaster>();

        // Top HUD Background Panel
        GameObject topPanelObj = new GameObject("TopHUDPanel");
        topPanelObj.transform.SetParent(gameCanvasObj.transform, false);
        Image topPanelImg = topPanelObj.AddComponent<Image>();
        topPanelImg.color = new Color(0.05f, 0.05f, 0.05f, 0.85f); // Sleek dark background
        RectTransform topRt = topPanelObj.GetComponent<RectTransform>();
        topRt.anchorMin = new Vector2(0, 1);   // Top-Left
        topRt.anchorMax = new Vector2(0, 1);   // Top-Left
        topRt.pivot = new Vector2(0, 1);       // Pivot at Top-Left
        topRt.anchoredPosition = new Vector2(20, -20); // 20px margin from top-left corner
        topRt.sizeDelta = new Vector2(400, 100); // Slightly smaller to look neat on the side

        // Golden Accent Line at the bottom of the panel
        GameObject accentObj = new GameObject("AccentLine");
        accentObj.transform.SetParent(topPanelObj.transform, false);
        Image accentImg = accentObj.AddComponent<Image>();
        accentImg.color = new Color(1f, 0.75f, 0f, 1f); // Vibrant Gold
        RectTransform accRt = accentObj.GetComponent<RectTransform>();
        accRt.anchorMin = new Vector2(0, 0);
        accRt.anchorMax = new Vector2(1, 0);
        accRt.pivot = new Vector2(0.5f, 0);
        accRt.offsetMin = new Vector2(0, 0);
        accRt.offsetMax = new Vector2(0, 4); // 4px thick line

        // Wave Text
        GameObject waveObj = new GameObject("WaveText");
        waveObj.transform.SetParent(topPanelObj.transform, false);
        waveText = waveObj.AddComponent<Text>();
        waveText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        waveText.fontSize = 45;
        waveText.fontStyle = FontStyle.Bold;
        waveText.color = Color.white;
        waveText.alignment = TextAnchor.MiddleCenter;
        waveText.horizontalOverflow = HorizontalWrapMode.Overflow;
        waveText.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform wrt = waveText.GetComponent<RectTransform>();
        wrt.anchorMin = new Vector2(0, 0);
        wrt.anchorMax = new Vector2(1, 1);
        wrt.pivot = new Vector2(0.5f, 0.5f);
        wrt.offsetMin = new Vector2(10, 10);
        wrt.offsetMax = new Vector2(-10, -10);
        Outline wOut = waveObj.AddComponent<Outline>();
        wOut.effectColor = new Color(0, 0, 0, 1f);
        wOut.effectDistance = new Vector2(2, -2);

        // Announcement Text
        GameObject annObj = new GameObject("AnnouncementText");
        annObj.transform.SetParent(gameCanvasObj.transform, false);
        announcementText = annObj.AddComponent<Text>();
        announcementText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        announcementText.fontSize = 80;
        announcementText.fontStyle = FontStyle.Bold;
        announcementText.color = Color.yellow;
        announcementText.alignment = TextAnchor.MiddleCenter;
        RectTransform annRt = announcementText.GetComponent<RectTransform>();
        annRt.anchorMin = new Vector2(0.5f, 0.5f);
        annRt.anchorMax = new Vector2(0.5f, 0.5f);
        annRt.anchoredPosition = new Vector2(0, 200);
        annRt.sizeDelta = new Vector2(1200, 200);
        Outline annOut = annObj.AddComponent<Outline>();
        annOut.effectColor = Color.black;
        annOut.effectDistance = new Vector2(2, -2);
        
        announcementCanvasGroup = annObj.AddComponent<CanvasGroup>();
        announcementCanvasGroup.alpha = 0f;

        // Game Over Panel
        gameOverPanel = new GameObject("GameOverPanel");
        gameOverPanel.transform.SetParent(gameCanvasObj.transform, false);
        Image panelImg = gameOverPanel.AddComponent<Image>();
        panelImg.color = new Color(0, 0, 0, 0.85f); // Dark background
        RectTransform prt = gameOverPanel.GetComponent<RectTransform>();
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.offsetMin = Vector2.zero;
        prt.offsetMax = Vector2.zero;
        
        gameOverCanvasGroup = gameOverPanel.AddComponent<CanvasGroup>();
        gameOverCanvasGroup.alpha = 0f; // Hidden initially
        gameOverPanel.SetActive(false);

        // Game Over Text
        GameObject goTextObj = new GameObject("GameOverText");
        goTextObj.transform.SetParent(gameOverPanel.transform, false);
        gameOverText = goTextObj.AddComponent<Text>();
        gameOverText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        gameOverText.fontSize = 100;
        gameOverText.fontStyle = FontStyle.Bold;
        gameOverText.alignment = TextAnchor.MiddleCenter;
        RectTransform gort = gameOverText.GetComponent<RectTransform>();
        gort.anchorMin = new Vector2(0.5f, 0.5f);
        gort.anchorMax = new Vector2(0.5f, 0.5f);
        gort.anchoredPosition = new Vector2(0, 100);
        gort.sizeDelta = new Vector2(800, 200);
        Outline goOut = goTextObj.AddComponent<Outline>();
        goOut.effectColor = Color.black;
        goOut.effectDistance = new Vector2(3, -3);

        // Try Again Button
        GameObject btnObj = new GameObject("TryAgainButton");
        btnObj.transform.SetParent(gameOverPanel.transform, false);
        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        tryAgainButton = btnObj.AddComponent<Button>();
        tryAgainButton.onClick.AddListener(() => {
            Time.timeScale = 1f; // Reset time scale!
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        });
        RectTransform brt = btnObj.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.5f, 0.5f);
        brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.anchoredPosition = new Vector2(0, -100);
        brt.sizeDelta = new Vector2(400, 100);
        
        GameObject btnTextObj = new GameObject("Text");
        btnTextObj.transform.SetParent(btnObj.transform, false);
        Text btnText = btnTextObj.AddComponent<Text>();
        btnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        btnText.fontSize = 50;
        btnText.color = Color.white;
        btnText.text = "TRY AGAIN";
        btnText.alignment = TextAnchor.MiddleCenter;
        RectTransform btrt = btnText.GetComponent<RectTransform>();
        btrt.anchorMin = Vector2.zero;
        btrt.anchorMax = Vector2.one;
        btrt.offsetMin = Vector2.zero;
        btrt.offsetMax = Vector2.zero;

        // Quit Game Button
        GameObject quitObj = new GameObject("QuitButton");
        quitObj.transform.SetParent(gameOverPanel.transform, false);
        Image quitImg = quitObj.AddComponent<Image>();
        quitImg.color = new Color(0.8f, 0.2f, 0.2f, 1f); // Red color
        Button quitButton = quitObj.AddComponent<Button>();
        quitButton.onClick.AddListener(() => {
            Time.timeScale = 1f;
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        });
        RectTransform qrt = quitObj.GetComponent<RectTransform>();
        qrt.anchorMin = new Vector2(0.5f, 0.5f);
        qrt.anchorMax = new Vector2(0.5f, 0.5f);
        qrt.anchoredPosition = new Vector2(0, -220); // Positioned below Try Again
        qrt.sizeDelta = new Vector2(400, 100);

        GameObject quitTextObj = new GameObject("Text");
        quitTextObj.transform.SetParent(quitObj.transform, false);
        Text quitText = quitTextObj.AddComponent<Text>();
        quitText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        quitText.fontSize = 50;
        quitText.color = Color.white;
        quitText.text = "QUIT GAME";
        quitText.alignment = TextAnchor.MiddleCenter;
        RectTransform qtrt = quitText.GetComponent<RectTransform>();
        qtrt.anchorMin = Vector2.zero;
        qtrt.anchorMax = Vector2.one;
        qtrt.offsetMin = Vector2.zero;
        qtrt.offsetMax = Vector2.zero;
    }

    private float checkTimer = 1f;

    void Update()
    {
        if (waveText != null && !isGameOver)
        {
            waveText.text = "LEVEL " + currentLevel + "\n<size=20>Enemies: " + totalEnemies + " | KILLS: " + totalKills + "</size>";
        }

        // Failsafe to ensure level transitions even if counter gets out of sync
        if (!isGameOver && !levelTransitioning)
        {
            checkTimer -= Time.deltaTime;
            if (checkTimer <= 0)
            {
                checkTimer = 1f;
                int actualCount = 0;
                EnemyAI[] enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
                foreach (var e in enemies)
                {
                    if (e.gameObject.activeInHierarchy && !e.name.Contains("SafeCopy") && e.gameObject != enemyPrefabTemplate && !e.GetComponent<EnemyAI>().health.Equals(0))
                    {
                        actualCount++;
                    }
                }
                
                if (actualCount == 0 && totalEnemies > 0)
                {
                    Debug.LogWarning("GameManager: Failsafe triggered! Actual enemies is 0 but totalEnemies was " + totalEnemies);
                    totalEnemies = 0;
                    StartCoroutine(LevelCompleteRoutine());
                }
            }
        }

        if (isGameOver && gameOverPanel != null)
        {
            if (!gameOverPanel.activeSelf)
            {
                gameOverPanel.SetActive(true);
                waveText.gameObject.SetActive(false);
                announcementText.gameObject.SetActive(false);
                
                gameOverText.text = "GAME OVER\n<size=40>Total Kills: " + totalKills + "</size>";
                gameOverText.color = new Color(1f, 0.2f, 0.2f); // Bright Red
            }
            
            // Fade in the game over panel smoothly (use unscaledDeltaTime because timeScale is 0)
            if (gameOverCanvasGroup.alpha < 1f)
            {
                gameOverCanvasGroup.alpha += Time.unscaledDeltaTime * 2f;
            }
        }
    }

    public void ShowAnnouncement(string message, float duration)
    {
        StartCoroutine(AnnouncementRoutine(message, duration));
    }

    IEnumerator AnnouncementRoutine(string message, float duration)
    {
        announcementText.text = message;
        
        // Fade in
        while (announcementCanvasGroup.alpha < 1f)
        {
            announcementCanvasGroup.alpha += Time.deltaTime * 3f;
            yield return null;
        }
        
        yield return new WaitForSeconds(duration);
        
        // Fade out
        while (announcementCanvasGroup.alpha > 0f)
        {
            announcementCanvasGroup.alpha -= Time.deltaTime * 2f;
            yield return null;
        }
    }

    IEnumerator LevelCompleteRoutine()
    {
        levelTransitioning = true;
        
        // Announce completion
        ShowAnnouncement("LEVEL " + currentLevel + " COMPLETED!", 3f);
        yield return new WaitForSeconds(4f);
        
        // Increment Level
        currentLevel++;
        int enemiesToSpawn = enemiesPerWaveBase + ((currentLevel - 1) * extraEnemiesPerLevel);
        
        // Announce next level
        ShowAnnouncement("STARTING LEVEL " + currentLevel, 2f);
        yield return new WaitForSeconds(2.5f);
        
        // Start spawning
        StartCoroutine(SpawnEnemies(enemiesToSpawn));
        levelTransitioning = false;
    }

    IEnumerator SpawnEnemies(int count)
    {
        if (enemyPrefabTemplate == null)
        {
            Debug.LogWarning("No enemy template found for Level System. Add at least one enemy to the scene.");
            yield break;
        }

        for (int i = 0; i < count; i++)
        {
            if (isGameOver) break;

            Vector3 spawnPos = GetRandomSpawnPosition();
            GameObject newEnemy = Instantiate(enemyPrefabTemplate, spawnPos, Quaternion.identity);
            newEnemy.SetActive(true);
            newEnemy.name = "LevelEnemy_" + currentLevel + "_" + i;
            
            // Randomize Archetype based on level progression
            EnemyAI ai = newEnemy.GetComponent<EnemyAI>();
            if (ai != null)
            {
                float rand = Random.value;
                if (currentLevel >= 3)
                {
                    if (rand < 0.15f) ai.archetype = EnemyArchetype.Tank;
                    else if (rand < 0.35f) ai.archetype = EnemyArchetype.Grenadier;
                    else if (rand < 0.55f) ai.archetype = EnemyArchetype.Rusher;
                    else if (rand < 0.75f) ai.archetype = EnemyArchetype.Sniper;
                    else ai.archetype = EnemyArchetype.Assaulter;
                }
                else if (currentLevel >= 2)
                {
                    if (rand < 0.2f) ai.archetype = EnemyArchetype.Rusher;
                    else if (rand < 0.4f) ai.archetype = EnemyArchetype.Sniper;
                    else ai.archetype = EnemyArchetype.Assaulter;
                }
                else
                {
                    ai.archetype = EnemyArchetype.Assaulter; // Level 1 is basic enemies only
                }
            }
            
            totalEnemies++;
            yield return new WaitForSeconds(Random.Range(0.5f, 1.5f)); // Staggered spawn
        }
    }

    Vector3 GetRandomSpawnPosition()
    {
        if (player == null) return Vector3.zero;

        // Try to find a valid open spawn point between 18m and 35m from player
        for (int i = 0; i < 30; i++)
        {
            float angle = Random.Range(0, 360) * Mathf.Deg2Rad;
            float distance = Random.Range(18f, 35f);
            Vector3 candidatePos = player.position + new Vector3(Mathf.Cos(angle) * distance, 0, Mathf.Sin(angle) * distance);

            UnityEngine.AI.NavMeshHit navHit;
            if (UnityEngine.AI.NavMesh.SamplePosition(candidatePos, out navHit, 6.0f, UnityEngine.AI.NavMesh.AllAreas))
            {
                if (Vector3.Distance(navHit.position, player.position) >= 15f)
                {
                    // Ensure the spawn point is NOT inside or near any container
                    if (!IsInsideOrNearContainer(navHit.position))
                    {
                        return navHit.position;
                    }
                }
            }
        }

        // Fallback: spawn at a safe 22m distance in a random direction away from containers
        for (int i = 0; i < 10; i++)
        {
            float fallbackAngle = Random.Range(0, 360) * Mathf.Deg2Rad;
            Vector3 fallbackPos = player.position + new Vector3(Mathf.Cos(fallbackAngle) * 22f, 0.5f, Mathf.Sin(fallbackAngle) * 22f);
            if (!IsInsideOrNearContainer(fallbackPos))
            {
                return fallbackPos;
            }
        }

        return player.position + player.forward * 20f + Vector3.up * 0.5f;
    }

    public static bool IsInsideOrNearContainer(Vector3 pos)
    {
        return ContainerObstacleManager.IsPointInsideAnyContainer(pos, 1.2f);
    }

    public void RefreshEnemyCount()
    {
        totalEnemies = 0;
        EnemyAI[] enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        foreach (var e in enemies)
        {
            // Do not count the template OR the safe copy
            if (e.gameObject.activeInHierarchy && e.gameObject != enemyPrefabTemplate && !e.name.Contains("SafeCopy")) 
            {
                totalEnemies++;
            }
        }
        Debug.Log("GameManager: Found " + totalEnemies + " active enemies.");
    }

    public void EnemyDied()
    {
        if (isGameOver) return;
        
        totalEnemies--;
        totalKills++; // Increment score
        if (totalEnemies < 0) totalEnemies = 0; // Prevent negative numbers
        
        Debug.Log("Enemy Died! Remaining: " + totalEnemies);
        
        if (totalEnemies == 0 && !levelTransitioning)
        {
            StartCoroutine(LevelCompleteRoutine());
        }
    }

    public void PlayerDied()
    {
        if (isGameOver) return;
        isGameOver = true;
        
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            if (gameOverCanvasGroup != null)
            {
                gameOverCanvasGroup.alpha = 1f;
                gameOverCanvasGroup.interactable = true;
                gameOverCanvasGroup.blocksRaycasts = true;
            }
            if (waveText != null) waveText.gameObject.SetActive(false);
            if (announcementText != null) announcementText.gameObject.SetActive(false);
            if (gameOverText != null)
            {
                gameOverText.text = "YOU DIED\n<size=40>Total Kills: " + totalKills + "</size>";
                gameOverText.color = new Color(1f, 0.2f, 0.2f);
            }
        }
        
        // Stop the game world
        Time.timeScale = 0f;
    }
}
