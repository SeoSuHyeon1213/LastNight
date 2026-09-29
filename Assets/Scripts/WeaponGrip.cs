using System;
using UnityEngine;

// 무기 프리팹의 손잡이를 기준으로 손목 위치와 회전을 적용한다.
// PlayerShooter가 조준 정렬 후 호출하므로 별도의 IK 컴포넌트는 필요 없다.
public static class WeaponGrip {
    public static Transform FindMount(Gun weapon, string mountName) {
        if (weapon == null) return null;
        Transform result = null;
        string alternate = mountName.Replace("Handle", "Handel");
        foreach (Transform candidate in weapon.GetComponentsInChildren<Transform>(true)) {
            if (!string.Equals(candidate.name, mountName, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(candidate.name, alternate, StringComparison.OrdinalIgnoreCase)) continue;
            if (result != null) {
                Debug.LogError($"WeaponGrip: {weapon.name}에 {mountName} 기준점이 중복되어 있습니다.", weapon);
                return null;
            }
            result = candidate;
        }
        if (result == null)
            Debug.LogError($"WeaponGrip: {weapon.name}의 자식에 {mountName} 기준점이 필요합니다.", weapon);
        return result;
    }

    public static void ApplyHands(Animator animator, Transform left, Transform right) {
        ApplyHand(animator, AvatarIKGoal.LeftHand, left);
        ApplyHand(animator, AvatarIKGoal.RightHand, right);
    }

    private static void ApplyHand(Animator animator, AvatarIKGoal hand, Transform target) {
        float weight = target != null ? 1f : 0f;
        animator.SetIKPositionWeight(hand, weight);
        animator.SetIKRotationWeight(hand, weight);
        if (target == null) return;
        animator.SetIKPosition(hand, target.position);
        animator.SetIKRotation(hand, target.rotation);
    }
}
