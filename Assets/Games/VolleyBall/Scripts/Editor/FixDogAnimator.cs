using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Linq;

public class FixDogAnimator : MonoBehaviour
{
    [MenuItem("Tools/Fix Dog Animator")]
    public static void FixAnimator()
    {
        string path = "Assets/Games/VolleyBall/Animator/Opp Dog Player.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null)
        {
            Debug.LogError("Could not find AnimatorController at " + path);
            return;
        }

        AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;

        AnimatorState idleState = rootStateMachine.states.FirstOrDefault(s => s.state.name == "Dog Idle").state;
        if (idleState == null)
        {
            Debug.LogError("Could not find 'Dog Idle' state. Make sure it is spelled exactly like that.");
            return;
        }

        string[] hitStateNames = { "Dog Success", "Dog Kick", "Dog Throw", "Dog Damage" };
        
        // Ensure parameters exist
        if (!controller.parameters.Any(p => p.name == "HitTrigger"))
            controller.AddParameter("HitTrigger", AnimatorControllerParameterType.Trigger);
        if (!controller.parameters.Any(p => p.name == "HitIndex"))
            controller.AddParameter("HitIndex", AnimatorControllerParameterType.Int);

        for (int i = 0; i < hitStateNames.Length; i++)
        {
            string stateName = hitStateNames[i];
            var childState = rootStateMachine.states.FirstOrDefault(s => s.state.name == stateName);
            if (childState.state != null)
            {
                AnimatorState state = childState.state;
                
                // 1. Fix AnyState to this State transitions
                var anyStateTransitions = rootStateMachine.anyStateTransitions;
                var filteredTransitions = anyStateTransitions.Where(t => t.destinationState != state).ToArray();
                rootStateMachine.anyStateTransitions = filteredTransitions;

                AnimatorStateTransition anyTrans = rootStateMachine.AddAnyStateTransition(state);
                anyTrans.AddCondition(AnimatorConditionMode.If, 0, "HitTrigger");
                anyTrans.AddCondition(AnimatorConditionMode.Equals, i, "HitIndex");
                anyTrans.hasExitTime = false;
                anyTrans.duration = 0.1f;
                anyTrans.canTransitionToSelf = false;

                // 2. Fix return to Idle transitions
                state.transitions = new AnimatorStateTransition[0]; // Clear bad transitions
                
                AnimatorStateTransition returnTrans = state.AddTransition(idleState);
                returnTrans.hasExitTime = true;
                returnTrans.exitTime = 0.85f; // Wait until animation is 85% done
                returnTrans.duration = 0.15f; // Smooth blend back to idle
            }
        }
        
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("<color=green><b>Dog Animator Fixed successfully!</b></color> All AnyState conditions and Return to Idle transitions have been wired up perfectly by the AI.");
    }
}
