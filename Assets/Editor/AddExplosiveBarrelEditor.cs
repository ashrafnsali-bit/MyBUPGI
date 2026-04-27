using UnityEngine;
using UnityEditor;

public class AddExplosiveBarrelEditor
{
    [MenuItem("Tools/Add Explosive Barrel")]
    public static void AddExplosiveBarrel()
    {
        GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        barrel.name = "ExplosiveBarrel";
        barrel.tag = "ExplosiveBarrel";
        
        // Setup visual size
        barrel.transform.localScale = new Vector3(1f, 1.2f, 1f);
        
        // Give it a red color if material exists, or just red material
        Renderer rend = barrel.GetComponent<Renderer>();
        if (rend != null)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = Color.red;
            rend.material = mat;
        }

        // Add physical properties
        Rigidbody rb = barrel.AddComponent<Rigidbody>();
        rb.mass = 50f;

        // Add script
        barrel.AddComponent<ExplosiveBarrel>();

        // Place in front of player or at origin
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            barrel.transform.position = player.transform.position + player.transform.forward * 5f + Vector3.up * 1f;
        }
        else
        {
            barrel.transform.position = new Vector3(0, 1.2f, 0);
        }

        Undo.RegisterCreatedObjectUndo(barrel, "Add Explosive Barrel");
        Debug.Log("Successfully added an Explosive Barrel to the scene.");
    }
}
