using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using System.Collections.Generic;

[InitializeOnLoad]
public class AutoFixCamera
{
    static AutoFixCamera()
    {
        // Run once after compilation
        EditorApplication.delayCall += () => {
            if (!SessionState.GetBool("CameraFixApplied", false))
            {
                FixPlayerCamera.FixAll();
                SessionState.SetBool("CameraFixApplied", true);
            }
        };
    }
}

public class FixPlayerCamera : EditorWindow
{
    [MenuItem("Tools/Screaming Fix/1. Fix Graphics Settings")]
    public static void ManualFixGraphics() { FixGraphicsSettings(); AssetDatabase.SaveAssets(); }

    [MenuItem("Tools/Screaming Fix/2. Convert Materials")]
    public static void ManualConvertMaterials() { ConvertMaterials(); AssetDatabase.SaveAssets(); }

    [MenuItem("Tools/Screaming Fix/3. Scene Cleanup & Lighting")]
    public static void ManualCleanup() { FixSceneLighting(); CleanupHDRP(); AssetDatabase.SaveAssets(); }

    [MenuItem("Tools/Screaming Fix/4. FULL RECOVERY")]
    public static void FixAll()
    {
        Debug.Log("Starting Full Recovery Fix...");
        FixGraphicsSettings();
        ConvertMaterials();
        
        // 1. Fix Prefab
        string prefabPath = "Assets/Easy FPS/Prefabs/Player.prefab";
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (playerPrefab != null)
        {
            string instancePath = AssetDatabase.GetAssetPath(playerPrefab);
            GameObject root = PrefabUtility.LoadPrefabContents(instancePath);
            FixCamerasInObject(root);
            PrefabUtility.SaveAsPrefabAsset(root, instancePath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // 2. Fix Scene
        // FixSceneLighting();
        // CleanupHDRP(); // REMOVED: Too destructive
        
        // Search for player in scene
        GameObject playerInstance = GameObject.FindGameObjectWithTag("Player");
        if (playerInstance != null)
        {
            FixCamerasInObject(playerInstance);
        }

        Debug.Log("Full Recovery Fix Complete!");
        EditorUtility.DisplayDialog("Fix Complete", "All systems optimized for URP.\n\nPlease 'Build and Run' now!", "Let's Go");
    }

    private static void FixGraphicsSettings()
    {
        string assetPath = "Assets/Settings/URP-Main-Asset.asset";
        UniversalRenderPipelineAsset urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
        if (urpAsset != null)
        {
            GraphicsSettings.defaultRenderPipeline = urpAsset;
            QualitySettings.renderPipeline = urpAsset;
            Debug.Log("URP Asset assigned to Graphics & Quality Settings.");
        }
    }

    public static void ConvertMaterials()
    {
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) return;

        int count = 0;
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            string sName = mat.shader != null ? mat.shader.name : "null";
            bool needsFix = mat.shader == null || 
                            sName == "Standard" || 
                            sName.Contains("InternalError") || 
                            sName.Contains("HDRP/") ||
                            sName.Contains("Autodesk") ||
                            sName.Contains("Legacy Shaders/");

            if (needsFix)
            {
                mat.shader = urpLit;
                EditorUtility.SetDirty(mat);
                count++;
            }
        }
        Debug.Log($"Converted {count} materials to URP Lit.");
    }

    private static void FixSceneLighting()
    {
        // 1. Boost Directional Light
        Light[] allLights = GameObject.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var l in allLights)
        {
            if (l.type == LightType.Directional)
            {
                l.intensity = Mathf.Max(l.intensity, 1.5f);
                l.color = Color.white;
            }
        }

        // 2. Fix Ambient & Skybox
        RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1.0f;
    }

    // EMPTY METHOD: Prevents compilation errors without deleting map objects
    private static void CleanupHDRP()
    {
    }

    private static void FixCamerasInObject(GameObject root)
    {
        Camera mainCam = null;
        Camera secondCam = null;

        Camera[] cams = root.GetComponentsInChildren<Camera>(true);
        foreach (var cam in cams)
        {
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            
            cam.cullingMask = -1; // Everything
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, 2000f);

            if (cam.gameObject.name.ToLower().Contains("main"))
                mainCam = cam;
            else if (cam.gameObject.name.ToLower().Contains("second") || cam.gameObject.name.ToLower().Contains("weapon"))
                secondCam = cam;
        }

        if (mainCam != null)
        {
            var mainData = mainCam.GetComponent<UniversalAdditionalCameraData>();
            mainData.renderType = CameraRenderType.Base;
            mainCam.enabled = true;
            mainCam.gameObject.SetActive(true);

            // Disable redundant scene cameras
            Camera[] allCams = GameObject.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var c in allCams)
            {
                if (c == mainCam || c == secondCam || c.transform.IsChildOf(mainCam.transform)) continue;
                if (c.transform.root.CompareTag("Player")) continue;
                
                c.gameObject.SetActive(false);
            }

            if (secondCam != null)
            {
                var secondData = secondCam.GetComponent<UniversalAdditionalCameraData>();
                secondData.renderType = CameraRenderType.Overlay;
                secondCam.enabled = true;
                secondCam.gameObject.SetActive(true);

                if (!mainData.cameraStack.Contains(secondCam))
                    mainData.cameraStack.Add(secondCam);
            }
        }
    }
}
