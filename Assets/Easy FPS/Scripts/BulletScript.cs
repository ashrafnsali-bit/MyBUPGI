using UnityEngine;
using System.Collections;

public class BulletScript : MonoBehaviour {

	[Tooltip("Furthest distance bullet will look for target")]
	public float maxDistance = 1000000;
	public float damage = 10;
	public bool isEnemyBullet = false;
	public GameObject owner; 
	RaycastHit hit;
	
	[Header("Penetration Settings")]
	[Tooltip("How many solid objects the bullet can pass through before being destroyed.")]
	public int maxPenetrations = 0;
	[Tooltip("Multiplier applied to damage after each penetration (e.g. 0.5 means damage is halved).")]
	public float damageFalloff = 0.5f;
	private int currentPenetrations = 0;
	[Tooltip("Prefab of wall damange hit. The object needs 'LevelPart' tag to create decal on it.")]
	public GameObject decalHitWall;
	[Tooltip("Decal will need to be sligtly infront of the wall so it doesnt cause rendeing problems so for best feel put from 0.01-0.1.")]
	public float floatInfrontOfWall;
	[Tooltip("Blood prefab particle this bullet will create upoon hitting enemy")]
	public GameObject bloodEffect;
	[Tooltip("Put Weapon layer and Player layer to ignore bullet raycast.")]
	public LayerMask ignoreLayer;
	// DIAGNOSTIC: Debug mode to show what bullets are hitting
	public bool debugBullets = true;
	private bool hasDealtDamage = false; 


	void Start() {
		// DIAGNOSTIC: Verify ignore layer
		if (debugBullets) Debug.Log(gameObject.name + " spawned. IgnoreLayer Mask: " + ignoreLayer.value);
		Destroy(gameObject, 5.0f);
	}

	void Update () {
		if (hasDealtDamage) return;
		
		// Use a precise radius for environment and a thicker one for targets
		float precisionRadius = 0.1f; 
		
		float hitRange = 5.0f;
		Rigidbody rb = GetComponent<Rigidbody>();
		if (rb != null) {
			// Ensure we check far enough ahead to cover the distance traveled this frame, plus a buffer
			hitRange = Mathf.Max(hitRange, rb.linearVelocity.magnitude * Time.deltaTime * 1.5f);
		}
		
		// HIT DETECTION: We check everything EXCEPT the ignoreLayer, 
		// BUT we must ENSURE the Player and Enemy layers are NOT ignored if they are targets.
		LayerMask detectionMask = ~ignoreLayer;
		
		// Force include Player and Enemy layers ONLY if they exist
		int playerLayer = LayerMask.NameToLayer("Player");
		int enemyLayer = LayerMask.NameToLayer("Enemy");
		if (playerLayer != -1) detectionMask |= (1 << playerLayer);
		if (enemyLayer != -1) detectionMask |= (1 << enemyLayer);

		// Also ensure Default and Ignore Raycast are NOT the ONLY things we have
		if (detectionMask.value == 0) {
			detectionMask = LayerMask.GetMask("Default", "Enemy", "Player");
			if (debugBullets) Debug.LogWarning("Bullet detectionMask was 0! Resetting to Default/Enemy/Player.");
		}

		RaycastHit[] hits = Physics.SphereCastAll(transform.position, precisionRadius, transform.forward, hitRange, detectionMask, QueryTriggerInteraction.Collide);
		
		if (hits.Length > 0)
		{
			System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));

