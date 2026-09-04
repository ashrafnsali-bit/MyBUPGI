using UnityEngine;

public class HealthPickup : MonoBehaviour
{
    public float healAmount = 25f;
    public float rotationSpeed = 90f; // degrees per second
    public AudioClip pickupSound;
    
    void Start()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
        else
        {
            SphereCollider sc = gameObject.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 1f;
        }
    }
    
    void Update()
    {
        // Rotate the pickup for a nice visual effect
        transform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime, Space.World);
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null && playerHealth.currentHealth < playerHealth.maxHealth)
            {
                playerHealth.Heal(healAmount);
                if (pickupSound != null)
                {
                    AudioSource.PlayClipAtPoint(pickupSound, transform.position);
                }
                Destroy(gameObject);
            }
        }
    }
}
