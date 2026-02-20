using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.IO;

public class URPSetupWizard : EditorWindow
{
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

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("URP Assets created and assigned successfully!");
        EditorUtility.DisplayDialog("URP Setup", "URP Assets created and assigned.\n\nNow, convert your materials: Window > Rendering > Render Pipeline Converter.", "OK");
    }
}
