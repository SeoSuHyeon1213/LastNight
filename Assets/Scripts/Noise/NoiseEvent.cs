using UnityEngine;

public enum NoiseKind {
    Gunshot,
    Movement,
    AimMovement,
    Reload,
    ItemPickup,
    BarricadeWork
}

// 한 번 발생한 소음. 반경은 실내 배율까지 적용된 최종 값이다.
public readonly struct NoiseEvent {
    public readonly Vector3 Position;
    public readonly float Radius;
    public readonly NoiseKind Kind;

    public NoiseEvent(Vector3 position, float radius, NoiseKind kind) {
        Position = position;
        Radius = radius;
        Kind = kind;
    }
}
