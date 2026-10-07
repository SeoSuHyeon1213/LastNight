using UnityEngine;
using UnityEngine.AI;

// 거점 후보가 될 수 있는 집 한 채. 실내 판정 구역, 보관소 배치 지점, 좀비 진입 경로를 에디터에서 연결한다.
// 상태(체류 시간·활성 여부)는 BaseRelocationDirector가 소유하고 이 컴포넌트는 정의만 가진다.
public sealed class BaseHouse : MonoBehaviour {
    [Tooltip("로그·UI에 쓰는 고유 ID. 집끼리 겹치면 안 된다.")]
    [SerializeField] private string houseId;
    [SerializeField] private string displayName;
    [Tooltip("플레이어 실내 판정에 쓰는 Trigger 콜라이더. 집 전체를 덮는 Box 콜라이더 권장.")]
    [SerializeField] private Collider[] indoorZones;
    [Tooltip("이 집이 거점일 때 보관소가 놓일 위치·방향. NavMesh 위 실내 지점이어야 한다.")]
    [SerializeField] private Transform storageAnchor;
    [SerializeField] private BaseEntryRoute[] entryRoutes;

    public string HouseId => houseId;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? houseId : displayName;
    public Transform StorageAnchor => storageAnchor;
    public BaseEntryRoute[] EntryRoutes => entryRoutes;

    public bool Contains(Vector3 position) {
        if (indoorZones == null) return false;
        foreach (Collider zone in indoorZones)
            if (zone != null && zone.enabled && zone.gameObject.activeInHierarchy &&
                (zone.ClosestPoint(position) - position).sqrMagnitude < 0.0001f) return true;
        return false;
    }

    // 후보 제외 조건(기획 4.2 전환 안전성): 판정 구역·보관소 지점이 있고, 바깥 지점→안쪽 지점→보관소가
    // 완전 경로로 이어지는 진입로가 하나 이상 있어야 한다. 시작 시점 기준이며 바리케이드 설치 상태는 보지 않는다.
    // 플레이어 전용 출입로는 아직 시스템이 없어 검사하지 않는다.
    public bool Validate(NavMeshQueryFilter filter, float navMeshSnapDistance, out string reason) {
        reason = null;
        if (string.IsNullOrWhiteSpace(houseId)) reason = "House Id가 비어 있음";
        else if (indoorZones == null || indoorZones.Length == 0 || System.Array.Exists(indoorZones, z => z == null))
            reason = "Indoor Zones가 비었거나 빈 칸이 있음";
        else if (storageAnchor == null) reason = "Storage Anchor 미연결";
        else if (!NavMesh.SamplePosition(storageAnchor.position, out NavMeshHit goal, navMeshSnapDistance, filter))
            reason = "Storage Anchor 근처에 NavMesh가 없음";
        else if (!Contains(storageAnchor.position + Vector3.up)) reason = "Storage Anchor가 실내 구역 밖에 있음";
        else if (!HasConnectedRoute(goal.position, filter, navMeshSnapDistance))
            reason = "보관소까지 완전 경로로 이어지는 Entry Route가 없음";
        return reason == null;
    }

    private bool HasConnectedRoute(Vector3 goal, NavMeshQueryFilter filter, float snapDistance) {
        if (entryRoutes == null) return false;
        var path = new NavMeshPath();
        foreach (BaseEntryRoute route in entryRoutes) {
            if (route == null || route.Inside == null) continue;
            if (!NavMesh.SamplePosition(route.transform.position, out NavMeshHit outside, snapDistance, filter) ||
                !NavMesh.SamplePosition(route.Inside.position, out NavMeshHit inside, snapDistance, filter)) continue;
            if (NavMesh.CalculatePath(outside.position, inside.position, filter, path) &&
                path.status == NavMeshPathStatus.PathComplete &&
                NavMesh.CalculatePath(inside.position, goal, filter, path) &&
                path.status == NavMeshPathStatus.PathComplete) return true;
        }
        return false;
    }
}
