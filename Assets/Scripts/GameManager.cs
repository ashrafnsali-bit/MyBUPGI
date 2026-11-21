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
    public int score = 0;
    public int comboCount = 0;

    [Header("Records")]
    public int bestKills = 0;
    public int bestScore = 0;

    [Header("Combo")]
    public float comboWindow = 3f;
    private float lastKillTime = -999f;

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
    private Button shareButton;
    private CanvasGroup gameOverCanvasGroup;
    
    // Level Announcement UI
    private Text announcementText;
    private CanvasGroup announcementCanvasGroup;
    private Coroutine announcementRoutine;

    void Awake()
    {
        instance = this;
        bestKills = PlayerPrefs.GetInt("MyBUPGI_BestKills", 0);
        bestScore = PlayerPrefs.GetInt("MyBUPGI_BestScore", 0);

        // Resume at the level after the last one the player cleared.
        currentLevel = Mathf.Clamp(LevelDirector.LastClearedLevel + 1, 1, 999);

        // Procedural level director + surprise & gifts layer (self-contained).
        if (Object.FindFirstObjectByType<LevelDirector>() == null)
        {
            GameObject director = new GameObject("LevelDirector");
            director.AddComponent<LevelDirector>();
        }

        // Surprise & gifts layer (self-contained; no scene setup required).
        if (Object.FindFirstObjectByType<GameFeatures>() == null)
        {
            GameObject features = new GameObject("GameFeatures");
            features.AddComponent<GameFeatures>();
        }
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
        
        // Build a fresh plan for this level (different style/roster than the last one).
        LevelPlan plan = LevelDirector.instance != null ? LevelDirector.instance.BuildPlan(currentLevel) : null;
        int initialEnemies = plan != null ? plan.enemyCount : enemiesPerWaveBase;

        string startMessage = currentLevel > 1
            ? ArabicUI.Format(ArabicStrings.ResumeLevel, currentLevel)
            : (plan != null
                ? ArabicUI.Format(ArabicStrings.LevelStartStyled, currentLevel, plan.StyleName)
                : ArabicUI.Format(ArabicStrings.AnnounceLevelStart, currentLevel));

        if (totalEnemies == 0) StartCoroutine(SpawnEnemies(initialEnemies));
        ShowAnnouncement(startMessage, 3f);
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
        topRt.sizeDelta = new Vector2(900, 100); // Wide enough for all four HUD stats (no overflow off-screen)

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
        ArabicUI.Apply(waveText, true);
        waveText.fontSize = 28;
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
        ArabicUI.Apply(announcementText, true);
        announcementText.fontSize = 64;
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
        ArabicUI.Apply(gameOverText, true);
        gameOverText.fontSize = 76;
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
        ArabicUI.Apply(btnText, true);
        btnText.fontSize = 46;
        btnText.color = Color.white;
        btnText.text = ArabicStrings.TryAgain;
        btnText.alignment = TextAnchor.MiddleCenter;
        RectTransform btrt = btnText.GetComponent<RectTransform>();
        btrt.anchorMin = Vector2.zero;
        btrt.anchorMax = Vector2.one;
        btrt.offsetMin = Vector2.zero;
        btrt.offsetMax = Vector2.zero;

        // Share Result Button (Application.Quit does nothing in WebGL)
        GameObject shareObj = new GameObject("ShareButton");
        shareObj.transform.SetParent(gameOverPanel.transform, false);
        Image shareImg = shareObj.AddComponent<Image>();
        shareImg.color = new Color(0.16f, 0.55f, 0.85f, 1f);
        shareButton = shareObj.AddComponent<Button>();
        shareButton.onClick.AddListener(ShareResult);
        RectTransform srt = shareObj.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0.5f, 0.5f);
        srt.anchorMax = new Vector2(0.5f, 0.5f);
        srt.anchoredPosition = new Vector2(0, -220);
        srt.sizeDelta = new Vector2(400, 100);

        GameObject shareTextObj = new GameObject("Text");
        shareTextObj.transform.SetParent(shareObj.transform, false);
        Text shareText = shareTextObj.AddComponent<Text>();
        ArabicUI.Apply(shareText, true);
        shareText.fontSize = 46;
        shareText.color = Color.white;
        shareText.text = ArabicStrings.Share;
        shareText.alignment = TextAnchor.MiddleCenter;
        RectTransform strt = shareText.GetComponent<RectTransform>();
        strt.anchorMin = Vector2.zero;
        strt.anchorMax = Vector2.one;
        strt.offsetMin = Vector2.zero;
        strt.offsetMax = Vector2.zero;
    }

    private float checkTimer = 1f;

    void Update()
    {
        if (waveText != null && !isGameOver)
        {
            waveText.text = ArabicUI.Format(ArabicStrings.HudTemplate, currentLevel, totalEnemies, totalKills, score);
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
                
                gameOverText.text = BuildGameOverText();
                gameOverText.color = new Color(1f, 0.35f, 0.25f);
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
        if (announcementText == null || announcementCanvasGroup == null) return;
        // Avoid two banners fighting over the same CanvasGroup (e.g. a reward on a kill
        // that happens while the next-level banner is still fading).
        if (announcementRoutine != null) StopCoroutine(announcementRoutine);
        announcementRoutine = StartCoroutine(AnnouncementRoutine(message, duration));
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
        announcementRoutine = null;
    }

    IEnumerator LevelCompleteRoutine()
    {
        levelTransitioning = true;
        
        // Announce completion + the reward earned for clearing this level.
        string rewardMessage = GrantLevelCompleteReward(currentLevel);
        ShowAnnouncement(ArabicUI.Format(ArabicStrings.AnnounceLevelComplete, currentLevel) + "\n" + rewardMessage, 3.5f);

        // Persist progress: a reload always resumes at the level after this one.
        LevelDirector.SaveClearedLevel(currentLevel);
        yield return new WaitForSeconds(4f);

        // Increment Level and generate a brand-new plan for it.
        currentLevel++;
        if (GameFeatures.instance != null)
            GameFeatures.instance.CheckAchievements(totalKills, comboCount, currentLevel);

        LevelPlan nextPlan = LevelDirector.instance != null ? LevelDirector.instance.BuildPlan(currentLevel) : null;
        int enemiesToSpawn = nextPlan != null
            ? nextPlan.enemyCount
            : enemiesPerWaveBase + ((currentLevel - 1) * extraEnemiesPerLevel);

        // Announce next level and its style so the variation is visible.
        string nextMessage = nextPlan != null
            ? ArabicUI.Format(ArabicStrings.LevelStartStyled, currentLevel, nextPlan.StyleName)
            : ArabicUI.Format(ArabicStrings.AnnounceNextLevel, currentLevel);
        ShowAnnouncement(nextMessage, 2f);
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

        LevelPlan plan = LevelDirector.instance != null ? LevelDirector.instance.current : null;
        int elitesSpawned = 0;
        int eliteLimit = plan != null ? Mathf.Max(1, plan.level / 4) : 1;
        int spawned = 0;

        while (spawned < count && !isGameOver)
        {
            // Higher levels arrive in bursts instead of a slow single-file trickle.
            int burst = plan != null ? Mathf.Min(plan.burstSize, count - spawned) : 1;
            for (int b = 0; b < burst && spawned < count; b++, spawned++)
            {
                Vector3 spawnPos = plan != null
                    ? GetRandomSpawnPosition(plan.pattern, plan.minDistance, plan.maxDistance)
                    : GetRandomSpawnPosition();

                GameObject newEnemy = Instantiate(enemyPrefabTemplate, spawnPos, Quaternion.identity);
                newEnemy.SetActive(true);
                newEnemy.name = "LevelEnemy_" + currentLevel + "_" + spawned;

                EnemyAI ai = newEnemy.GetComponent<EnemyAI>();
                if (ai != null)
                {
                    if (plan != null)
                    {
                        ai.archetype = plan.ArchetypeAt(spawned);
                        ai.isElite = false;
                        ai.healthScale = plan.healthScale;
                        ai.damageScale = plan.damageScale;
                        ai.speedScale = plan.speedScale;
                        ai.fireRateScale = plan.fireRateScale;
                        if (elitesSpawned < eliteLimit && Random.value < plan.eliteChance)
                        {
                            ai.isElite = true;
                            elitesSpawned++;
                        }
                        ai.ApplyArchetype();
                    }
                    else
                    {
                        // Legacy fallback when no director is present.
                        ai.archetype = EnemyArchetype.Assaulter;
                    }
                }

                totalEnemies++;
            }

            float waitMin = plan != null ? plan.delayMin : 0.5f;
            float waitMax = plan != null ? plan.delayMax : 1.5f;
            yield return new WaitForSeconds(Random.Range(waitMin, waitMax));
        }
    }

    // Called by surprise events: an extra wave without waiting for the next level.
    public void SpawnBonusEnemies(int count)
    {
        if (!isGameOver) StartCoroutine(SpawnEnemies(count));
    }

    // Called by surprise events: a golden, much tougher enemy worth bonus score.
    public void SpawnEliteEnemy()
    {
        if (isGameOver || enemyPrefabTemplate == null) return;

        Vector3 spawnPos = GetRandomSpawnPosition();
        GameObject elite = Instantiate(enemyPrefabTemplate, spawnPos, Quaternion.identity);
        elite.SetActive(true);
        elite.name = "EliteEnemy_" + currentLevel;

        EnemyAI ai = elite.GetComponent<EnemyAI>();
        if (ai != null)
        {
            LevelPlan plan = LevelDirector.instance != null ? LevelDirector.instance.current : null;
            ai.isElite = true;
            ai.archetype = plan != null ? plan.RandomArchetype() : EnemyArchetype.Tank;
            ai.healthScale = plan != null ? plan.healthScale : 1f;
            ai.damageScale = plan != null ? plan.damageScale : 1f;
            ai.speedScale = plan != null ? plan.speedScale : 1f;
            ai.fireRateScale = plan != null ? plan.fireRateScale : 1f;
            ai.ApplyArchetype();
        }

        elite.transform.localScale = Vector3.one * 1.25f;
        foreach (Renderer r in elite.GetComponentsInChildren<Renderer>())
        {
            if (r != null) r.material.color = new Color(1f, 0.78f, 0.15f);
        }

        totalEnemies++;
    }

    Vector3 GetRandomSpawnPosition()
    {
        return GetRandomSpawnPosition(SpawnPattern.Ring, 18f, 35f);
    }

    /// <summary>Spawn point chosen by the level plan: ring, flank, behind or long range.</summary>
    Vector3 GetRandomSpawnPosition(SpawnPattern pattern, float minDist, float maxDist)
    {
        if (player == null) return Vector3.zero;

        minDist = Mathf.Max(6f, minDist);
        maxDist = Mathf.Max(minDist + 2f, maxDist);
        float facing = player.eulerAngles.y;

        UnityEngine.AI.NavMeshPath testPath = new UnityEngine.AI.NavMeshPath();
        for (int i = 0; i < 40; i++)
        {
            float angle = PickSpawnAngle(pattern, facing) * Mathf.Deg2Rad;
            float distance = pattern == SpawnPattern.Far ? maxDist : Random.Range(minDist, maxDist);
            Vector3 candidatePos = player.position + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);

            UnityEngine.AI.NavMeshHit navHit;
            if (UnityEngine.AI.NavMesh.SamplePosition(candidatePos, out navHit, 6.0f, UnityEngine.AI.NavMesh.AllAreas))
            {
                if (Vector3.Distance(navHit.position, player.position) >= minDist - 3f)
                {
                    if (!IsInsideOrNearContainer(navHit.position))
                    {
                        if (UnityEngine.AI.NavMesh.CalculatePath(navHit.position, player.position, UnityEngine.AI.NavMesh.AllAreas, testPath) &&
                            testPath.status == UnityEngine.AI.NavMeshPathStatus.PathComplete)
                        {
                            return navHit.position;
                        }
                    }
                }
            }
        }

        // Fallback: keep the pattern direction but relax the distance constraint.
        for (int i = 0; i < 20; i++)
        {
            float angle = PickSpawnAngle(pattern, facing) * Mathf.Deg2Rad;
            float distance = pattern == SpawnPattern.Far ? maxDist : Mathf.Clamp(22f, minDist, maxDist);
            Vector3 fallbackPos = player.position + new Vector3(Mathf.Cos(angle) * distance, 0.5f, Mathf.Sin(angle) * distance);
            UnityEngine.AI.NavMeshHit navHit;
            if (UnityEngine.AI.NavMesh.SamplePosition(fallbackPos, out navHit, 8.0f, UnityEngine.AI.NavMesh.AllAreas))
            {
                if (!IsInsideOrNearContainer(navHit.position))
                {
                    if (UnityEngine.AI.NavMesh.CalculatePath(navHit.position, player.position, UnityEngine.AI.NavMesh.AllAreas, testPath) &&
                        testPath.status == UnityEngine.AI.NavMeshPathStatus.PathComplete)
                    {
                        return navHit.position;
                    }
                }
            }
        }

        Vector3 finalPos = player.position + player.forward * 20f + Vector3.up * 0.5f;
        BoxCollider hitBox;
        Vector3 safePos;
        if (ContainerObstacleManager.IsPointInsideAnyContainer(finalPos, out hitBox, out safePos, 2.0f))
        {
            return safePos;
        }
        return finalPos;
    }

    static float PickSpawnAngle(SpawnPattern pattern, float facingDegrees)
    {
        switch (pattern)
        {
            case SpawnPattern.Flank:
                return facingDegrees + (Random.value < 0.5f ? 90f : 270f) + Random.Range(-35f, 35f);
            case SpawnPattern.Behind:
                return facingDegrees + 180f + Random.Range(-50f, 50f);
            default:
                return Random.Range(0f, 360f);
        }
    }

    public static bool IsInsideOrNearContainer(Vector3 pos)
    {
        return ContainerObstacleManager.IsPointInsideAnyContainer(pos, 2.5f);
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

    public void EnemyDied(bool elite = false)
    {
        if (isGameOver) return;

        totalEnemies--;
        totalKills++;
        if (totalEnemies < 0) totalEnemies = 0;

        // Every 10 kills grants a rotating reward (health -> shield -> grenades + score).
        const int killsPerReward = 10;
        if (totalKills > 0 && totalKills % killsPerReward == 0)
            GrantKillMilestoneReward(totalKills / killsPerReward);

        // Combo kills stack, and a surprise "double points" event can stack on top.
        float now = Time.realtimeSinceStartup;
        comboCount = (now - lastKillTime <= comboWindow) ? comboCount + 1 : 1;
        lastKillTime = now;
        int multiplier = Mathf.Clamp(comboCount, 1, 5);
        if (GameFeatures.instance != null) multiplier = Mathf.RoundToInt(multiplier * GameFeatures.instance.ScoreMultiplier);
        score += 100 * multiplier;

        if (elite)
        {
            const int eliteBonus = 1000;
            score += eliteBonus;
            ShowAnnouncement(ArabicUI.Format(ArabicStrings.EliteKilled, eliteBonus), 2.6f);
        }

        if (GameFeatures.instance != null)
        {
            GameFeatures.instance.NotifyEnemyKilled();
            GameFeatures.instance.CheckAchievements(totalKills, comboCount, currentLevel);
        }

        if (UIManager.instance != null)
        {
            if (comboCount == 2) UIManager.instance.ShowKillMessage(ArabicStrings.ComboDouble);
            else if (comboCount == 3) UIManager.instance.ShowKillMessage(ArabicStrings.ComboTriple);
            else if (comboCount > 3) UIManager.instance.ShowKillMessage(ArabicUI.Format(ArabicStrings.ComboTemplate, multiplier));
            if (comboCount > 1) UIManager.instance.ShowCombo(multiplier);
        }

        Debug.Log("Enemy Died! Remaining: " + totalEnemies + " | Combo x" + multiplier);

        if (totalEnemies == 0 && !levelTransitioning)
        {
            StartCoroutine(FinalKillRoutine());
        }
    }

    IEnumerator FinalKillRoutine()
    {
        levelTransitioning = true;
        float previousScale = Time.timeScale;
        Time.timeScale = 0.35f;
        yield return new WaitForSecondsRealtime(0.35f);
        Time.timeScale = previousScale;
        levelTransitioning = false;
        StartCoroutine(LevelCompleteRoutine());
    }

    GameObject GetPlayerObject()
    {
        if (player != null) return player.gameObject;
        return GameObject.FindWithTag("Player");
    }

    // Reward for a kill milestone. Rotates so the player keeps getting useful help.
    void GrantKillMilestoneReward(int tier)
    {
        if (isGameOver) return;
        GameObject po = GetPlayerObject();
        if (po == null) return;

        string message;
        switch (tier % 3)
        {
            case 1:
            {
                PlayerHealth hp = po.GetComponent<PlayerHealth>();
                if (hp != null) hp.Heal(25f);
                message = ArabicUI.Format(ArabicStrings.RewardHealth, 25);
                break;
            }
            case 2:
            {
                PlayerAbilities ab = po.GetComponent<PlayerAbilities>();
                if (ab != null) ab.currentShield = Mathf.Min(ab.maxShield, ab.currentShield + 100f);
                message = ArabicUI.Format(ArabicStrings.RewardShield, 100);
                break;
            }
            default:
            {
                PlayerGrenades gr = po.GetComponent<PlayerGrenades>();
                if (gr != null) gr.AddGrenades(2);
                score += 500;
                message = ArabicUI.Format(ArabicStrings.RewardGrenadesScore, 2, 500);
                break;
            }
        }
        ShowAnnouncement(message, 2.2f);
    }

    // Reward for clearing a level: heal, full shield, a grenade and a score bonus.
    // Returns the shaped Arabic message so the completion banner can show it inline.
    string GrantLevelCompleteReward(int completedLevel)
    {
        int bonus = 500 * completedLevel;
        score += bonus;

        GameObject po = GetPlayerObject();
        if (po != null)
        {
            PlayerHealth hp = po.GetComponent<PlayerHealth>();
            if (hp != null) hp.Heal(30f);

            PlayerAbilities ab = po.GetComponent<PlayerAbilities>();
            if (ab != null) ab.currentShield = ab.maxShield;

            PlayerGrenades gr = po.GetComponent<PlayerGrenades>();
            if (gr != null) gr.AddGrenades(1);
        }

        return ArabicUI.Format(ArabicStrings.RewardLevelComplete, completedLevel, bonus);
    }

    bool IsNewRecord()
    {
        return totalKills > bestKills || score > bestScore;
    }

    void SaveRecords()
    {
        if (totalKills > bestKills)
        {
            bestKills = totalKills;
            PlayerPrefs.SetInt("MyBUPGI_BestKills", bestKills);
        }
        if (score > bestScore)
        {
            bestScore = score;
            PlayerPrefs.SetInt("MyBUPGI_BestScore", bestScore);
        }
        PlayerPrefs.Save();
    }

    string BuildGameOverText()
    {
        string stats = ArabicUI.Format(ArabicStrings.GameOverStats, totalKills, Mathf.Max(bestKills, totalKills));
        string record = IsNewRecord() ? "\n" + ArabicStrings.NewRecord : "";
        string progress = "\n" + ArabicUI.Format(ArabicStrings.LastCleared, LevelDirector.LastClearedLevel);
        return ArabicStrings.GameOverTitle + "\n" + stats + record + progress;
    }

    void ShareResult()
    {
        string stats = ArabicUI.Format(ArabicStrings.GameOverStats, totalKills, Mathf.Max(bestKills, totalKills));
        string message = "MYBUPGI - " + stats + " #MyBUPGI\n" +
                         ArabicUI.Format(ArabicStrings.LastCleared, LevelDirector.LastClearedLevel);
        if (GameFeatures.instance != null)
        {
            string achievements = GameFeatures.instance.GetAchievementSummary();
            if (!string.IsNullOrEmpty(achievements))
                message += "\n" + ArabicUI.Format(ArabicStrings.ShareAchievements, achievements);
        }
        try { GUIUtility.systemCopyBuffer = message; } catch (System.Exception) { }
        Application.OpenURL("https://wa.me/?text=" + System.Uri.EscapeDataString(message));
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
                gameOverText.text = BuildGameOverText();
                gameOverText.color = new Color(1f, 0.35f, 0.25f);
            }
        }

        SaveRecords();

        // Stop the game world
        Time.timeScale = 0f;
    }
}
