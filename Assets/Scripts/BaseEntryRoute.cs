using UnityEngine;

// 이 오브젝트는 입구 바깥 접근점이다. 안쪽 지점과 방어물을 Inspector에서 연결한다.
public sealed class BaseEntryRoute : MonoBehaviour {
    [SerializeField] private Transform inside;
    [SerializeField] private Barricade barricade;
    public Transform Inside => inside;
    public Barricade Barricade => barricade;

    private void OnDrawGizmosSelected() {
        if (inside == null) return;
        Gizmos.color = barricade != null && barricade.IsBlocking ? Color.red : Color.green;
        Gizmos.DrawLine(transform.position, inside.position);
        Gizmos.DrawWireSphere(transform.position, 0.3f);
        Gizmos.DrawWireSphere(inside.position, 0.3f);
    }
}
