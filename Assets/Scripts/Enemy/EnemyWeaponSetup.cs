using UnityEngine;

public class EnemyWeaponSetup : MonoBehaviour
{
    public GameObject weaponPrefab;
    public Vector3 weaponPositionOffset = new Vector3(0, 0, 0);
    public Vector3 weaponRotationOffset = new Vector3(0, 0, 0);
    public float weaponScale = 1f;
    public string rightHandBoneName = "mixamorig:RightHand";
    public GameObject spawnedWeapon;
    public Transform firePoint;

    void Start()
    {
        if (weaponPrefab == null) {
             // Try to load a default weapon if none assigned
             weaponPrefab = Resources.Load<GameObject>("NewGun_auto");
             if (weaponPrefab == null) Debug.LogWarning("EnemyWeaponSetup: No weapon prefab assigned and could not load default 'NewGun_auto'.");
        }

        if (weaponPrefab != null)
        {
            // Find the hand bone recursively
            Transform rightHand = FindBone(transform, rightHandBoneName);
            
            if (rightHand == null) {
                Debug.LogWarning("EnemyWeaponSetup: Could not find bone '" + rightHandBoneName + "'. Attaching to root.");
                rightHand = transform;
            }
            // Instantiate weapon
            spawnedWeapon = Instantiate(weaponPrefab, rightHand);
            spawnedWeapon.transform.localPosition = weaponPositionOffset;
            spawnedWeapon.transform.localRotation = Quaternion.Euler(weaponRotationOffset);
            spawnedWeapon.transform.localScale = Vector3.one * weaponScale;

            // Remove player scripts from enemy weapon
            var gunScript = spawnedWeapon.GetComponent<GunScript>();
            if (gunScript != null) Destroy(gunScript);

            // Find or create FirePoint
            Transform muzzle = spawnedWeapon.transform.Find("Muzzle");
            if (muzzle == null) muzzle = spawnedWeapon.transform.Find("FirePoint");
            
            if (muzzle != null) {
                firePoint = muzzle;
            } else {
                GameObject fp = new GameObject("FirePoint");
                fp.transform.SetParent(spawnedWeapon.transform);
                // Increased Z to 1.5f to ensure it spawns outside the enemy collider
                fp.transform.localPosition = new Vector3(0, 0, 1.5f); 
                fp.transform.localRotation = Quaternion.identity;
                firePoint = fp.transform;
            }
        }
    }

    Transform FindBone(Transform current, string name)
    {
        if (current.name.Contains(name)) return current;
        foreach (Transform child in current)
        {
            Transform found = FindBone(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
