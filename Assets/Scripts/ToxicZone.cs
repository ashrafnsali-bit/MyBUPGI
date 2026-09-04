using UnityEngine;

public class ToxicZone : MonoBehaviour
{
    public float damagePerSecond = 15f;
    public float tickRate = 0.5f; // Apply damage every 0.5 seconds
    
    // Optional visual effects
    public ParticleSystem toxicGasEffect;
    
    private float nextTickTime;

    void Start()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
        else
        {
            BoxCollider bc = gameObject.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(5, 2, 5); // Default size
        }

        if (toxicGasEffect != null && !toxicGasEffect.isPlaying)
        {
            toxicGasEffect.Play();
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (Time.time >= nextTickTime)
        {
            bool dealtDamage = false;

            // Damage Player
            if (other.CompareTag("Player"))
            {
                PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
                if (player != null)
                {
                    player.TakeDamage(damagePerSecond * tickRate); // Scale damage by tick rate
                    dealtDamage = true;
                }
            }
            // Damage Enemy
            else if (other.CompareTag("Enemy") || other.GetComponentInParent<EnemyAI>() != null)
            {
                EnemyAI enemy = other.GetComponentInParent<EnemyAI>();
                if (enemy != null)
                {
                    enemy.TakeDamage(damagePerSecond * tickRate);
                    FloatingDamage.Create(enemy.transform.position, (int)(damagePerSecond * tickRate));
                    dealtDamage = true;
                }
            }

            if (dealtDamage)
            {
                nextTickTime = Time.time + tickRate;
            }
        }
    }
}
