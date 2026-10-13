using System.Collections.Generic;
using UnityEngine;

public class AnimatorController : StateMachineBehaviour
{
    [Tooltip("OnAttackHit 이벤트가 없는 클립의 타격 시점(전체 재생 구간의 비율)")]
    [SerializeField, Range(0.05f, 0.95f)] private float fallbackHitTime = 0.4f;

    private Zombie zombie;
    private bool clipChecked;
    private bool usesHitEvent;
    private bool fallbackHitSent;
    private readonly List<AnimatorClipInfo> clips = new List<AnimatorClipInfo>();

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        zombie = animator.GetComponent<Zombie>();
        clipChecked = false;
        usesHitEvent = false;
        fallbackHitSent = false;
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (zombie == null || zombie.dead || !zombie.isActiveAndEnabled || Time.timeScale <= 0f)
            return;

        if (!clipChecked)
        {
            // 진입 전환 중에는 Current가 이전 상태이므로 공격 클립은 Next에서 읽는다.
            if (animator.GetCurrentAnimatorStateInfo(layerIndex).fullPathHash == stateInfo.fullPathHash)
                animator.GetCurrentAnimatorClipInfo(layerIndex, clips);
            else if (animator.IsInTransition(layerIndex) &&
                     animator.GetNextAnimatorStateInfo(layerIndex).fullPathHash == stateInfo.fullPathHash)
                animator.GetNextAnimatorClipInfo(layerIndex, clips);
            else
                return;

            if (clips.Count == 0) return;
            foreach (AnimatorClipInfo info in clips)
            {
                if (info.clip == null) continue;
                foreach (AnimationEvent animationEvent in info.clip.events)
                    if (animationEvent.functionName == nameof(Zombie.OnAttackHit))
                        usesHitEvent = true;
            }
            clipChecked = true;
        }

        if (usesHitEvent || fallbackHitSent || stateInfo.normalizedTime < fallbackHitTime)
            return;

        fallbackHitSent = true;
        zombie.OnAttackHit();
    }

    public override void OnStateExit(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex)
    {
        if (zombie == null) zombie = animator.GetComponent<Zombie>();

        if (zombie != null)
        {
            zombie.OnAttackFinished();
        }
    }
}
