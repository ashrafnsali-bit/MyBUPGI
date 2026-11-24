using UnityEngine;

/// <summary>
/// Realistic 44.1 kHz procedural DSP audio engine for MyBUPGI.
/// Synthesizes multi-layered gunshots (crack + sub-bass + blast + warehouse tail + brass casing),
/// explosions (shockwave + filtered noise roar + debris), air-whoosh Dash, cinematic Slow-Mo drop,
/// echoing tactical Sonar, metallic reload clicks, and subtle industrial wind ambience —
/// all in memory with zero extra WebGL download size.
/// </summary>
public class GameAudio : MonoBehaviour
{
    public static GameAudio instance;

    private AudioSource sfxSource;
    private AudioSource gunSource;
    private AudioSource ambientSource;

    private AudioClip[] rifleClips;
    private AudioClip[] heavyRifleClips;
    private AudioClip hitClip;
    private AudioClip killClip;
    private AudioClip explosionClip;
    private AudioClip waveStartClip;
    private AudioClip waveCompleteClip;
    private AudioClip reloadClickClip;
    private AudioClip sonarPingClip;
    private AudioClip dashClip;
    private AudioClip slowMoClip;
    private AudioClip ambientClip;

    private const int SampleRate = 44100;

    public static void Ensure()
    {
        if (instance != null) return;
        GameObject obj = new GameObject("GameAudio");
        instance = obj.AddComponent<GameAudio>();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;

        gunSource = gameObject.AddComponent<AudioSource>();
        gunSource.playOnAwake = false;
        gunSource.spatialBlend = 0f;

        ambientSource = gameObject.AddComponent<AudioSource>();
        ambientSource.playOnAwake = false;
        ambientSource.spatialBlend = 0f;
        ambientSource.loop = true;
        ambientSource.volume = 0.08f;

        // Build 3 subtle variations of each gunshot so rapid fire sounds organic & realistic
        rifleClips = new AudioClip[]
        {
            BuildRealisticGunshot("Rifle_0", false, 101),
            BuildRealisticGunshot("Rifle_1", false, 207),
            BuildRealisticGunshot("Rifle_2", false, 313)
        };
        heavyRifleClips = new AudioClip[]
        {
            BuildRealisticGunshot("Heavy_0", true, 409),
            BuildRealisticGunshot("Heavy_1", true, 521)
        };

        hitClip = BuildImpactHit("HitImpact", false);
        killClip = BuildImpactHit("KillImpact", true);
        explosionClip = BuildRealisticExplosion("Explosion");
        waveStartClip = BuildTacticalHorn("WaveStart", false);
        waveCompleteClip = BuildTacticalHorn("WaveComplete", true);
        reloadClickClip = BuildMechanicalReload("ReloadMech");
        sonarPingClip = BuildEchoingSonar("SonarPing");
        dashClip = BuildAirWhoosh("DashWhoosh");
        slowMoClip = BuildSlowMoDrop("SlowMoDrop");
        ambientClip = BuildIndustrialAmbience("IndustrialWind");

        ambientSource.clip = ambientClip;
        ambientSource.Play();
    }

    void Update()
    {
        if (ambientSource == null) return;
        bool inCombat = !UIManager.IsStartOverlayActive && (GameManager.instance == null || !GameManager.instance.isGameOver);
        float targetVol = inCombat ? 0.085f : 0.025f;
        ambientSource.volume = Mathf.MoveTowards(ambientSource.volume, targetVol, Time.unscaledDeltaTime * 0.12f);
    }

    public static void PlayGunshot(bool isHeavy = false)
    {
        Ensure();
        if (instance == null || instance.gunSource == null) return;
        AudioClip[] bank = isHeavy ? instance.heavyRifleClips : instance.rifleClips;
        if (bank == null || bank.Length == 0) return;
        AudioClip clip = bank[Random.Range(0, bank.Length)];
        instance.gunSource.pitch = Random.Range(0.95f, 1.05f);
        instance.gunSource.PlayOneShot(clip, isHeavy ? 0.72f : 0.58f);
    }

    public static void PlayHit(bool isKill = false)
    {
        Ensure();
        if (instance == null || instance.sfxSource == null) return;
        AudioClip clip = isKill ? instance.killClip : instance.hitClip;
        if (clip == null) return;
        instance.sfxSource.pitch = Random.Range(0.96f, 1.04f);
        instance.sfxSource.PlayOneShot(clip, isKill ? 0.75f : 0.55f);
    }

