using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class TextureOptimization : EditorWindow
{
    [MenuItem("Tools/Optimize All Textures")]
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
                
                // Cap max texture size at 1024
                if (importer.maxTextureSize > 1024)
                {
                    importer.maxTextureSize = 1024;
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
            
            if (count % 10 == 0)
            {
                EditorUtility.DisplayProgressBar("Optimizing Textures", path, (float)count / guids.Length);
            }
        }
        
        EditorUtility.ClearProgressBar();
        Debug.Log($"Successfully optimized {count} textures.");
    }
}
