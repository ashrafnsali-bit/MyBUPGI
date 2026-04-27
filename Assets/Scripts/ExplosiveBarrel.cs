using UnityEngine;

public class ExplosiveBarrel : MonoBehaviour
{
    public float health = 30f;
    public float explosionRadius = 5f;
    public float explosionDamage = 50f;
    public float explosionForce = 1000f;
    public GameObject explosionEffect;
    public AudioClip explosionSound;
    
    private bool exploded = false;

    // Called via SendMessage from BulletScript
    public void TakeDamage(float amount)
    {
        if (exploded) return;

        health -= amount;
        if (health <= 0)
        {
            Explode();
        }
    }

    void Explode()
    {
        exploded = true;
        
        // Effects
        if (explosionEffect) Instantiate(explosionEffect, transform.position, Quaternion.identity);
        if (explosionSound) AudioSource.PlayClipAtPoint(explosionSound, transform.position);
        
        // Camera Shake
        if (CameraShake.instance != null) CameraShake.instance.TriggerShake(0.5f, 0.5f);
        
        // Damage nearby objects
        Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (Collider col in colliders)
        {
            // Damage Enemies
            EnemyAI enemy = col.GetComponentInParent<EnemyAI>();
            if (enemy != null)
            {
                enemy.TakeDamage(explosionDamage);
                FloatingDamage.Create(enemy.transform.position, (int)explosionDamage);
            }
            
            // Damage Player
            PlayerHealth player = col.GetComponentInParent<PlayerHealth>();
            if (player != null)
            {
                player.TakeDamage(explosionDamage);
            }
            
            // Chain reactions (other barrels)
            ExplosiveBarrel barrel = col.GetComponent<ExplosiveBarrel>();
            if (barrel != null && barrel != this)
            {
                barrel.TakeDamage(explosionDamage);
            }

            // Apply physics force
            Rigidbody rb = col.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddExplosionForce(explosionForce, transform.position, explosionRadius);
            }
        }
        
        Destroy(gameObject);
    }
}
