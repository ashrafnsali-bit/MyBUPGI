using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Runtime.InteropServices;

/// <summary>
/// Manages WebRTC Co-op Multiplayer state sync between Unity WebGL and the browser PeerJS layer.
/// Spawns 3D teammate avatars with floating Arabic nameplates and syncs local player position/stats.
/// </summary>
public class MultiplayerManager : MonoBehaviour
{
    public static MultiplayerManager instance;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void MP_SendLocalState(string json);

    [DllImport("__Internal")]
    private static extern void MP_NotifyReady();
#endif

    [System.Serializable]
    public class PeerJoinData
    {
        public string id;
        public string name;
        public int colorIndex;
    }

    [System.Serializable]
    public class PeerStateData
    {
        public string id;
        public string name;
        public float x;
        public float y;
        public float z;
        public float ry;
        public int score;
        public int kills;
        public int hp;
        public bool firing;
    }

    private class RemotePeer
    {
        public string id;
        public string displayName;
        public GameObject avatarRoot;
        public GameObject muzzleFlashObj;
        public Vector3 targetPos;
        public float targetRotY;
        public float muzzleTimer;
        public RectTransform nameplateRt;
        public Text nameplateText;
    }

    private readonly Dictionary<string, RemotePeer> peers = new Dictionary<string, RemotePeer>();
    private Canvas nameplateCanvas;
    private Transform localPlayer;
    private PlayerHealth localHealth;
    private float nextSendTime;
    private bool firedSinceLastSync;

    private static readonly Color[] TeamColors = new Color[]
    {
        new Color(0.18f, 0.78f, 0.98f), // Cyan
        new Color(0.22f, 0.88f, 0.48f), // Emerald
        new Color(1.00f, 0.76f, 0.18f), // Gold
        new Color(0.82f, 0.45f, 1.00f), // Violet
        new Color(1.00f, 0.48f, 0.30f), // Coral
        new Color(0.45f, 0.95f, 0.85f)  // Mint
    };

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        gameObject.name = "MultiplayerManager"; // Exact name used by unityInstance.SendMessage
        SetupNameplateCanvas();
    }

    void Start()
    {
        CacheLocalPlayer();
#if UNITY_WEBGL && !UNITY_EDITOR
        MP_NotifyReady();
#endif
    }

    void CacheLocalPlayer()
    {
        if (localPlayer != null) return;
        GameObject p = GameObject.FindWithTag("Player");
        if (p == null) return;
        localPlayer = p.transform;
        localHealth = p.GetComponent<PlayerHealth>();
    }

    void SetupNameplateCanvas()
    {
        GameObject cObj = new GameObject("MultiplayerNameplatesCanvas");
        cObj.transform.SetParent(transform, false);
        nameplateCanvas = cObj.AddComponent<Canvas>();
        nameplateCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        nameplateCanvas.sortingOrder = 5;
        CanvasScaler scaler = cObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
    }

    public void NotifyLocalFired()
    {
        firedSinceLastSync = true;
    }

    void Update()
    {
        CacheLocalPlayer();

        // 1. Send local player state at ~16 Hz to WebRTC peers
        if (localPlayer != null && Time.unscaledTime >= nextSendTime)
        {
            nextSendTime = Time.unscaledTime + 0.06f;
            SendLocalStateToJS();
        }

        // 2. Smoothly interpolate remote teammate avatars & update floating nameplates
        Camera cam = Camera.main;
        bool hideNames = UIManager.IsStartOverlayActive || (GameManager.instance != null && GameManager.instance.isGameOver);

        foreach (var kv in peers)
        {
            RemotePeer rp = kv.Value;
            if (rp.avatarRoot == null) continue;

            rp.avatarRoot.transform.position = Vector3.Lerp(rp.avatarRoot.transform.position, rp.targetPos, Time.unscaledDeltaTime * 12f);
            Quaternion targetRot = Quaternion.Euler(0f, rp.targetRotY, 0f);
            rp.avatarRoot.transform.rotation = Quaternion.Slerp(rp.avatarRoot.transform.rotation, targetRot, Time.unscaledDeltaTime * 14f);

            if (rp.muzzleTimer > 0f)
            {
                rp.muzzleTimer -= Time.unscaledDeltaTime;
                if (rp.muzzleTimer <= 0f && rp.muzzleFlashObj != null) rp.muzzleFlashObj.SetActive(false);
            }

            if (rp.nameplateRt != null)
            {
                if (hideNames || cam == null)
                {
                    rp.nameplateRt.gameObject.SetActive(false);
                }
                else
                {
                    Vector3 headWorld = rp.avatarRoot.transform.position + Vector3.up * 2.25f;
                    Vector3 screenPos = cam.WorldToScreenPoint(headWorld);
                    bool inFront = screenPos.z > 0.5f && screenPos.z < 90f;
                    rp.nameplateRt.gameObject.SetActive(inFront);
                    if (inFront)
                    {
                        rp.nameplateRt.position = new Vector3(screenPos.x, screenPos.y, 0f);
                    }
                }
            }
        }
    }

    void SendLocalStateToJS()
    {
        Vector3 pos = localPlayer.position;
        float ry = localPlayer.eulerAngles.y;
        int sc = GameManager.instance != null ? GameManager.instance.score : 0;
        int kl = GameManager.instance != null ? GameManager.instance.totalKills : 0;
        int hp = localHealth != null ? Mathf.RoundToInt(localHealth.currentHealth) : 100;

        string json = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "{{\"x\":{0:F2},\"y\":{1:F2},\"z\":{2:F2},\"ry\":{3:F1},\"score\":{4},\"kills\":{5},\"hp\":{6},\"firing\":{7}}}",
            pos.x, pos.y, pos.z, ry, sc, kl, hp, firedSinceLastSync ? "true" : "false"
        );
        firedSinceLastSync = false;

