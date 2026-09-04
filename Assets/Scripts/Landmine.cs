using UnityEngine;

public class Landmine : MonoBehaviour
{
    public float explosionRadius = 6f;
    public float explosionDamage = 100f;
    public float explosionForce = 1500f;
    
    public GameObject explosionEffect;
    public AudioClip explosionSound;
    public float setupTime = 2f; // Time before it becomes active
    
    private bool isActive = false;
    private bool hasExploded = false;
    private float timer = 0f;

    void Start()
    {
        // Try to find explosion effect and sound from the barrel if not assigned
        if (explosionEffect == null || explosionSound == null)
        {
            ExplosiveBarrel barrel = Object.FindFirstObjectByType<ExplosiveBarrel>();
            if (barrel != null)
            {
                if (explosionEffect == null) explosionEffect = barrel.explosionEffect;
                if (explosionSound == null) explosionSound = barrel.explosionSound;
            }
        }
        
        // Ensure there is a trigger collider
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
        else
        {
            SphereCollider sc = gameObject.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 2f; // Trigger radius
        }
    }

    void Update()
    {
        if (!isActive)
        {
            timer += Time.deltaTime;
            if (timer >= setupTime)
            {
                isActive = true;
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (isActive && !hasExploded)
        {
            // If player or enemy steps on it
            if (other.CompareTag("Player") || other.GetComponentInParent<EnemyAI>() != null || other.CompareTag("Enemy"))
            {
                Explode();
            }
        }
    }

    void Explode()
    {
        hasExploded = true;

        if (explosionEffect != null) Instantiate(explosionEffect, transform.position, transform.rotation);
        
        if (explosionSound != null)
        {
            AudioSource.PlayClipAtPoint(explosionSound, transform.position, 1.0f);
        }
        
        if (CameraShake.instance != null) CameraShake.instance.TriggerShake(1.5f, 1.0f);

        System.Collections.Generic.HashSet<GameObject> hitObjects = new System.Collections.Generic.HashSet<GameObject>();
        Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRadius);
        
        foreach (Collider col in colliders)
        {
            Transform root = col.transform.root;
            if (hitObjects.Contains(root.gameObject)) continue;
            
            bool hitSomething = false;

            // Damage Enemy
            EnemyAI enemy = col.GetComponentInParent<EnemyAI>();
            if (enemy != null) { 
                enemy.TakeDamage(explosionDamage); 
                FloatingDamage.Create(enemy.transform.position, (int)explosionDamage); 
                hitSomething = true;
            }
            
            // Damage Player
            PlayerHealth player = col.GetComponentInParent<PlayerHealth>();
            if (player != null && !hitSomething) {
                player.TakeDamage(explosionDamage * 0.8f);
                hitSomething = true;
            }
            
            // Trigger other barrels or landmines
            ExplosiveBarrel barrel = col.GetComponent<ExplosiveBarrel>();
            if (barrel != null && !hitSomething) {
                barrel.TakeDamage(explosionDamage);
                hitSomething = true;
            }
            
            Landmine otherMine = col.GetComponent<Landmine>();
            if (otherMine != null && otherMine != this && !otherMine.hasExploded) {
                otherMine.Invoke("Explode", Random.Range(0.1f, 0.3f)); // Chain reaction
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

        // Hide visuals immediately, destroy after sound finishes if needed, or destroy now since we used PlayClipAtPoint
        Destroy(gameObject);
    }
}
