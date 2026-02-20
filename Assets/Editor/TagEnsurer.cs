using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public class TagEnsurer
{
    static TagEnsurer()
    {
        EnsureTagExists("LevelPart");
    }

    public static void EnsureTagExists(string tagName)
    {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tagsProp = tagManager.FindProperty("tags");

        bool exists = false;
        for (int i = 0; i < tagsProp.arraySize; i++)
        {
            if (tagsProp.GetArrayElementAtIndex(i).stringValue == tagName)
            {
                exists = true;
                break;
            }
        }

        if (!exists)
        {
            tagsProp.InsertArrayElementAtIndex(0);
            tagsProp.GetArrayElementAtIndex(0).stringValue = tagName;
            tagManager.ApplyModifiedProperties();
            Debug.Log("TagEnsurer: Added missing tag '" + tagName + "'");
        }
    }

    [MenuItem("EnemyAI/Fix Missing Tags")]
    public static void ManualFix()
    {
        EnsureTagExists("LevelPart");
        AssetDatabase.Refresh();
    }
}
