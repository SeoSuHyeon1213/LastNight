using UnityEngine;

public class AnimatorController : StateMachineBehaviour
{    
    public override void OnStateExit(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex)
    {
        Zombie zombie = animator.GetComponent<Zombie>();

        if (zombie != null)
        {
            zombie.OnAttackFinished();
        }
    }
}
