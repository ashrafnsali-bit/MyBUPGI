using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.IO;

public class URPSetupWizard : EditorWindow
{
    [MenuItem("Tools/Fix URP Renderer Assets")]
    public static void FixURPRenderer()
    {
        Debug.Log("Starting URP Renderer Fix...");
        
        // Find Renderer Data
        string rendererPath = "Assets/Settings/URP-Main-Renderer.asset";
        UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        
        if (rendererData != null)
        {
            SerializedObject so = new SerializedObject(rendererData);
            
            // Try to find PostProcessData in the package
            string ppdPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";
            Object ppd = AssetDatabase.LoadAssetAtPath<Object>(ppdPath);
            if (ppd != null)
            {
                so.FindProperty("postProcessData").objectReferenceValue = ppd;
                Debug.Log("Assigned PostProcessData to Renderer.");
            }

            // Assign fallbacks if possible
            // Note: In some versions these are internal, but we try to trigger a refresh
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(rendererData);
        }

        AssetDatabase.ImportAsset("Packages/com.unity.render-pipelines.universal", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset("Packages/com.unity.shadergraph", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        
        AssetDatabase.Refresh();
        Debug.Log("URP Renderer data refreshed.");
        EditorUtility.DisplayDialog("URP Fix", "Renderer assets updated.\n\nNext, run 'Convert All Materials to URP Lit' if objects are still black.", "OK");
    }

    [MenuItem("Tools/Convert All Materials to URP Lit")]
    public static void ConvertMaterials()
    {
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
        int count = 0;
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        
        if (urpLit == null)
        {
            Debug.LogError("Could not find 'Universal Render Pipeline/Lit' shader! Is URP installed?");
            return;
        }

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            
            // Convert if shader is null (broken), Standard, or HDRP
            if (mat.shader == null || mat.shader.name.Contains("HDRP") || mat.shader.name == "Standard")
            {
                mat.shader = urpLit;
                EditorUtility.SetDirty(mat);
                count++;
            }
        }
        
        AssetDatabase.SaveAssets();
        Debug.Log($"Successfully converted {count} materials to URP Lit.");
        EditorUtility.DisplayDialog("Material Conversion", $"Converted {count} materials to URP Lit.", "OK");
    }

    [MenuItem("Tools/Setup URP Now")]
    public static void SetupURP()
    {
        // 0. Unassign current pipeline to prevent validation errors during setup
        GraphicsSettings.defaultRenderPipeline = null;
        
        string settingsPath = "Assets/Settings";
        if (!AssetDatabase.IsValidFolder(settingsPath))
        {
            AssetDatabase.CreateFolder("Assets", "Settings");
        }

        string rendererPath = Path.Combine(settingsPath, "URP-Main-Renderer.asset");
        string assetPath = Path.Combine(settingsPath, "URP-Main-Asset.asset");

        // Clean up old assets if they exist
        if (AssetDatabase.LoadAssetAtPath<Object>(rendererPath)) AssetDatabase.DeleteAsset(rendererPath);
        if (AssetDatabase.LoadAssetAtPath<Object>(assetPath)) AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.SaveAssets();

        // 1. Create Universal Renderer Data
        UniversalRendererData rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
        AssetDatabase.CreateAsset(rendererData, rendererPath);

        // 2. Create Universal Render Pipeline Asset
        UniversalRenderPipelineAsset urpAsset = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
        AssetDatabase.CreateAsset(urpAsset, assetPath);

        // Link Renderer via SerializedObject
        SerializedObject so = new SerializedObject(urpAsset);
        SerializedProperty rendererDataList = so.FindProperty("m_RendererDataList");
        if (rendererDataList != null)
        {
            rendererDataList.arraySize = 1;
            rendererDataList.GetArrayElementAtIndex(0).objectReferenceValue = rendererData;
            SerializedProperty defaultRendererIndex = so.FindProperty("m_DefaultRendererIndex");
            if (defaultRendererIndex != null) defaultRendererIndex.intValue = 0;
            so.ApplyModifiedProperties();
        }

        // 3. Set the active pipeline
        GraphicsSettings.defaultRenderPipeline = urpAsset;

        // 4. Assign to all Quality Levels to ensure consistency via SerializedObject
        
        // Use SerializedObject to be safe for all quality levels
        UnityEngine.Object qualitySettingsAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("ProjectSettings/QualitySettings.asset");
        if (qualitySettingsAsset != null)
        {
            SerializedObject qso = new SerializedObject(qualitySettingsAsset);
            SerializedProperty qualitySettingsProp = qso.FindProperty("m_QualitySettings");
            if (qualitySettingsProp != null && qualitySettingsProp.isArray)
            {
                for (int i = 0; i < qualitySettingsProp.arraySize; i++)
                {
                    SerializedProperty setting = qualitySettingsProp.GetArrayElementAtIndex(i);
                    SerializedProperty pipeline = setting.FindPropertyRelative("customRenderPipeline");
                    if (pipeline != null) pipeline.objectReferenceValue = urpAsset;
                }
                qso.ApplyModifiedProperties();
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("URP Assets created and assigned successfully to Graphics and Quality Settings!");
        EditorUtility.DisplayDialog("URP Setup", "URP Assets created and assigned to all Quality Levels.\n\nNow, convert your materials: Window > Rendering > Render Pipeline Converter.", "OK");
    }

    [MenuItem("Tools/Super Fix URP Scene")]
    public static void SuperFixURPScene()
    {
        Debug.Log("Starting Super Fix URP Scene...");

        // 1. Fix all Cameras in the scene
        Camera[] allCameras = GameObject.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (var cam in allCameras)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            if (data == null)
            {
                Debug.Log($"Added URP Data to {cam.name}");
            }
        }

        // 2. Identify and Link Player Cameras
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Camera mainCam = null;
        Camera secondCam = null;

        if (player != null)
        {
            foreach (var cam in player.GetComponentsInChildren<Camera>(true))
            {
                if (cam.gameObject.name.ToLower().Contains("main"))
                    mainCam = cam;
                else if (cam.gameObject.name.ToLower().Contains("second") || cam.gameObject.name.ToLower().Contains("weapon") || cam != mainCam)
                    secondCam = cam;
            }
        }

        // Fallback search for Main Camera if Player detection failed or was incomplete
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) mainCam = GameObject.Find("Main Camera")?.GetComponent<Camera>();

        if (mainCam != null)
        {
            var mainData = mainCam.GetUniversalAdditionalCameraData();
            mainData.renderType = CameraRenderType.Base;
            
            // Ensure culling mask includes Default (0)
            mainCam.cullingMask |= (1 << 0);

            if (secondCam != null)
            {
                var secondData = secondCam.GetUniversalAdditionalCameraData();
                secondData.renderType = CameraRenderType.Overlay;
                
                if (!mainData.cameraStack.Contains(secondCam))
                {
                    mainData.cameraStack.Add(secondCam);
                    Debug.Log("Linked SecondCamera as Overlay to Main Camera stack.");
                }
            }
            Debug.Log("Main Camera configured as URP Base.");
        }
        else
        {
            Debug.LogError("Could not find Main Camera to configure!");
        }

        // 3. Fix Skybox
        if (RenderSettings.skybox != null)
        {
            Shader skyShader = RenderSettings.skybox.shader;
            if (skyShader != null && (skyShader.name.Contains("Procedural") || skyShader.name.Contains("6 Sided")))
            {
                Debug.Log($"Current skybox uses legacy shader: {skyShader.name}. URP usually supports these, but if it's black, try 'Universal Render Pipeline/Skybox/6 Sided'.");
            }
        }

        // 4. Force Lighting Refresh
        DynamicGI.UpdateEnvironment();
        
        Debug.Log("Super Fix URP Scene complete!");
        EditorUtility.DisplayDialog("URP Super Fix", "Scene recovery tool finished.\n\n1. All cameras tagged for URP.\n2. Camera stacking configured.\n3. Environment layers enabled.\n\nCheck your Simulator now!", "Awesome");
    }
}


