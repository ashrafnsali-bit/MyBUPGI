using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MobileUISetup : EditorWindow
{
    [MenuItem("Tools/Setup Mobile UI")]
    public static void Setup()
    {
        // 1. EventSystem
        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        // 2. Canvas
        GameObject canvasObj = GameObject.Find("MobileControlsCanvas");
        if (canvasObj != null) DestroyImmediate(canvasObj); // Clean existing

        canvasObj = new GameObject("MobileControlsCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObj.AddComponent<GraphicRaycaster>();

        // 3. Joystick Area (Invisible background to catch drags)
        GameObject joystickArea = new GameObject("JoystickArea");
        joystickArea.transform.SetParent(canvasObj.transform);
        RectTransform areaRect = joystickArea.AddComponent<RectTransform>();
        areaRect.anchorMin = Vector2.zero;
        areaRect.anchorMax = new Vector2(0.5f, 0.5f); // Bottom left half
        areaRect.pivot = Vector2.zero;
        areaRect.anchoredPosition = Vector2.zero;
        areaRect.sizeDelta = Vector2.zero;

        // 4. Joystick Visual Container
        GameObject joystickContainer = new GameObject("JoystickContainer");
        joystickContainer.transform.SetParent(joystickArea.transform);
        RectTransform containerRect = joystickContainer.AddComponent<RectTransform>();
        // Anchor to bottom left so it stays in a fixed spot
        containerRect.anchorMin = new Vector2(0, 0);
        containerRect.anchorMax = new Vector2(0, 0);
        containerRect.pivot = new Vector2(0.5f, 0.5f);
        // Position it nicely in the bottom left corner
        containerRect.anchoredPosition = new Vector2(150, 150); 
        containerRect.sizeDelta = new Vector2(180, 180); // Very small and proportionate

        Image containerImage = joystickContainer.AddComponent<Image>();
        containerImage.color = new Color(1, 1, 1, 0.2f);
        containerImage.raycastTarget = true;

        // 5. Handle
        GameObject joystickHandle = new GameObject("Handle");
        joystickHandle.transform.SetParent(joystickContainer.transform);
        RectTransform handleRect = joystickHandle.AddComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0.5f, 0.5f);
        handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.anchoredPosition = Vector2.zero;
        handleRect.sizeDelta = new Vector2(60, 60);

        Image handleImage = joystickHandle.AddComponent<Image>();
        handleImage.color = new Color(1, 1, 1, 0.5f);
        handleImage.raycastTarget = false;

        // Add component after children are created so Awake can find them
        joystickContainer.AddComponent<MobileJoystick>();

        // 6. Debug Text
        GameObject debugObj = new GameObject("DebugText");
        debugObj.transform.SetParent(canvasObj.transform);
        RectTransform debugRect = debugObj.AddComponent<RectTransform>();
        debugRect.anchorMin = new Vector2(0.5f, 1);
        debugRect.anchorMax = new Vector2(0.5f, 1);
        debugRect.pivot = new Vector2(0.5f, 1);
        debugRect.anchoredPosition = new Vector2(0, -50);
        debugRect.sizeDelta = new Vector2(1000, 300);

        Text t = debugObj.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 40;
        t.alignment = TextAnchor.UpperCenter;
        t.color = Color.green;
        
        canvasObj.AddComponent<MobileInputDebugger>().debugText = t;

        Selection.activeGameObject = canvasObj;
        Debug.Log("Mobile UI Re-Setup Complete! Please re-run the build to your Android device.");
    }
}
