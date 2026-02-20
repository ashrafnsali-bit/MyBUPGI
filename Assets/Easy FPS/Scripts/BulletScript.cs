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
		if (hasDealtDamage) return;
		
		// Use a precise radius for environment and a thicker one for targets
		float precisionRadius = 0.1f; 
		float hitRange = 2.0f; // Check slightly ahead
		
		RaycastHit[] hits = Physics.SphereCastAll(transform.position, precisionRadius, transform.forward, hitRange, ~ignoreLayer, QueryTriggerInteraction.Ignore);
		System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));

		foreach (RaycastHit hit in hits)
		{
			// 1. Ignore shooter and close-range self-hits
			if (hit.transform.gameObject == owner) continue;
			if (hit.distance < 0.1f && hit.transform.root == transform.root) continue;

			// 2. Check for Targets (Player or Enemy)
			PlayerHealth player = hit.transform.GetComponent<PlayerHealth>();
			if (player == null) player = hit.transform.GetComponentInParent<PlayerHealth>();

			if (player != null) {
				hasDealtDamage = true;
				Debug.Log(gameObject.name + " HIT PLAYER: " + player.name);
				player.TakeDamage(damage);
				if (bloodEffect) Instantiate(bloodEffect, hit.point, Quaternion.LookRotation(hit.normal));
				Destroy(gameObject);
				return;
			}
			
			// Enemies only take damage from Player bullets (Friendly Fire OFF)
			if (!isEnemyBullet) {
				EnemyAI enemy = hit.transform.GetComponent<EnemyAI>();
				if (enemy == null) enemy = hit.transform.GetComponentInParent<EnemyAI>();
				
				if (enemy != null) {
					hasDealtDamage = true;
					Debug.Log(gameObject.name + " HIT ENEMY: " + enemy.name);
					enemy.TakeDamage(damage);
					if (bloodEffect) Instantiate(bloodEffect, hit.point, Quaternion.LookRotation(hit.normal));
					Destroy(gameObject);
					return;
				}
			}

			// 3. Check for Solid Objects (Fences, Walls, etc.)
			// We stop if it's not a trigger and not the owner
			if (!hit.collider.isTrigger)
			{
				// Try to show decal if tag exists, otherwise skip without crashing
				if (decalHitWall) {
					bool isLevelPart = false;
					try {
						isLevelPart = hit.transform.CompareTag("LevelPart");
					} catch {
						// Tag LevelPart doesn't exist in Project Settings
					}
					
					if (isLevelPart) {
						Instantiate(decalHitWall, hit.point + hit.normal * floatInfrontOfWall, Quaternion.LookRotation(hit.normal));
					}
				}
				
				Debug.Log(gameObject.name + " hit solid: " + hit.transform.name + " at distance " + hit.distance);
				Destroy(gameObject);
				return;
			}
		}
		
		Destroy(gameObject, 0.1f);
	}
}

