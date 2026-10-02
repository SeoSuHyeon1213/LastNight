using UnityEngine;

// FBX 이벤트 대신 Animator 상태의 재생 구간으로 타격을 동기화한다.
public sealed class MeleeAttackAnimation : StateMachineBehaviour {
    [Range(0f, 1f)] public float hitStart = 0.25f;
    [Range(0f, 1f)] public float hitEnd = 0.65f;
    private MeleeWeapon weapon;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
        PlayerShooter shooter = animator.GetComponent<PlayerShooter>();
        weapon = shooter != null ? shooter.gun as MeleeWeapon : null;
        if (weapon != null) weapon.SetHitWindow(false);
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
        if (weapon == null) return;
        float time = stateInfo.normalizedTime;
        weapon.SetHitWindow(time >= hitStart && time < Mathf.Max(hitStart, hitEnd));
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) {
        if (weapon != null) weapon.FinishAttack();
        weapon = null;
    }
}
