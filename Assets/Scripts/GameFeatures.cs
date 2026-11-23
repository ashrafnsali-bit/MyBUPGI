using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// Adds the "surprise & gifts" layer: random gift boxes, temporary power-ups,
/// surprise events, a daily gift and persistent achievements.
/// All timers use unscaled time so slow-motion / game-over pauses cannot stall them.
/// </summary>
public class GameFeatures : MonoBehaviour
{
    public static GameFeatures instance;

    [Header("Gift boxes")]
    public float firstGiftDelay = 12f;
    public float giftIntervalMin = 20f;
    public float giftIntervalMax = 32f;
    public float giftLifetime = 25f;

    [Header("Surprise events")]
    public float firstEventDelay = 26f;
    public float eventIntervalMin = 30f;
    public float eventIntervalMax = 48f;

    private const string DailyKey = "MyBUPGI_LastDaily";
    private const string AchPrefix = "MyBUPGI_Ach_";

    private float nextGiftTime;
    private float nextEventTime;
    private float nextBuffUiTime;
    private float speedEnd, rapidEnd, damageEnd, scoreEnd;
    private float challengeEnd;
    private int challengeTarget, challengeKills;
    private bool challengeActive;

    private GameObject playerObj;
    private PlayerMovementScript movement;
    private PlayerHealth health;
    private PlayerAbilities abilities;
    private PlayerGrenades grenades;

    public float DamageMultiplier { get { return Time.unscaledTime < damageEnd ? 2f : 1f; } }
    public float FireRateMultiplier { get { return Time.unscaledTime < rapidEnd ? 2f : 1f; } }
    public float ScoreMultiplier { get { return Time.unscaledTime < scoreEnd ? 2f : 1f; } }

    void Awake()
    {
        instance = this;
        float now = Time.unscaledTime;
        nextGiftTime = now + firstGiftDelay;
        nextEventTime = now + firstEventDelay;
    }

    void Start()
    {
        speedEnd = rapidEnd = damageEnd = scoreEnd = 0f;
        challengeActive = false;
        CachePlayer();
        // The daily gift is claimed explicitly from the start-overlay button,
        // so it stays a visible reward instead of being consumed silently here.
    }

