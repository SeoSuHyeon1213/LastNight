using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WeaponGripProfile : MonoBehaviour {
    public Transform leftHand;
    public Transform rightHand;
    [Tooltip("애니메이션의 오른손 위치에 Right Handle을 맞춥니다. 권총 자세에 사용합니다.")]
    public bool anchorAtAnimatedRightHand;
    [Tooltip("오른손 기준 장착 시 손과 무기에 함께 적용하는 캐릭터 로컬 위치 보정")]
    public Vector3 animatedHandAnchorOffset;
    public Vector3 leftPositionOffset;
    public Vector3 rightPositionOffset;
    public Vector3 leftRotationOffset;
    public Vector3 rightRotationOffset;
    [Range(0f, 1f)] public float leftPositionWeight = 1f;
    [Range(0f, 1f)] public float rightPositionWeight = 1f;
    [Range(0f, 1f)] public float leftRotationWeight = 1f;
    [Range(0f, 1f)] public float rightRotationWeight = 1f;
    [Tooltip("캐릭터 로컬 방향을 기준으로, 애니메이션 팔꿈치에 더하는 오프셋")]
    public Vector3 leftElbowOffset = new Vector3(-0.12f, -0.08f, 0f);
    public Vector3 rightElbowOffset = new Vector3(0.12f, -0.08f, 0f);
    [Range(0f, 1f)] public float elbowWeight = 0.5f;
    [Min(0.01f)] public float blendSeconds = 0.2f;
    [Range(0f, 1f)] public float reloadLeftWeight = 0f;
    [Range(0f, 1f)] public float reloadRightWeight = 1f;
    [Range(0f, 1f)] public float fingerWeight = 1f;

    [Serializable]
    public struct FingerPose {
        public HumanBodyBones bone;
        public Vector3 localEulerAngles;
    }
    public FingerPose[] fingerPose = Array.Empty<FingerPose>();

    public static bool IsFinger(HumanBodyBones bone) =>
        bone >= HumanBodyBones.LeftThumbProximal && bone <= HumanBodyBones.RightLittleDistal;

    public void CaptureFingerPose(Animator animator) {
        if (animator == null || !animator.isHuman) return;
        var poses = new System.Collections.Generic.List<FingerPose>();
        for (int i = (int)HumanBodyBones.LeftThumbProximal; i <= (int)HumanBodyBones.RightLittleDistal; i++) {
            Transform bone = animator.GetBoneTransform((HumanBodyBones)i);
            if (bone != null) poses.Add(new FingerPose { bone = (HumanBodyBones)i, localEulerAngles = bone.localEulerAngles });
        }
        fingerPose = poses.ToArray();
    }

    private void OnValidate() {
        blendSeconds = Mathf.Max(0.01f, blendSeconds);
    }
}
