using UnityEngine;

/// <summary>
/// Lightweight procedural sound effects (hitmarker, kill confirm, wave start/clear, reload click).
/// Generates tiny AudioClips in memory at runtime so WebGL build size stays unaffected.
/// </summary>
public class GameAudio : MonoBehaviour
{
    public static GameAudio instance;

    private AudioSource sfxSource;
    private AudioClip hitClip;
    private AudioClip killClip;
    private AudioClip waveStartClip;
    private AudioClip waveCompleteClip;
    private AudioClip reloadClickClip;
    private AudioClip sonarPingClip;
    private AudioClip dashClip;
    private AudioClip slowMoClip;

    private const int SampleRate = 22050;

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
        sfxSource.spatialBlend = 0f; // 2D UI/feedback audio

        hitClip = BuildToneClip("HitTick", 0.055f, 1500f, 900f, 0.35f);
        killClip = BuildDualToneClip("KillConfirm", 0.14f, 660f, 990f, 0.42f);
        waveStartClip = BuildDualToneClip("WaveStart", 0.36f, 330f, 495f, 0.35f);
        waveCompleteClip = BuildTriadClip("WaveComplete", 0.48f, 440f, 554.37f, 659.25f, 0.35f);
        reloadClickClip = BuildToneClip("ReloadClick", 0.07f, 420f, 220f, 0.28f);
        sonarPingClip = BuildToneClip("SonarPing", 0.42f, 1320f, 880f, 0.40f);
        dashClip = BuildToneClip("DashWhoosh", 0.18f, 240f, 780f, 0.34f);
        slowMoClip = BuildToneClip("SlowMoDrop", 0.35f, 520f, 140f, 0.38f);
    }

    public static void PlayDash()
    {
        Ensure();
        if (instance == null || instance.sfxSource == null || instance.dashClip == null) return;
        instance.sfxSource.PlayOneShot(instance.dashClip, 0.55f);
    }

    public static void PlaySlowMo()
    {
        Ensure();
        if (instance == null || instance.sfxSource == null || instance.slowMoClip == null) return;
        instance.sfxSource.PlayOneShot(instance.slowMoClip, 0.6f);
    }

    public static void PlaySonarPing()
    {
        Ensure();
        if (instance == null || instance.sfxSource == null || instance.sonarPingClip == null) return;
        instance.sfxSource.PlayOneShot(instance.sonarPingClip, 0.6f);
    }

    public static void PlayHit(bool isKill = false)
    {
        Ensure();
        if (instance == null || instance.sfxSource == null) return;
        AudioClip clip = isKill ? instance.killClip : instance.hitClip;
        if (clip != null) instance.sfxSource.PlayOneShot(clip, isKill ? 0.65f : 0.45f);
    }

    public static void PlayWaveStart()
    {
        Ensure();
        if (instance == null || instance.sfxSource == null || instance.waveStartClip == null) return;
        instance.sfxSource.PlayOneShot(instance.waveStartClip, 0.55f);
    }

    public static void PlayWaveComplete()
    {
        Ensure();
        if (instance == null || instance.sfxSource == null || instance.waveCompleteClip == null) return;
        instance.sfxSource.PlayOneShot(instance.waveCompleteClip, 0.6f);
    }

    public static void PlayReloadClick()
    {
        Ensure();
        if (instance == null || instance.sfxSource == null || instance.reloadClickClip == null) return;
        instance.sfxSource.PlayOneShot(instance.reloadClickClip, 0.45f);
    }

    static AudioClip BuildToneClip(string name, float duration, float startFreq, float endFreq, float volume)
    {
        int samples = Mathf.Max(64, Mathf.RoundToInt(SampleRate * duration));
        float[] data = new float[samples];
        float phase = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float freq = Mathf.Lerp(startFreq, endFreq, t);
            phase += 2f * Mathf.PI * freq / SampleRate;
            float env = (1f - t) * (1f - t);
            data[i] = Mathf.Sin(phase) * env * volume;
        }
        AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip BuildDualToneClip(string name, float duration, float freqA, float freqB, float volume)
    {
        int samples = Mathf.Max(64, Mathf.RoundToInt(SampleRate * duration));
        float[] data = new float[samples];
        int half = samples / 2;
        float phase = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float freq = i < half ? freqA : freqB;
            phase += 2f * Mathf.PI * freq / SampleRate;
            float localT = i < half ? (float)i / half : (float)(i - half) / (samples - half);
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(localT)) * (1f - t * 0.35f);
            data[i] = Mathf.Sin(phase) * env * volume;
        }
        AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip BuildTriadClip(string name, float duration, float f1, float f2, float f3, float volume)
    {
        int samples = Mathf.Max(64, Mathf.RoundToInt(SampleRate * duration));
        float[] data = new float[samples];
        int third = samples / 3;
        float phase = 0f;
        for (int i = 0; i < samples; i++)
        {
            float freq = i < third ? f1 : (i < third * 2 ? f2 : f3);
            phase += 2f * Mathf.PI * freq / SampleRate;
            float env = 1f - ((float)i / samples) * 0.75f;
            data[i] = Mathf.Sin(phase) * env * volume;
        }
        AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
