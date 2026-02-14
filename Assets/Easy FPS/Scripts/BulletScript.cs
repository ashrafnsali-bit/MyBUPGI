using UnityEngine;
using System.Collections;

public class BulletScript : MonoBehaviour {

	[Tooltip("Furthest distance bullet will look for target")]
	public float maxDistance = 1000000;
	public float damage = 10;
	public bool isEnemyBullet = false;
	public GameObject owner; 
	RaycastHit hit;
	[Tooltip("Prefab of wall damange hit. The object needs 'LevelPart' tag to create decal on it.")]
	public GameObject decalHitWall;
	[Tooltip("Decal will need to be sligtly infront of the wall so it doesnt cause rendeing problems so for best feel put from 0.01-0.1.")]
	public float floatInfrontOfWall;
	[Tooltip("Blood prefab particle this bullet will create upoon hitting enemy")]
	public GameObject bloodEffect;
	[Tooltip("Put Weapon layer and Player layer to ignore bullet raycast.")]
	public LayerMask ignoreLayer;
	private bool hasDealtDamage = false; // CRITICAL FIX: Prevent multi-hit per bullet

	void Update () {
		// CRITICAL: If this bullet already dealt damage, don't check again
		if (hasDealtDamage) return;
		
		// FIXED: Use SphereCast for thicker bullet detection
		// This helps hit the player even if the aim is slightly off or the bullet is small
		// Use a reasonable distance (5.0f) to ensure we don't skip over targets at high speeds
		float radius = 0.5f; 
		RaycastHit[] hits = Physics.SphereCastAll(transform.position, radius, transform.forward, 5.0f, ~ignoreLayer, QueryTriggerInteraction.Ignore);
		
		// Sort by distance so we hit the closest thing
		System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));

		foreach (RaycastHit hit in hits)
		{
			if (hit.transform.gameObject == owner) continue; // Ignore shooter
			if (hit.distance < 0.1f && hit.transform.root == transform.root) continue; // Ignore self/gun parts (Reduced distance check)

			// Special Check: Did we hit a Player?
			PlayerHealth player = hit.transform.GetComponent<PlayerHealth>();
			if (player == null) player = hit.transform.GetComponentInParent<PlayerHealth>();

			// If we hit SOMETHING, and it's not the owner...
			
			// 1. Check for Player
			if (player != null) {
				hasDealtDamage = true; // Mark as dealt
				Debug.Log("Bullet Hit PLAYER! Applying damage: " + damage);
				player.TakeDamage(damage);
				if (bloodEffect) Instantiate(bloodEffect, hit.point, Quaternion.LookRotation(hit.normal));
				Destroy(gameObject);
				return;
			}
			
			// 2. Check for Enemy (Friendly Fire / Player bullets)
			if (!isEnemyBullet) {
				EnemyAI enemy = hit.transform.GetComponent<EnemyAI>();
				if (enemy == null) enemy = hit.transform.GetComponentInParent<EnemyAI>();
				
				if (enemy != null) {
					hasDealtDamage = true; // Mark as dealt
					enemy.TakeDamage(damage);
					if (bloodEffect) Instantiate(bloodEffect, hit.point, Quaternion.LookRotation(hit.normal));
					Destroy(gameObject);
					return;
				}
			}

			// 3. Walls / Environment
			if(decalHitWall && hit.transform.tag == "LevelPart"){
				Instantiate(decalHitWall, hit.point + hit.normal * floatInfrontOfWall, Quaternion.LookRotation(hit.normal));
				Destroy(gameObject);
				return;
			}
			
			// If we hit something solid that isn't a trigger, stop.
			if (!hit.collider.isTrigger)
			{
				Debug.Log("Bullet hit solid object: " + hit.transform.name);
				Destroy(gameObject);
				return;
			}
		}
		
		Destroy(gameObject, 0.1f);
	}
}