    void CachePlayer()
    {
        if (playerObj != null) return;
        playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null) return;
        movement = playerObj.GetComponent<PlayerMovementScript>();
        health = playerObj.GetComponent<PlayerHealth>();
        abilities = playerObj.GetComponent<PlayerAbilities>();
        grenades = playerObj.GetComponent<PlayerGrenades>();
    }

    void Update()
    {
        float now = Time.unscaledTime;

        CachePlayer();
        if (movement != null) movement.speedMultiplier = now < speedEnd ? 1.5f : 1f;

        if (now >= nextBuffUiTime)
        {
            nextBuffUiTime = now + 1f;
            string active = GetActiveBuffLabel(now);
            if (!string.IsNullOrEmpty(active) && UIManager.instance != null)
                UIManager.instance.ShowBuff(active, 1.2f);
        }

        if (GameManager.instance == null || GameManager.instance.isGameOver) return;

        if (now >= nextGiftTime)
        {
            SpawnGiftBox();
            nextGiftTime = now + Random.Range(giftIntervalMin, giftIntervalMax);
        }

        if (now >= nextEventTime)
        {
            TriggerRandomEvent();
            nextEventTime = now + Random.Range(eventIntervalMin, eventIntervalMax);
        }

        if (challengeActive && now >= challengeEnd)
        {
            challengeActive = false;
            Announce(ArabicStrings.EventFailed, 2f);
        }
    }

    void Announce(string message, float duration)
    {
        if (GameManager.instance != null) GameManager.instance.ShowAnnouncement(message, duration);
    }

    string GetActiveBuffLabel(float now)
    {
        List<string> parts = new List<string>();
        if (now < speedEnd) parts.Add(ArabicStrings.BuffSpeed);
        if (now < rapidEnd) parts.Add(ArabicStrings.BuffRapid);
        if (now < damageEnd) parts.Add(ArabicStrings.BuffDamage);
        if (now < scoreEnd) parts.Add(ArabicStrings.BuffScore);
        return parts.Count == 0 ? "" : string.Join(" | ", parts);
    }

    // ---------------------------------------------------------------- gifts

    void SpawnGiftBox()
    {
        CachePlayer();
        if (playerObj == null) return;

        Vector3 spot = Vector3.zero;
        bool found = false;
        for (int i = 0; i < 14; i++)
        {
            Vector2 r = Random.insideUnitCircle * Random.Range(10f, 24f);
            Vector3 candidate = playerObj.transform.position + new Vector3(r.x, 0f, r.y);
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(candidate, out hit, 4f, NavMesh.AllAreas)) continue;
            if (GameManager.IsInsideOrNearContainer(hit.position)) continue;
            spot = hit.position + Vector3.up * 1.1f;
            found = true;
            break;
        }
        if (!found) return;

        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = "SurpriseGift";
        box.transform.position = spot;
        box.transform.localScale = Vector3.one * 0.9f;

        Color gold = new Color(1f, 0.72f, 0.12f);
        Renderer rend = box.GetComponent<Renderer>();
        if (rend != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader != null)
            {
                Material mat = new Material(shader);
                mat.color = gold;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", gold);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.6f);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.7f);
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", gold * 1.5f);
                rend.material = mat;
            }
            else rend.material.color = Color.yellow;
        }

        // A simple red ribbon makes it read as a present rather than a crate.
        GameObject ribbon = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ribbon.name = "Ribbon";
        ribbon.transform.SetParent(box.transform, false);
        ribbon.transform.localScale = new Vector3(1.04f, 0.14f, 1.04f);
        Renderer rr = ribbon.GetComponent<Renderer>();
        if (rr != null) rr.material.color = new Color(0.95f, 0.16f, 0.35f);
        Collider rc = ribbon.GetComponent<Collider>();
        if (rc != null) Destroy(rc);

        // Tall vertical golden beacon beam so players can spot the mystery gift across the arena.
        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.name = "GiftLightBeam";
        beam.transform.SetParent(box.transform, false);
        beam.transform.localPosition = new Vector3(0f, 12f, 0f);
        beam.transform.localScale = new Vector3(0.28f, 12f, 0.28f);
        Collider bc = beam.GetComponent<Collider>();
        if (bc != null) Destroy(bc);
        Renderer br = beam.GetComponent<Renderer>();
        if (br != null)
        {
            Shader beamShader = Shader.Find("Sprites/Default");
            if (beamShader == null) beamShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (beamShader != null)
            {
                Material bmat = new Material(beamShader);
                Color beamCol = new Color(1f, 0.82f, 0.18f, 0.42f);
                bmat.color = beamCol;
                if (bmat.HasProperty("_BaseColor")) bmat.SetColor("_BaseColor", beamCol);
                br.material = bmat;
            }
        }

        Collider boxCollider = box.GetComponent<Collider>();
        if (boxCollider != null) boxCollider.isTrigger = true;
        SphereCollider trigger = box.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 1.8f;

        GiftBoxPickup pickup = box.AddComponent<GiftBoxPickup>();
        pickup.expireTime = Time.unscaledTime + giftLifetime;
        Destroy(box, giftLifetime);

        Announce(ArabicStrings.GiftAppeared, 2.4f);
    }

    public void OpenGift()
    {
        CachePlayer();
        string label;
        switch (Random.Range(0, 7))
        {
            case 0:
                speedEnd = Time.unscaledTime + 15f;
                label = ArabicStrings.BuffSpeed;
                break;
            case 1:
                rapidEnd = Time.unscaledTime + 15f;
                label = ArabicStrings.BuffRapid;
                break;
            case 2:
                damageEnd = Time.unscaledTime + 18f;
                label = ArabicStrings.BuffDamage;
                break;
            case 3:
                if (abilities != null) abilities.currentShield = abilities.maxShield;
                label = ArabicStrings.BuffShield;
                break;
            case 4:
                if (grenades != null) grenades.AddGrenades(3);
                label = ArabicStrings.BuffGrenades;
                break;
            case 5:
                scoreEnd = Time.unscaledTime + 20f;
                label = ArabicStrings.BuffScore;
                break;
            default:
                if (health != null) health.Heal(40f);
                label = ArabicStrings.BuffHeal;
                break;
        }

        if (GameManager.instance != null) GameManager.instance.score += 150;
        Announce(ArabicStrings.GiftOpened + "\n" + label, 2.5f);
        if (UIManager.instance != null) UIManager.instance.ShowBuff(label, 3f);
    }

    // --------------------------------------------------------------- events

    void TriggerRandomEvent()
    {
        GameManager gm = GameManager.instance;
        int roll = Random.Range(0, 4);
        switch (roll)
        {
            case 0:
                Announce(ArabicStrings.EventAmbush, 2.6f);
                if (gm != null) gm.SpawnBonusEnemies(3 + Mathf.Min(3, gm.currentLevel / 2));
                break;
            case 1:
                Announce(ArabicStrings.EventElite, 2.6f);
                if (gm != null) gm.SpawnEliteEnemy();
                break;
            case 2:
            {
                int level = gm != null ? gm.currentLevel : 1;
                challengeTarget = 3 + Mathf.Min(4, level / 2);
                challengeKills = 0;
                challengeEnd = Time.unscaledTime + 22f;
                challengeActive = true;
                string msg = ArabicUI.Format(ArabicStrings.EventChallenge, challengeTarget, 22);
                Announce(msg, 3f);
                if (UIManager.instance != null) UIManager.instance.ShowEventTimer(msg, 22f);
                break;
            }
            default:
                scoreEnd = Time.unscaledTime + 20f;
                Announce(ArabicStrings.EventDoubleScore, 2.6f);
                break;
        }
    }

    public void NotifyEnemyKilled()
    {
        if (!challengeActive) return;
        challengeKills++;
        if (challengeKills < challengeTarget) return;

        challengeActive = false;
        int bonus = 500 + (GameManager.instance != null ? 100 * GameManager.instance.currentLevel : 0);
        if (GameManager.instance != null) GameManager.instance.score += bonus;
        string msg = ArabicUI.Format(ArabicStrings.EventSuccess, bonus);
        Announce(msg, 3f);
        if (UIManager.instance != null) UIManager.instance.ShowBuff(msg, 3f);
    }

    // ----------------------------------------------------------- daily gift

    public bool IsDailyClaimed()
    {
        return PlayerPrefs.GetString(DailyKey, "") == System.DateTime.UtcNow.ToString("yyyy-MM-dd");
    }

    public void ClaimDailyGift()
    {
        if (IsDailyClaimed())
        {
            Announce(ArabicStrings.DailyAlready, 2f);
            return;
        }

        CachePlayer();
        const int bonus = 300;
        if (GameManager.instance != null) GameManager.instance.score += bonus;
        if (grenades != null) grenades.AddGrenades(2);
        if (health != null) health.Heal(25f);

        PlayerPrefs.SetString(DailyKey, System.DateTime.UtcNow.ToString("yyyy-MM-dd"));
        PlayerPrefs.Save();
        Announce(ArabicUI.Format(ArabicStrings.DailyReward, bonus), 3.2f);
    }

    // --------------------------------------------------------- achievements

    public void CheckAchievements(int kills, int combo, int level)
    {
        if (kills >= 1) Unlock("FirstBlood", ArabicStrings.AchFirstBlood);
        if (combo >= 5) Unlock("Combo5", ArabicStrings.AchCombo5);
        if (kills >= 50) Unlock("Kills50", ArabicStrings.AchKills50);
        if (kills >= 100) Unlock("Kills100", ArabicStrings.AchKills100);
        if (level >= 5) Unlock("Level5", ArabicStrings.AchLevel5);
    }

    void Unlock(string id, string label)
    {
        string key = AchPrefix + id;
        if (PlayerPrefs.GetInt(key, 0) == 1) return;
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
        if (UIManager.instance != null) UIManager.instance.ShowAchievement(label);
    }

    public string GetAchievementSummary()
    {
        List<string> names = new List<string>();
        if (PlayerPrefs.GetInt(AchPrefix + "FirstBlood", 0) == 1) names.Add(ArabicStrings.AchFirstBlood);
        if (PlayerPrefs.GetInt(AchPrefix + "Combo5", 0) == 1) names.Add(ArabicStrings.AchCombo5);
        if (PlayerPrefs.GetInt(AchPrefix + "Kills50", 0) == 1) names.Add(ArabicStrings.AchKills50);
        if (PlayerPrefs.GetInt(AchPrefix + "Kills100", 0) == 1) names.Add(ArabicStrings.AchKills100);
        if (PlayerPrefs.GetInt(AchPrefix + "Level5", 0) == 1) names.Add(ArabicStrings.AchLevel5);
        return names.Count == 0 ? "" : string.Join(" | ", names);
    }
}

/// <summary>World gift box: opens when the player walks into it.</summary>
public class GiftBoxPickup : MonoBehaviour
{
    public float expireTime;
    private bool opened;
    private Vector3 basePos;

    void Start()
    {
        basePos = transform.position;
        if (expireTime <= 0f) expireTime = Time.unscaledTime + 25f;
    }

    void Update()
    {
        transform.Rotate(Vector3.up * 90f * Time.deltaTime, Space.World);
        transform.position = new Vector3(basePos.x, basePos.y + Mathf.Sin(Time.time * 2.5f) * 0.22f, basePos.z);
        float remaining = Mathf.Max(0f, expireTime - Time.unscaledTime);
        if (UIManager.instance != null) UIManager.instance.UpdateGiftBeacon(transform.position, remaining);
    }

    void OnTriggerEnter(Collider other)
    {
        if (opened || !other.CompareTag("Player")) return;
        opened = true;
        if (UIManager.instance != null) UIManager.instance.ClearGiftBeacon();
        if (GameFeatures.instance != null) GameFeatures.instance.OpenGift();
        GameAudio.PlayWaveComplete();
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (UIManager.instance != null) UIManager.instance.ClearGiftBeacon();
    }
}
