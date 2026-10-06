using UnityEngine;

// 에디터에서 배치하는 대기 군집 후보 지점. 구성원은 이 지점 주변 반경 안에만 놓인다.
public sealed class IdleGroupPoint : MonoBehaviour {
    [Tooltip("구성원을 흩어 놓을 반경")]
    [SerializeField, Min(0f)] private float spreadRadius = 2f;
    [Tooltip("이 지점에 둘 수 있는 최대 구성원 수")]
    [SerializeField, Range(1, 3)] private int capacity = 3;

    public float SpreadRadius => spreadRadius;
    public int Capacity => capacity;
    public Vector3 Position => transform.position;

    private void OnDrawGizmos() {
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.3f);
#if UNITY_EDITOR
        UnityEditor.Handles.color = Gizmos.color;
        UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, spreadRadius);
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.2f, $"Idle Group x{capacity}");
#endif
    }
}