    public static void PlayExplosion(float volume = 0.95f)
    {
        Ensure();
        if (instance == null || instance.sfxSource == null || instance.explosionClip == null) return;
        instance.sfxSource.pitch = Random.Range(0.90f, 1.06f);
        instance.sfxSource.PlayOneShot(instance.explosionClip, volume);
    }

    public static void PlayWaveStart()
    {
        Ensure();
        if (instance == null || instance.sfxSource == null || instance.waveStartClip == null) return;
        instance.sfxSource.pitch = 1f;
        instance.sfxSource.PlayOneShot(instance.waveStartClip, 0.52f);
    }

    public static void PlayWaveComplete()
    {
        Ensure();
        if (instance == null || instance.sfxSource == null || instance.waveCompleteClip == null) return;
        instance.sfxSource.pitch = 1f;
        instance.sfxSource.PlayOneShot(instance.waveCompleteClip, 0.58f);
    }

    public static void PlayReloadClick()
    {
        Ensure();
        if (instance == null || instance.sfxSource == null || instance.reloadClickClip == null) return;
        instance.sfxSource.pitch = Random.Range(0.96f, 1.04f);
        instance.sfxSource.PlayOneShot(instance.reloadClickClip, 0.65f);
    }

    public static void PlaySonarPing()
    {
        Ensure();
        if (instance == null || instance.sfxSource == null || instance.sonarPingClip == null) return;
        instance.sfxSource.pitch = 1f;
        instance.sfxSource.PlayOneShot(instance.sonarPingClip, 0.65f);
    }

    public static void PlayDash()
    {
        Ensure();
        if (instance == null || instance.sfxSource == null || instance.dashClip == null) return;
        instance.sfxSource.pitch = Random.Range(0.95f, 1.08f);
        instance.sfxSource.PlayOneShot(instance.dashClip, 0.62f);
    }

    public static void PlaySlowMo()
    {
        Ensure();
        if (instance == null || instance.sfxSource == null || instance.slowMoClip == null) return;
        instance.sfxSource.pitch = 1f;
        instance.sfxSource.PlayOneShot(instance.slowMoClip, 0.72f);
    }

    // ---- Realistic DSP Synthesizers ----

    static float NextNoise(ref uint seed)
    {
        seed = seed * 1664525u + 1013904223u;
        return ((seed >> 8) / 8388608f) - 1f;
    }

    static float SoftClip(float x)
    {
        if (x > 3f) return 1f;
        if (x < -3f) return -1f;
        return x * (27f + x * x) / (27f + 9f * x * x);
    }

    static AudioClip BuildRealisticGunshot(string name, bool heavy, uint seed)
    {
        float duration = heavy ? 0.52f : 0.36f;
        int samples = Mathf.RoundToInt(SampleRate * duration);
        float[] data = new float[samples];

        float lp1 = 0f, lp2 = 0f, bp = 0f;
        float subPhase = 0f;
        float crackPhase = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            float n = NextNoise(ref seed);

            // 1. Supersonic muzzle crack (first 16ms)
            float crackEnv = Mathf.Exp(-t * (heavy ? 110f : 165f));
            float crackFreq = Mathf.Lerp(heavy ? 1400f : 2100f, 160f, Mathf.Min(1f, t * 40f));
            crackPhase += 2f * Mathf.PI * crackFreq / SampleRate;
            float crack = (n * 0.7f + Mathf.Sin(crackPhase) * 0.4f) * crackEnv * 1.15f;

            // 2. Chest-thumping sub-bass punch (first 95ms)
            float subEnv = Mathf.Exp(-t * (heavy ? 18f : 28f));
            float subFreq = Mathf.Lerp(heavy ? 115f : 145f, heavy ? 36f : 48f, Mathf.Min(1f, t * 14f));
            subPhase += 2f * Mathf.PI * subFreq / SampleRate;
            float punch = Mathf.Sin(subPhase) * subEnv * (heavy ? 0.95f : 0.72f);

            // 3. Gunpowder blast + industrial warehouse echo tail
            lp1 += (n - lp1) * (heavy ? 0.14f : 0.22f);
            lp2 += (lp1 - lp2) * 0.12f;
            float blastEnv = Mathf.Exp(-t * (heavy ? 8.5f : 13f));
            float blast = lp1 * blastEnv * 0.85f;
            float tail = lp2 * Mathf.Exp(-t * (heavy ? 4.2f : 6.2f)) * 0.42f;

            // 4. Crisp metallic bolt snap at ~28ms + subtle brass shell clink at ~220ms
            float bolt = 0f;
            if (t > 0.022f && t < 0.055f)
            {
                float bt = t - 0.022f;
                bp = n - lp1;
                bolt = bp * Mathf.Exp(-bt * 120f) * 0.25f;
            }
            float shell = 0f;
            if (!heavy && t > 0.21f && t < 0.30f)
            {
                float st = t - 0.21f;
                shell = (Mathf.Sin(2f * Mathf.PI * 4650f * st) + 0.5f * Mathf.Sin(2f * Mathf.PI * 6920f * st))
                        * Mathf.Exp(-st * 45f) * 0.06f;
            }

            data[i] = SoftClip((crack + punch + blast + tail + bolt + shell) * 1.15f) * 0.9f;
        }

        AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip BuildRealisticExplosion(string name)
    {
        float duration = 1.45f;
        int samples = Mathf.RoundToInt(SampleRate * duration);
        float[] data = new float[samples];
        uint seed = 7919u;
        float lpLow = 0f, lpMid = 0f;
        float subPhase = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            float n = NextNoise(ref seed);

            // Initial detonation crack
            float snap = n * Mathf.Exp(-t * 65f) * 1.1f;

            // Deep sub-bass shockwave
            float subFreq = Mathf.Lerp(88f, 24f, Mathf.Min(1f, t * 2.5f));
            subPhase += 2f * Mathf.PI * subFreq / SampleRate;
            float sub = Mathf.Sin(subPhase) * Mathf.Exp(-t * 3.4f) * 1.0f;

            // Brown/pink noise roar
            lpLow += (n - lpLow) * 0.035f;
            lpMid += (n - lpMid) * 0.16f;
            float roar = lpLow * Mathf.Exp(-t * 2.1f) * 2.2f + lpMid * Mathf.Exp(-t * 5.2f) * 0.7f;

            // Flying debris crackle
            float debris = (Mathf.Abs(n) > 0.94f && t > 0.08f) ? n * Mathf.Exp(-t * 3.0f) * 0.35f : 0f;

            data[i] = SoftClip((snap + sub + roar + debris) * 1.1f) * 0.95f;
        }

        AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip BuildImpactHit(string name, bool isKill)
    {
        float duration = isKill ? 0.16f : 0.065f;
        int samples = Mathf.RoundToInt(SampleRate * duration);
        float[] data = new float[samples];
        uint seed = isKill ? 997u : 557u;
        float lp = 0f;
        float phase = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            float n = NextNoise(ref seed);
            lp += (n - lp) * 0.35f;

            // Crisp tactical hit tick (filtered transient + short thud, never harsh sine beep)
            float tick = (n - lp) * Mathf.Exp(-t * 110f) * 0.85f;
            float thud = lp * Mathf.Exp(-t * 48f) * 0.7f;

            float confirm = 0f;
            if (isKill)
            {
                float f = t < 0.055f ? 520f : 780f;
                phase += 2f * Mathf.PI * f / SampleRate;
                confirm = Mathf.Sin(phase) * Mathf.Exp(-t * 18f) * 0.42f;
            }

            data[i] = SoftClip((tick + thud + confirm) * 1.2f) * 0.85f;
        }

        AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip BuildAirWhoosh(string name)
    {
        float duration = 0.28f;
        int samples = Mathf.RoundToInt(SampleRate * duration);
        float[] data = new float[samples];
        uint seed = 3313u;
        float lp1 = 0f, lp2 = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / duration;
            float n = NextNoise(ref seed);
            float env = Mathf.Sin(Mathf.PI * t);
            float cutoff = Mathf.Lerp(0.05f, 0.32f, env);
            lp1 += (n - lp1) * cutoff;
            lp2 += (lp1 - lp2) * (cutoff * 0.5f);
            float band = (lp1 - lp2) * 2.4f;
            data[i] = SoftClip(band * env * 1.4f) * 0.8f;
        }

        AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip BuildSlowMoDrop(string name)
    {
        float duration = 0.55f;
        int samples = Mathf.RoundToInt(SampleRate * duration);
        float[] data = new float[samples];
        uint seed = 4409u;
        float lp = 0f;
        float phase = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            float norm = t / duration;
            float freq = Mathf.Lerp(210f, 36f, Mathf.Pow(norm, 0.55f));
            phase += 2f * Mathf.PI * freq / SampleRate;

            float n = NextNoise(ref seed);
            lp += (n - lp) * Mathf.Lerp(0.18f, 0.02f, norm);

            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(norm * 1.15f)) * (1f - norm * 0.4f);
            data[i] = SoftClip((Mathf.Sin(phase) * 0.75f + lp * 0.65f) * env * 1.25f) * 0.85f;
        }

        AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip BuildEchoingSonar(string name)
    {
        float duration = 0.95f;
        int samples = Mathf.RoundToInt(SampleRate * duration);
        float[] data = new float[samples];

        float[] delays = { 0f, 0.24f, 0.48f };
        float[] gains = { 0.72f, 0.34f, 0.14f };

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            float sum = 0f;
            for (int e = 0; e < delays.Length; e++)
            {
                float dt = t - delays[e];
                if (dt < 0f) continue;
                float env = Mathf.Exp(-dt * 9.5f) * Mathf.Min(1f, dt * 160f);
                float tone = Mathf.Sin(2f * Mathf.PI * 1160f * dt) * 0.8f
                           + Mathf.Sin(2f * Mathf.PI * 2320f * dt) * 0.2f;
                sum += tone * env * gains[e];
            }
            data[i] = SoftClip(sum) * 0.75f;
        }

        AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip BuildMechanicalReload(string name)
    {
        float duration = 0.26f;
        int samples = Mathf.RoundToInt(SampleRate * duration);
        float[] data = new float[samples];
        uint seed = 8821u;
        float lp = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            float n = NextNoise(ref seed);
            lp += (n - lp) * 0.25f;
            float hp = n - lp;

            // Mag lock click at 0ms + slide rack click at 130ms
            float c1 = (hp * 0.9f + Mathf.Sin(2f * Mathf.PI * 1950f * t) * 0.3f) * Mathf.Exp(-t * 95f);
            float c2 = 0f;
            if (t > 0.125f)
            {
                float t2 = t - 0.125f;
                c2 = (hp * 1.1f + Mathf.Sin(2f * Mathf.PI * 2600f * t2) * 0.25f) * Mathf.Exp(-t2 * 75f);
            }
            data[i] = SoftClip((c1 + c2) * 1.2f) * 0.78f;
        }

        AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip BuildTacticalHorn(string name, bool victory)
    {
        float duration = victory ? 0.65f : 0.48f;
        int samples = Mathf.RoundToInt(SampleRate * duration);
        float[] data = new float[samples];

        float[] notes = victory ? new float[] { 392f, 493.88f, 587.33f } : new float[] { 246.94f, 329.63f };
        float seg = duration / notes.Length;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            int idx = Mathf.Min(notes.Length - 1, Mathf.FloorToInt(t / seg));
            float lt = t - idx * seg;
            float f = notes[idx];
            // Warm brass-like harmonic blend instead of plain sine
            float s = Mathf.Sin(2f * Mathf.PI * f * t) * 0.60f
                    + Mathf.Sin(2f * Mathf.PI * f * 2f * t) * 0.25f
                    + Mathf.Sin(2f * Mathf.PI * f * 3f * t) * 0.10f;
            float env = Mathf.Min(1f, lt * 45f) * Mathf.Exp(-lt * 4.5f);
            data[i] = SoftClip(s * env * 0.9f) * 0.65f;
        }

        AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip BuildIndustrialAmbience(string name)
    {
        float duration = 4.0f;
        int samples = Mathf.RoundToInt(SampleRate * duration);
        float[] data = new float[samples];
        uint seed = 6173u;
        float lp1 = 0f, lp2 = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            float n = NextNoise(ref seed);
            lp1 += (n - lp1) * 0.015f;
            lp2 += (lp1 - lp2) * 0.010f;

            // Gentle industrial wind gust + distant low hum (seamless loop via periodic LFO)
            float gust = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * (t / duration));
            float hum = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.08f;
            data[i] = SoftClip((lp2 * 4.5f * gust + hum) * 0.7f);
        }

        // Crossfade last 0.15s with start so loop seam is completely inaudible
        int fade = Mathf.RoundToInt(SampleRate * 0.15f);
        for (int i = 0; i < fade; i++)
        {
            float w = (float)i / fade;
            int tailIdx = samples - fade + i;
            data[tailIdx] = Mathf.Lerp(data[tailIdx], data[i], w);
        }

        AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
