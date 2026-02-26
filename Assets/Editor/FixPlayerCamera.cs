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

    [MenuItem("Tools/Screaming Fix/3. Final Camera Fix")]
    public static void FixAll()
    {
        Debug.Log("Starting Final Camera Fix...");

        // 0. Ensure URP is active and materials are converted
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
            Debug.Log("Fixed Player Prefab.");
        }

        // 2. Fix Scene
        GameObject scenePlayer = GameObject.FindGameObjectWithTag("Player");
        if (scenePlayer != null)
        {
            FixCamerasInObject(scenePlayer);
            Debug.Log("Fixed Player in Scene.");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        EditorUtility.DisplayDialog("Camera Fix", "Player cameras configured. Materials checked.\n\nCheck the Simulator now!", "OK");
    }

    private static void FixGraphicsSettings()
    {
        UniversalRenderPipelineAsset urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/URP-Main-Asset.asset");
        if (urpAsset != null)
        {
            GraphicsSettings.defaultRenderPipeline = urpAsset;
            
            string[] names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i);
                QualitySettings.renderPipeline = urpAsset;
            }
            Debug.Log("Assigned URP Asset to all Quality Levels.");
        }
    }

    private static void ConvertMaterials()
    {
        Debug.Log("Checking materials for URP compatibility...");
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogError("Could not find URP Lit shader!");
            return;
        }

        int count = 0;
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            string shaderName = mat.shader != null ? mat.shader.name : "null";
            
            // Convert if shader is null, Standard, or has internal errors
            bool needsFix = mat.shader == null || 
                            shaderName == "Standard" || 
                            shaderName.Contains("InternalError") || 
                            shaderName.Contains("Hidden/InternalErrorShader") ||
                            shaderName == "Autodesk Interactive"; // Often used in imported assets

            if (needsFix)
            {
                Debug.Log($"Converting material to URP Lit: {mat.name} (was {shaderName})");
                mat.shader = urpLit;
                EditorUtility.SetDirty(mat);
                count++;
            }
        }
        Debug.Log($"Material check complete. Converted {count} materials.");
    }

    private static void FixCamerasInObject(GameObject root)
    {
        Camera mainCam = null;
        Camera secondCam = null;

        Camera[] cams = root.GetComponentsInChildren<Camera>(true);
        foreach (var cam in cams)
        {
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data == null)
            {
                data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            if (cam.gameObject.name.ToLower().Contains("main"))
                mainCam = cam;
            else if (cam.gameObject.name.ToLower().Contains("second") || cam.gameObject.name.ToLower().Contains("weapon"))
                secondCam = cam;
        }

        if (mainCam != null)
        {
            var mainData = mainCam.GetComponent<UniversalAdditionalCameraData>();
            SerializedObject soMain = new SerializedObject(mainData);
            soMain.FindProperty("m_CameraType").intValue = (int)CameraRenderType.Base;
            soMain.FindProperty("m_RendererIndex").intValue = 0;
            
            mainCam.enabled = true;
            mainCam.gameObject.SetActive(true);
            mainCam.cullingMask |= (1 << 0);
            
            // Disable other base cameras in scene
            Camera[] allCams = GameObject.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach(var c in allCams) {
                if (c == mainCam || c == secondCam) continue;
                if (c.transform.IsChildOf(mainCam.transform.root)) continue;
                
                var d = c.GetComponent<UniversalAdditionalCameraData>();
                if (d != null && d.renderType == CameraRenderType.Base) {
                    c.enabled = false;
                }
            }

            if (secondCam != null)
            {
                var secondData = secondCam.GetComponent<UniversalAdditionalCameraData>();
                SerializedObject soSecond = new SerializedObject(secondData);
                soSecond.FindProperty("m_CameraType").intValue = (int)CameraRenderType.Overlay;
                soSecond.FindProperty("m_RendererIndex").intValue = 0;
                soSecond.ApplyModifiedProperties();
                
                secondCam.enabled = true;
                secondCam.gameObject.SetActive(true);
                
                SerializedProperty stack = soMain.FindProperty("m_Cameras");
                bool exists = false;
                for (int i = 0; i < stack.arraySize; i++)
                {
                    if (stack.GetArrayElementAtIndex(i).objectReferenceValue == secondCam)
                    {
                        exists = true;
                        break;
                    }
                }
                
                if (!exists)
                {
                    stack.InsertArrayElementAtIndex(stack.arraySize);
                    stack.GetArrayElementAtIndex(stack.arraySize - 1).objectReferenceValue = secondCam;
                }
            }
            soMain.ApplyModifiedProperties();
            EditorUtility.SetDirty(mainCam.gameObject);
            if (PrefabUtility.IsPartOfAnyPrefab(mainCam.gameObject))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(mainData);
                PrefabUtility.RecordPrefabInstancePropertyModifications(mainCam);
            }
        }
    }
}
