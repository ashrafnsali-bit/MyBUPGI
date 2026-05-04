using UnityEngine;

public class Grenade : MonoBehaviour
{
    public float delay = 3f;
    public float explosionRadius = 7f;
    public float explosionDamage = 100f;
    public float explosionForce = 1500f;

    private GameObject explosionEffect;
    private AudioClip explosionSound;
    private float countdown;
    private bool hasExploded = false;

    void Start()
    {
        countdown = delay;
        
        ExplosiveBarrel barrel = Object.FindFirstObjectByType<ExplosiveBarrel>();
        if (barrel != null)
        {
            explosionEffect = barrel.explosionEffect;
            explosionSound = barrel.explosionSound;
        }
    }

    void Update()
    {
        countdown -= Time.deltaTime;
        if (countdown <= 0f && !hasExploded)
        {
            Explode();
        }
    }

    void Explode()
    {
        hasExploded = true;

        if (explosionEffect != null) Instantiate(explosionEffect, transform.position, transform.rotation);
        if (explosionSound != null) AudioSource.PlayClipAtPoint(explosionSound, transform.position);
        
        if (CameraShake.instance != null) CameraShake.instance.TriggerShake(0.6f, 0.5f);

        // Keep track of hit objects to avoid doing multiple times damage to same entity (if it has multiple colliders)
        System.Collections.Generic.HashSet<GameObject> hitObjects = new System.Collections.Generic.HashSet<GameObject>();

        Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (Collider col in colliders)
        {
            Transform root = col.transform.root;
            if (hitObjects.Contains(root.gameObject)) continue;
            
            bool hitSomething = false;

            // Check for enemy
            EnemyAI enemy = col.GetComponentInParent<EnemyAI>();
            if (enemy != null) { 
                enemy.TakeDamage(explosionDamage); 
                FloatingDamage.Create(enemy.transform.position, (int)explosionDamage); 
                hitSomething = true;
            }
            // Fallback for dummies or other enemies without EnemyAI script
            else if (col.transform.tag == "Enemy" || col.transform.tag == "Dummie")
            {
                col.transform.SendMessage("TakeDamage", explosionDamage, SendMessageOptions.DontRequireReceiver);
                FloatingDamage.Create(col.transform.position, (int)explosionDamage);
                hitSomething = true;
            }
            
            // Check for player
            PlayerHealth player = col.GetComponentInParent<PlayerHealth>();
            if (player != null && !hitSomething) {
                player.TakeDamage(explosionDamage * 0.5f);
                hitSomething = true;
            }
            
            // Check for explosive barrels
            ExplosiveBarrel barrel = col.GetComponent<ExplosiveBarrel>();
            if (barrel != null && !hitSomething) {
                barrel.TakeDamage(explosionDamage);
                hitSomething = true;
            }

            // Apply physics force
            Rigidbody rb = col.GetComponent<Rigidbody>();
            if (rb != null) {
                rb.AddExplosionForce(explosionForce, transform.position, explosionRadius);
            }

            if (hitSomething || rb != null) {
                hitObjects.Add(root.gameObject);
            }
        }

        Destroy(gameObject);
    }
}