#if UNITY_WEBGL && !UNITY_EDITOR
        MP_SendLocalState(json);
#endif
    }

    // ---- Called from JavaScript via unityInstance.SendMessage("MultiplayerManager", ...) ----

    public void OnPeerJoined(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        PeerJoinData data = JsonUtility.FromJson<PeerJoinData>(json);
        if (data == null || string.IsNullOrEmpty(data.id)) return;
        EnsureRemotePeer(data.id, data.name, data.colorIndex);
    }

    public void OnPeerState(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        PeerStateData data = JsonUtility.FromJson<PeerStateData>(json);
        if (data == null || string.IsNullOrEmpty(data.id)) return;

        RemotePeer rp = EnsureRemotePeer(data.id, data.name, Mathf.Abs(data.id.GetHashCode()));
        rp.targetPos = new Vector3(data.x, data.y, data.z);
        rp.targetRotY = data.ry;

        if (!string.IsNullOrEmpty(data.name) && rp.displayName != data.name)
        {
            rp.displayName = data.name;
            if (rp.nameplateText != null) rp.nameplateText.text = data.name;
        }

        if (data.firing && rp.muzzleFlashObj != null)
        {
            rp.muzzleFlashObj.SetActive(true);
            rp.muzzleTimer = 0.08f;
        }
    }

    public void OnPeerLeft(string peerId)
    {
        if (string.IsNullOrEmpty(peerId)) return;
        RemotePeer rp;
        if (peers.TryGetValue(peerId, out rp))
        {
            if (rp.avatarRoot != null) Destroy(rp.avatarRoot);
            if (rp.nameplateRt != null) Destroy(rp.nameplateRt.gameObject);
            peers.Remove(peerId);
        }
    }

    public void OnClearAllPeers(string unused)
    {
        foreach (var kv in peers)
        {
            if (kv.Value.avatarRoot != null) Destroy(kv.Value.avatarRoot);
            if (kv.Value.nameplateRt != null) Destroy(kv.Value.nameplateRt.gameObject);
        }
        peers.Clear();
    }

    RemotePeer EnsureRemotePeer(string id, string displayName, int colorSeed)
    {
        RemotePeer existing;
        if (peers.TryGetValue(id, out existing)) return existing;

        Color teamCol = TeamColors[Mathf.Abs(colorSeed) % TeamColors.Length];
        Vector3 spawnPos = localPlayer != null ? localPlayer.position + localPlayer.right * 3f : Vector3.zero;

        GameObject root = new GameObject("Teammate_" + id);
        root.transform.position = spawnPos;

        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) urpLit = Shader.Find("Standard");

        // Body
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 1.0f, 0f);
        body.transform.localScale = new Vector3(0.65f, 0.85f, 0.65f);
        Destroy(body.GetComponent<Collider>());
        ApplyUrpColor(body, urpLit, teamCol);

        // Head / Helmet
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.transform.SetParent(root.transform, false);
        head.transform.localPosition = new Vector3(0f, 1.82f, 0f);
        head.transform.localScale = Vector3.one * 0.44f;
        Destroy(head.GetComponent<Collider>());
        ApplyUrpColor(head, urpLit, new Color(0.14f, 0.18f, 0.24f));

        // Visor (shows facing direction)
        GameObject visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visor.transform.SetParent(head.transform, false);
        visor.transform.localPosition = new Vector3(0f, 0.05f, 0.42f);
        visor.transform.localScale = new Vector3(0.75f, 0.28f, 0.25f);
        Destroy(visor.GetComponent<Collider>());
        ApplyUrpColor(visor, urpLit, new Color(0.2f, 0.95f, 1f));

        // Weapon silhouette
        GameObject rifle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rifle.transform.SetParent(root.transform, false);
        rifle.transform.localPosition = new Vector3(0.32f, 1.25f, 0.52f);
        rifle.transform.localScale = new Vector3(0.12f, 0.14f, 0.85f);
        Destroy(rifle.GetComponent<Collider>());
        ApplyUrpColor(rifle, urpLit, new Color(0.12f, 0.12f, 0.14f));

        // Muzzle flash sphere (activated briefly when teammate shoots)
        GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flash.transform.SetParent(rifle.transform, false);
        flash.transform.localPosition = new Vector3(0f, 0f, 0.6f);
        flash.transform.localScale = new Vector3(1.8f, 1.6f, 0.35f);
        Destroy(flash.GetComponent<Collider>());
        ApplyUrpColor(flash, urpLit, new Color(1f, 0.85f, 0.2f));
        flash.SetActive(false);

        // Floating screen-space nameplate pill
        GameObject plateObj = new GameObject("Nameplate_" + id);
        plateObj.transform.SetParent(nameplateCanvas.transform, false);
        Image plateBg = plateObj.AddComponent<Image>();
        UIStyle.StyleCard(plateBg, new Color(0.05f, 0.08f, 0.14f, 0.82f), false);
        RectTransform plateRt = plateObj.GetComponent<RectTransform>();
        plateRt.sizeDelta = new Vector2(180, 34);

        GameObject txtObj = new GameObject("Name");
        txtObj.transform.SetParent(plateObj.transform, false);
        Text txt = txtObj.AddComponent<Text>();
        UIStyle.ConfigureArabicText(txt, 18, teamCol, true);
        txt.text = string.IsNullOrEmpty(displayName) ? "Teammate" : displayName;
        RectTransform trt = txt.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(6, 2);
        trt.offsetMax = new Vector2(-6, -2);

        RemotePeer rp = new RemotePeer
        {
            id = id,
            displayName = txt.text,
            avatarRoot = root,
            muzzleFlashObj = flash,
            targetPos = spawnPos,
            targetRotY = 0f,
            nameplateRt = plateRt,
            nameplateText = txt
        };
        peers[id] = rp;
        return rp;
    }

    static void ApplyUrpColor(GameObject obj, Shader shader, Color color)
    {
        Renderer r = obj.GetComponent<Renderer>();
        if (r == null) return;
        if (shader != null)
        {
            Material mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            r.material = mat;
        }
        else
        {
            r.material.color = color;
        }
    }
}