			foreach (RaycastHit hit in hits)
			{
				// 1. Ignore shooter and close-range self-hits
				if (owner != null) {
					if (hit.transform.IsChildOf(owner.transform)) continue;
				}
				
				// Prevent hitting the bullet itself
				if (hit.transform == transform) continue;

				if (debugBullets) Debug.Log(gameObject.name + " detected hit on: " + hit.transform.name + " (Layer: " + LayerMask.LayerToName(hit.transform.gameObject.layer) + ")");

				// 2. Check for Targets (Player or Enemy)
				// Use GetComponentInParent to handle hits on child colliders (limbs, etc.)
				PlayerHealth player = hit.transform.GetComponentInParent<PlayerHealth>();
				EnemyAI enemy = hit.transform.GetComponentInParent<EnemyAI>();

				// TEAM FILTERING:
				// If this is an Enemy Bullet, it can only hit the Player.
				// If this is a Player Bullet, it can only hit Enemies.
				if (isEnemyBullet) {
					// Enemy bullets skip hitting other enemies
					if (enemy != null) {
						if (debugBullets) Debug.Log(gameObject.name + " (Enemy Bullet) ignoring ally: " + enemy.name);
						continue; // Skip damage and keep bullet flying
					}
					
					if (player != null) {
						hasDealtDamage = true;
						if (debugBullets) Debug.Log(gameObject.name + " (Enemy Bullet) HIT PLAYER: " + player.name);
						Vector3 sourcePos = owner != null ? owner.transform.position : transform.position;
						player.TakeDamage(damage, sourcePos);
						if (bloodEffect) Instantiate(bloodEffect, hit.point, Quaternion.LookRotation(hit.normal));
						Destroy(gameObject);
						return;
					}
				} else {
					// Player bullets skip hitting the player
					if (player != null) {
						// Usually handled by Owner check (IsChildOf), but this is an extra layer of safety.
						if (debugBullets) Debug.Log(gameObject.name + " (Player Bullet) ignoring self/player: " + player.name);
						continue; 
					}

					if (enemy != null) {
						hasDealtDamage = true;
						if (debugBullets) Debug.Log(gameObject.name + " (Player Bullet) HIT ENEMY: " + enemy.name);
						enemy.TakeDamage(damage);
						if (bloodEffect) Instantiate(bloodEffect, hit.point, Quaternion.LookRotation(hit.normal));
						
						// NEW: UI Feedback
						FloatingDamage.Create(hit.point, (int)damage);
						if (UIManager.instance != null) UIManager.instance.ShowHitMarker();
						GunScript.HitMarkerSound();

						Destroy(gameObject);
						return;
					}
				} 
				
				// FALLBACK: If tagged Enemy but no EnemyAI, try to find ANY TakeDamage method or just log loudly
				bool isEnemyTarget = false;
				try {
					isEnemyTarget = hit.transform.tag == "Enemy" || hit.transform.tag == "Dummie" || hit.transform.tag == "ExplosiveBarrel";
				} catch { }

				if (isEnemyTarget) {
					Debug.LogWarning("Bullet hit an object tagged '" + hit.transform.tag + "' (" + hit.transform.name + ") but it has no EnemyAI component! Checking for other damageable components...");
					// Some objects might have health scripts named differently
					hit.transform.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
					
					// If we sent message, we consider it a hit
					hasDealtDamage = true;
					if (bloodEffect && hit.transform.tag != "ExplosiveBarrel") Instantiate(bloodEffect, hit.point, Quaternion.LookRotation(hit.normal));
					
					// NEW: UI Feedback
					FloatingDamage.Create(hit.point, (int)damage);
					if (UIManager.instance != null) UIManager.instance.ShowHitMarker();
					GunScript.HitMarkerSound();

					Destroy(gameObject);
					return;
				}

				// 3. Check for Solid Objects (Fences, Walls, etc.)
				// We stop if it's not a trigger
				if (!hit.collider.isTrigger)
				{
					// Try to show decal if tag exists, otherwise skip without crashing
					if (decalHitWall) {
						bool isLevelPart = false;
						try {
							isLevelPart = hit.transform.tag == "LevelPart";
						} catch { }
						
						if (isLevelPart) {
							Instantiate(decalHitWall, hit.point + hit.normal * floatInfrontOfWall, Quaternion.LookRotation(hit.normal));
						}
					}
					
					// Enemy bullets NEVER penetrate solid obstacles or containers
					if (isEnemyBullet)
					{
						if (debugBullets) Debug.Log(gameObject.name + " (Enemy Bullet) blocked by solid obstacle: " + hit.transform.name);
						Destroy(gameObject);
						return;
					}

					// Penetration Logic (for player bullets)
					bool isPenetrable = false;
					try {
						string objName = hit.transform.name.ToLower();
						isPenetrable = objName.Contains("container") || objName.Contains("hangar") || objName.Contains("oil_tank") || hit.transform.tag == "Container";
					} catch { }

					if (isPenetrable || currentPenetrations < maxPenetrations) {
						if (!isPenetrable) currentPenetrations++; // Only count towards limit if it's a normal wall
						// Don't reduce damage heavily for penetrable objects to make sure enemies inside can be killed
						if (!isPenetrable) damage *= damageFalloff; 
						
						if (debugBullets) Debug.Log(gameObject.name + " penetrated solid environment: " + hit.transform.name);
						
						// FIX: Ignore physical collision so the bullet continues flying through the wall
						Collider myCollider = GetComponent<Collider>();
						if (myCollider != null && hit.collider != null) {
							Physics.IgnoreCollision(myCollider, hit.collider);
						}
						
						continue; // Continue checking other hits in the SphereCast
					} else {
						if (debugBullets) Debug.Log(gameObject.name + " hit solid environment: " + hit.transform.name + " at distance " + hit.distance);
						Destroy(gameObject);
						return;
					}
				}
				else if (debugBullets) 
				{
					Debug.Log(gameObject.name + " passed through trigger: " + hit.transform.name);
				}
			}
		}
	}
}

