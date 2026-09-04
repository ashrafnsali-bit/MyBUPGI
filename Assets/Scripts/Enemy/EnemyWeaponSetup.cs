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
            // Find the hand bone recursively with flexible naming
            Transform rightHand = FindHandBone(transform);
            
            if (rightHand == null) {
                Debug.LogWarning("EnemyWeaponSetup: Could not find hand bone. Attaching to upper body offset.");
                rightHand = transform;
                if (weaponPositionOffset == Vector3.zero) {
                    weaponPositionOffset = new Vector3(0.25f, 1.2f, 0.4f);
                }
            }
            
            // Instantiate weapon
            spawnedWeapon = Instantiate(weaponPrefab, rightHand);
            spawnedWeapon.transform.localPosition = weaponPositionOffset;
            spawnedWeapon.transform.localRotation = Quaternion.Euler(weaponRotationOffset);
            spawnedWeapon.transform.localScale = Vector3.one * weaponScale;

            // Remove player scripts and physics so weapon stays firmly in hand
            var gunScript = spawnedWeapon.GetComponent<GunScript>();
            if (gunScript != null) Destroy(gunScript);

            var rb = spawnedWeapon.GetComponent<Rigidbody>();
            if (rb != null) Destroy(rb);

            foreach (var col in spawnedWeapon.GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }

            // Find or create FirePoint
            Transform muzzle = spawnedWeapon.transform.Find("Muzzle");
            if (muzzle == null) muzzle = spawnedWeapon.transform.Find("FirePoint");
            
            if (muzzle != null) {
                firePoint = muzzle;
            } else {
                GameObject fp = new GameObject("FirePoint");
                fp.transform.SetParent(spawnedWeapon.transform);
                fp.transform.localPosition = new Vector3(0, 0, 1.5f); 
                fp.transform.localRotation = Quaternion.identity;
                firePoint = fp.transform;
            }
        }
    }

    Transform FindHandBone(Transform root)
    {
        string[] candidates = new string[] {
            "RightHand", "Right_Hand", "Hand_R", "hand_r", "hand.r", 
            "mixamorig:RightHand", "Bip01 R Hand", "bip_hand_R", "R_hand",
            "RightForeArm", "RightArm"
        };

        foreach (string name in candidates)
        {
            Transform found = FindBoneRecursive(root, name);
            if (found != null) return found;
        }

        return null;
    }

    Transform FindBoneRecursive(Transform current, string name)
    {
        if (current.name.ToLower().Contains(name.ToLower())) return current;
        foreach (Transform child in current)
        {
            Transform found = FindBoneRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
