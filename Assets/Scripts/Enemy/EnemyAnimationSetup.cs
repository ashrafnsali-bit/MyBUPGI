using UnityEngine;

public class EnemyAnimationSetup : MonoBehaviour
{
    public AnimationClip idleClip;
    public AnimationClip walkClip;
    public AnimationClip runClip;
    public AnimationClip shootClip;
    public AnimationClip aimClip;
    public AnimationClip hitClip;
    public AnimationClip deathClip;
    public bool createNewController = true;
    public string controllerName = "EnemyAnimator";
    public bool setupOnStart = true;

    void Awake()
    {
        if (!setupOnStart) return;

        Animator anim = GetComponent<Animator>();
        if (anim == null) anim = gameObject.AddComponent<Animator>();

        // Note: Creating an AnimatorController at runtime is complex and requires UnityEditor namespace.
        // For a runtime script, we assume the user will manually create a controller or we use a basic one if pre-assigned.
        Debug.Log("EnemyAnimationSetup: Animator checked for " + gameObject.name);
    }
}
