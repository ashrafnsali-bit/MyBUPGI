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

        // CRITICAL FIX: If there are no explosive barrels in the scene, the bomb will have no sound!
        // We must ensure there is always an explosion sound.
        if (explosionSound == null)
        {
            // Search all loaded AudioClips for anything sounding like an explosion or shot
            AudioClip[] allClips = Resources.FindObjectsOfTypeAll<AudioClip>();
            foreach (AudioClip clip in allClips) {
                if (clip.name.ToLower().Contains("expl")) {
                    explosionSound = clip;
                    break;
                }
            }
            
            // Fallback: If no explosion sound exists, use the Gun Shot sound! 
            // When we pitch it down drastically, it will sound exactly like a heavy explosion.
            if (explosionSound == null) {
                foreach (AudioClip clip in allClips) {
                    if (clip.name.ToLower().Contains("shot")) {
                        explosionSound = clip;
                        break;
                    }
                }
            }
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
        
        if (explosionSound != null) {
            // Create a devastating explosion sound effect by layering multiple 2D sounds
            GameObject audioObj = new GameObject("MassiveExplosionAudio");
            audioObj.transform.position = transform.position;
            
            // Layer 1: Core Boom (Fully 2D for max loudness in player's ears)
            AudioSource src = audioObj.AddComponent<AudioSource>();
            src.clip = explosionSound;
            src.volume = 1.0f; 
            src.spatialBlend = 0.0f; // 100% 2D - Ignores distance, MAXIMUM LOUDNESS
            src.pitch = Random.Range(0.7f, 0.8f); 
            src.Play();
            
            // Layer 2: Deep Bass Rumble
            AudioSource src2 = audioObj.AddComponent<AudioSource>();
            src2.clip = explosionSound;
            src2.volume = 1.0f;
            src2.spatialBlend = 0.0f;
            src2.pitch = 0.4f; // Extreme low pitch for bass
            src2.PlayDelayed(0.02f);
            
            // Layer 3: High Impact Crack
            AudioSource src3 = audioObj.AddComponent<AudioSource>();
            src3.clip = explosionSound;
            src3.volume = 0.8f;
            src3.spatialBlend = 0.0f;
            src3.pitch = 1.2f; // High pitch for the initial explosive crack
            src3.PlayDelayed(0.01f);

            Destroy(audioObj, explosionSound.length + 1f);
        }
        
        if (CameraShake.instance != null) CameraShake.instance.TriggerShake(1.5f, 1.0f); // Extreme screen shake

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
