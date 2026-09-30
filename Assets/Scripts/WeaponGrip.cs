using System;
using UnityEngine;

public static class WeaponGrip {
    public static Transform FindMount(Gun weapon, string mountName) {
        if (weapon == null) return null;
        Transform result = null;
        string expected = mountName.Replace(" ", "").ToLowerInvariant().Replace("handel", "handle");
        foreach (Transform candidate in weapon.GetComponentsInChildren<Transform>(true)) {
            string name = candidate.name.Replace(" ", "").ToLowerInvariant().Replace("handel", "handle");
            if (name != expected) continue;
            if (result != null) {
                Debug.LogError($"WeaponGrip: {weapon.name}에 {mountName} 기준점이 중복되어 있습니다.", weapon);
                return null;
            }
            result = candidate;
        }
        if (result == null) Debug.LogError($"WeaponGrip: {weapon.name}에 {mountName} 기준점이 필요합니다.", weapon);
        return result;
    }

    public static void ApplyHands(Animator animator, Transform left, Transform right) {
        ApplyHands(animator, left, right, null, 1f, 1f, Vector3.zero, Vector3.zero);
    }

    public static void ApplyHands(Animator animator, Transform left, Transform right,
        WeaponGripProfile profile, float leftBlend, float rightBlend, Vector3 leftElbow, Vector3 rightElbow) {
        ApplyHand(animator, AvatarIKGoal.LeftHand, left, leftBlend,
            profile != null ? profile.leftPositionWeight : 1f, profile != null ? profile.leftRotationWeight : 1f,
            profile != null ? profile.leftPositionOffset : Vector3.zero, profile != null ? profile.leftRotationOffset : Vector3.zero);
        ApplyHand(animator, AvatarIKGoal.RightHand, right, rightBlend,
            profile != null ? profile.rightPositionWeight : 1f, profile != null ? profile.rightRotationWeight : 1f,
            profile != null ? profile.rightPositionOffset : Vector3.zero, profile != null ? profile.rightRotationOffset : Vector3.zero);
        float hintWeight = profile != null ? profile.elbowWeight : 0f;
        animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, left != null ? hintWeight * leftBlend : 0f);
        animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, right != null ? hintWeight * rightBlend : 0f);
        animator.SetIKHintPosition(AvatarIKHint.LeftElbow, leftElbow);
        animator.SetIKHintPosition(AvatarIKHint.RightElbow, rightElbow);
    }

    private static void ApplyHand(Animator animator, AvatarIKGoal hand, Transform target, float blend,
        float positionWeight, float rotationWeight, Vector3 offset, Vector3 rotation) {
        float weight = target != null ? Mathf.Clamp01(blend) : 0f;
        animator.SetIKPositionWeight(hand, weight * Mathf.Clamp01(positionWeight));
        animator.SetIKRotationWeight(hand, weight * Mathf.Clamp01(rotationWeight));
        if (target == null) return;
        animator.SetIKPosition(hand, target.position + target.rotation * offset);
        animator.SetIKRotation(hand, target.rotation * Quaternion.Euler(rotation));
    }
}
