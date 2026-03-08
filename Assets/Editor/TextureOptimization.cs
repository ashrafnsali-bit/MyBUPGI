using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class TextureOptimization : EditorWindow
{
    [MenuItem("Tools/Screaming Fix/5. Extreme Texture Optimization")]
    public static void OptimizeAllTextures()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture");
        int count = 0;
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            
            if (importer != null)
            {
                bool changed = false;
                
                // Aggressive cap Max texture size at 512 for low-memory systems
                if (importer.maxTextureSize > 512)
                {
                    importer.maxTextureSize = 512;
                    changed = true;
                }
                
                // Ensure compression is enabled
                if (importer.textureCompression == TextureImporterCompression.Uncompressed)
                {
                    importer.textureCompression = TextureImporterCompression.Compressed;
                    changed = true;
                }
                
                if (changed)
                {
                    importer.SaveAndReimport();
                    count++;
                }
            }
            
            if (count % 20 == 0)
            {
                EditorUtility.DisplayProgressBar("Extreme Optimization", path, (float)count / guids.Length);
            }
        }
        
        EditorUtility.ClearProgressBar();
        Debug.Log($"Successfully optimized {count} textures to 512px.");
        EditorUtility.DisplayDialog("Optimization Complete", $"Capped {count} textures at 512px.\n\nMemory usage during build will be much lower now.", "Thanks!");
    }

    [MenuItem("Tools/Screaming Fix/6. Clean Shader Cache")]
    public static void CleanShaderCache()
    {
        string cachePath = Path.Combine(Directory.GetCurrentDirectory(), "Library", "ShaderCache");
        if (Directory.Exists(cachePath))
        {
            Directory.Delete(cachePath, true);
            Debug.Log("Deleted Shader Cache. Unity will recreate it next time it needs it.");
            EditorUtility.DisplayDialog("Cache Cleaned", "Shader cache deleted to free up memory.", "OK");
        }
        else
        {
            Debug.Log("Shader Cache folder not found. Nothing to delete.");
        }
    }
}
