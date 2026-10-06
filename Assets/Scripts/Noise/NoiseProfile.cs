using System;
using UnityEngine;

// 플레이어 소음원별 반경·반복 간격·실내 배율. 모두 테스트 시작값이다.
[CreateAssetMenu(menuName = "Scriptable/NoiseProfile", fileName = "Noise Profile")]
public sealed class NoiseProfile : ScriptableObject {
    [Serializable]
    public struct NoiseSetting {
        [Min(0f)] public float radius;
        [Tooltip("연속 발생 시 다음 소음까지의 최소 간격(초)")]
        [Min(0f)] public float repeatInterval;
        [Tooltip("소음원이 실내에 있을 때 반경에 곱하는 값")]
        [Range(0f, 1f)] public float indoorMultiplier;

        public NoiseSetting(float radius, float repeatInterval, float indoorMultiplier) {
            this.radius = radius;
            this.repeatInterval = repeatInterval;
            this.indoorMultiplier = indoorMultiplier;
        }

        public float GetRadius(bool indoors) => radius * (indoors ? indoorMultiplier : 1f);
    }

    // 총성 30m는 기존 테스트값을 유지한다. 시야 탐지 반경 8m와 별도로 조정한다.
    [SerializeField] private NoiseSetting gunshot = new NoiseSetting(30f, 1f, 1f);
    [SerializeField] private NoiseSetting movement = new NoiseSetting(8f, 0.5f, 0.7f);
    [SerializeField] private NoiseSetting aimMovement = new NoiseSetting(3f, 0.5f, 0.7f);
    [Tooltip("정지 중 소음. 반경 0이면 발생하지 않는다.")]
    [SerializeField] private NoiseSetting stationary = new NoiseSetting(0f, 0.5f, 0.7f);

    // 공유 실내 구역 안에서는 근거리 작업 소음을 감쇠한다.
    [Tooltip("재장전 시작 시 1회. 반복 간격은 쓰지 않는다.")]
    [SerializeField] private NoiseSetting reload = new NoiseSetting(5f, 0f, 0.7f);
    [Tooltip("아이템 획득 시 1회 (회복약 즉시 사용 포함). 반복 간격은 쓰지 않는다.")]
    [SerializeField] private NoiseSetting itemPickup = new NoiseSetting(4f, 0f, 0.7f);
    [Tooltip("바리케이드 설치·수리 작업 중 반복 간격마다")]
    [SerializeField] private NoiseSetting barricadeWork = new NoiseSetting(10f, 1f, 0.7f);

    public NoiseSetting Gunshot => gunshot;
    public NoiseSetting Movement => movement;
    public NoiseSetting AimMovement => aimMovement;
    public NoiseSetting Stationary => stationary;
    public NoiseSetting Reload => reload;
    public NoiseSetting ItemPickup => itemPickup;
    public NoiseSetting BarricadeWork => barricadeWork;
}
