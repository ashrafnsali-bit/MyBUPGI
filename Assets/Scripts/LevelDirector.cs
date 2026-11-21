using UnityEngine;
using System.Collections.Generic;

/// <summary>Procedural level "director". Builds a fresh, non-repeating plan for every
/// level instead of replaying the same wave, and scales enemy stats with depth.</summary>
public enum LevelStyle { Assault, Swarm, Snipers, Armored, Mixed, Ambush }
public enum SpawnPattern { Ring, Flank, Behind, Far }

public class LevelPlan
{
    public int level;
    public LevelStyle style;
    public SpawnPattern pattern;
    public int enemyCount;
    public List<EnemyArchetype> roster = new List<EnemyArchetype>();

    public float healthScale = 1f;
    public float damageScale = 1f;
    public float speedScale = 1f;
    public float fireRateScale = 1f;
    public float eliteChance = 0f;

    public float delayMin = 0.5f;
    public float delayMax = 1.5f;
    public int burstSize = 1;
    public float minDistance = 18f;
    public float maxDistance = 35f;

    public string StyleName { get { return LevelDirector.StyleName(style); } }

    public EnemyArchetype ArchetypeAt(int index)
    {
        if (roster.Count == 0) return EnemyArchetype.Assaulter;
        return roster[index % roster.Count];
    }

    public EnemyArchetype RandomArchetype()
    {
        if (roster.Count == 0) return EnemyArchetype.Assaulter;
        return roster[Random.Range(0, roster.Count)];
    }
}

public class LevelDirector : MonoBehaviour
{
    public static LevelDirector instance;

    private const string ClearedKey = "MyBUPGI_LastClearedLevel";
    private const string HistoryKey = "MyBUPGI_LevelSignatureHistory";

    private List<int> recentSignatures = new List<int>();
    public LevelPlan current { get; private set; }

    public static int LastClearedLevel { get { return PlayerPrefs.GetInt(ClearedKey, 0); } }

    public static void SaveClearedLevel(int level)
    {
        if (level > LastClearedLevel)
        {
            PlayerPrefs.SetInt(ClearedKey, level);
            PlayerPrefs.Save();
        }
    }

    public static string StyleName(LevelStyle style)
    {
        switch (style)
        {
            case LevelStyle.Swarm: return ArabicStrings.StyleSwarm;
            case LevelStyle.Snipers: return ArabicStrings.StyleSnipers;
            case LevelStyle.Armored: return ArabicStrings.StyleArmored;
            case LevelStyle.Mixed: return ArabicStrings.StyleMixed;
            case LevelStyle.Ambush: return ArabicStrings.StyleAmbush;
            default: return ArabicStrings.StyleAssault;
        }
    }

    void Awake()
    {
        instance = this;
        LoadHistory();
    }

    void LoadHistory()
    {
        recentSignatures.Clear();
        string raw = PlayerPrefs.GetString(HistoryKey, "");
        if (string.IsNullOrEmpty(raw)) return;
        string[] parts = raw.Split(',');
        foreach (string part in parts)
        {
            int value;
            if (int.TryParse(part, out value)) recentSignatures.Add(value);
        }
    }

    void Remember(int signature)
    {
        recentSignatures.Remove(signature);
        recentSignatures.Add(signature);
        while (recentSignatures.Count > 8) recentSignatures.RemoveAt(0);
        PlayerPrefs.SetString(HistoryKey, string.Join(",", recentSignatures));
        PlayerPrefs.Save();
    }

    /// <summary>Creates the plan for a level. Never reuses a recent signature, so two
    /// consecutive play sessions (and two attempts at the same level) feel different.</summary>
    public LevelPlan BuildPlan(int level)
    {
        if (level < 1) level = 1;

        LevelStyle style = PickStyle(level);
        int styleBonus = (style == LevelStyle.Swarm || style == LevelStyle.Mixed) ? 2 :
                         (style == LevelStyle.Armored ? -1 : 0);
        int count = Mathf.Clamp(3 + (level - 1) * 2 + styleBonus, 3, 26);

        EnemyArchetype dominant = DominantArchetype(style);
        int signature = ((int)style) * 10000 + count * 10 + (int)dominant;

        // If we have seen this exact plan recently, reroll the style a few times.
        for (int attempt = 0; attempt < 10 && recentSignatures.Contains(signature); attempt++)
        {
            style = PickStyle(level);
            dominant = DominantArchetype(style);
            signature = ((int)style) * 10000 + count * 10 + (int)dominant;
        }
        Remember(signature);

        LevelPlan plan = new LevelPlan();
        plan.level = level;
        plan.style = style;
        plan.pattern = PickPattern(style, level);
        plan.enemyCount = count;
        plan.roster = BuildRoster(style, level);

        float depth = level - 1f;
        plan.healthScale = Mathf.Clamp(1f + depth * 0.10f, 1f, 3.2f);
        plan.damageScale = Mathf.Clamp(1f + depth * 0.045f, 1f, 2.1f);
        plan.speedScale = Mathf.Clamp(1f + depth * 0.020f, 1f, 1.6f);
        plan.fireRateScale = Mathf.Clamp(1f + depth * 0.035f, 1f, 2.0f);
        plan.eliteChance = Mathf.Clamp01((level - 2) * 0.04f);

        plan.delayMin = Mathf.Clamp(0.50f - depth * 0.025f, 0.18f, 0.50f);
        plan.delayMax = Mathf.Clamp(1.50f - depth * 0.060f, 0.45f, 1.50f);
        plan.burstSize = Mathf.Clamp(1 + (int)(depth / 4f), 1, 3);
        plan.minDistance = style == LevelStyle.Ambush ? 12f : 18f;
        plan.maxDistance = style == LevelStyle.Snipers ? 45f : (style == LevelStyle.Ambush ? 24f : 35f);

        current = plan;
        return plan;
    }

