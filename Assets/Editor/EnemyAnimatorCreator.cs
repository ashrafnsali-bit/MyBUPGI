using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;

public class EnemyAnimatorCreator : EditorWindow
{
    [MenuItem("EnemyAI/Fix Enemy Animator")]
    public static void CreateEnemyAnimator()
    {
        // 1. Create Controller
        string path = "Assets/EnemyAnimator.controller";
        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);

        // 2. Add Parameters
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

        // 3. Find Animations
        // UPDATED: Using "Rifle" specific animations for better visuals
        AnimationClip idleClip = FindClip("rifle aiming idle"); // Was generic "Idle"
        if (idleClip == null) idleClip = FindClip("Vanguard By T. Choonyung@Idle"); // Fallback
        
        AnimationClip walkClip = FindClip("Vanguard By T. Choonyung@Walking");
        
        AnimationClip runClip = FindClip("rifle run"); // Was generic "Run"
        if (runClip == null) runClip = FindClip("Vanguard By T. Choonyung@Run"); // Fallback

        AnimationClip shootClip = FindClip("firing rifle"); // Was "Shoot Rifle" - trying "firing rifle" as it might match the idle better
        if (shootClip == null) shootClip = FindClip("Vanguard By T. Choonyung@Shoot Rifle");
        
        AnimationClip hitClip = FindClip("Vanguard By T. Choonyung@Hit Reaction");
        AnimationClip dieClip = FindClip("Vanguard By T. Choonyung@Death");

        // 4. Create States
        var root = controller.layers[0].stateMachine;

        // Blend Tree for Movement (Idle/Walk/Run)
        var moveState = root.AddState("Movement");
        var blendTree = new BlendTree();
        moveState.motion = blendTree;
        blendTree.blendType = BlendTreeType.Simple1D;
        blendTree.blendParameter = "Speed";
        blendTree.AddChild(idleClip, 0);
        blendTree.AddChild(walkClip, 0.5f);
        blendTree.AddChild(runClip, 1.0f);

        // Action States
        var shootState = root.AddState("Shoot");
        shootState.motion = shootClip;

        var hitState = root.AddState("Hit");
        hitState.motion = hitClip;

        var dieState = root.AddState("Die");
        dieState.motion = dieClip;

        // 5. Transitions
        // Any State -> Die
        var anyToDie = root.AddAnyStateTransition(dieState);
        anyToDie.AddCondition(AnimatorConditionMode.If, 0, "Die");

        // Any State -> Hit
        var anyToHit = root.AddAnyStateTransition(hitState);
        anyToHit.AddCondition(AnimatorConditionMode.If, 0, "Hit");
        var hitToMove = hitState.AddTransition(moveState);
        hitToMove.hasExitTime = true;
        hitToMove.exitTime = 0.9f;

        // Movement -> Shoot
        var moveToShoot = moveState.AddTransition(shootState);
        moveToShoot.AddCondition(AnimatorConditionMode.If, 0, "Shoot");
        
        // Shoot -> Movement
        var shootToMove = shootState.AddTransition(moveState);
        shootToMove.hasExitTime = true;
        shootToMove.exitTime = 0.9f;

        // 6. Assign to Prefabs/Scene Objects
        AssignController(controller);

        Debug.Log("Enemy Animator Created at: " + path);
    }

    static AnimationClip FindClip(string namePart)
    {
        string[] guids = AssetDatabase.FindAssets("t:Model " + namePart);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (Object asset in assets)
            {
                if (asset is AnimationClip clip && !clip.name.Contains("__preview__"))
                {
                    return clip;
                }
            }
        }
        Debug.LogWarning("Could not find clip with name part: " + namePart);
        return null;
    }

    static void AssignController(RuntimeAnimatorController controller)
    {
        // 5. Assign to all EnemyAI in scene
        var enemies = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            var animator = enemy.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.runtimeAnimatorController = controller;
                Debug.Log("Assigned Animator to: " + enemy.name);
            }
        }
    }
}
