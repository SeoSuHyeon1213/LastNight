using UnityEngine;

public enum ThrowMode { Straight, Arc }

public interface IThrowable {
    void SetThrowMode(ThrowMode mode);
    // 파워는 질량과 무관한 초기 속도(m/s)로 사용한다.
    void SetThrowPower(float speed);
    void Throw(Vector3 direction, GameObject owner);
}