    LevelStyle PickStyle(int level)
    {
        // Level 1 always eases the player in; later pools get richer and tougher.
        List<LevelStyle> pool = new List<LevelStyle> { LevelStyle.Assault };
        if (level >= 2)
        {
            pool.Add(LevelStyle.Assault);
            pool.Add(LevelStyle.Swarm);
            pool.Add(LevelStyle.Snipers);
        }
        if (level >= 3)
        {
            pool.Add(LevelStyle.Swarm);
            pool.Add(LevelStyle.Armored);
            pool.Add(LevelStyle.Assault);
        }
        if (level >= 5)
        {
            pool.Add(LevelStyle.Mixed);
            pool.Add(LevelStyle.Ambush);
            pool.Add(LevelStyle.Armored);
            pool.Add(LevelStyle.Snipers);
        }
        return pool[Random.Range(0, pool.Count)];
    }

    SpawnPattern PickPattern(LevelStyle style, int level)
    {
        switch (style)
        {
            case LevelStyle.Ambush: return level >= 6 ? SpawnPattern.Behind : SpawnPattern.Flank;
            case LevelStyle.Snipers: return SpawnPattern.Far;
            case LevelStyle.Swarm: return SpawnPattern.Flank;
            case LevelStyle.Mixed: return (Random.value < 0.5f) ? SpawnPattern.Flank : SpawnPattern.Ring;
            default: return SpawnPattern.Ring;
        }
    }

    EnemyArchetype DominantArchetype(LevelStyle style)
    {
        switch (style)
        {
            case LevelStyle.Swarm: return EnemyArchetype.Rusher;
            case LevelStyle.Snipers: return EnemyArchetype.Sniper;
            case LevelStyle.Armored: return EnemyArchetype.Tank;
            case LevelStyle.Ambush: return EnemyArchetype.Rusher;
            case LevelStyle.Mixed: return EnemyArchetype.Grenadier;
            default: return EnemyArchetype.Assaulter;
        }
    }

    List<EnemyArchetype> BuildRoster(LevelStyle style, int level)
    {
        List<EnemyArchetype> roster = new List<EnemyArchetype>();
        switch (style)
        {
            case LevelStyle.Swarm:
                Add(roster, EnemyArchetype.Rusher, 5); Add(roster, EnemyArchetype.Assaulter, 3);
                if (level >= 4) Add(roster, EnemyArchetype.Grenadier, 2);
                break;
            case LevelStyle.Snipers:
                Add(roster, EnemyArchetype.Sniper, 4); Add(roster, EnemyArchetype.Assaulter, 3);
                if (level >= 4) Add(roster, EnemyArchetype.Tank, 1);
                break;
            case LevelStyle.Armored:
                Add(roster, EnemyArchetype.Tank, 4); Add(roster, EnemyArchetype.Grenadier, 3);
                Add(roster, EnemyArchetype.Assaulter, 2);
                break;
            case LevelStyle.Ambush:
                Add(roster, EnemyArchetype.Rusher, 4); Add(roster, EnemyArchetype.Assaulter, 2);
                Add(roster, EnemyArchetype.Grenadier, 2); Add(roster, EnemyArchetype.Sniper, 1);
                break;
            case LevelStyle.Mixed:
                Add(roster, EnemyArchetype.Assaulter, 2); Add(roster, EnemyArchetype.Rusher, 2);
                Add(roster, EnemyArchetype.Sniper, 2); Add(roster, EnemyArchetype.Tank, 2);
                Add(roster, EnemyArchetype.Grenadier, 2);
                break;
            default:
                Add(roster, EnemyArchetype.Assaulter, 5); Add(roster, EnemyArchetype.Rusher, 2);
                if (level >= 3) { Add(roster, EnemyArchetype.Sniper, 1); Add(roster, EnemyArchetype.Grenadier, 1); }
                break;
        }

        // Shuffle so the spawn order changes every time, even for the same style.
        for (int i = roster.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            EnemyArchetype tmp = roster[i];
            roster[i] = roster[j];
            roster[j] = tmp;
        }
        return roster;
    }

    static void Add(List<EnemyArchetype> list, EnemyArchetype type, int times)
    {
        for (int i = 0; i < times; i++) list.Add(type);
    }
}
