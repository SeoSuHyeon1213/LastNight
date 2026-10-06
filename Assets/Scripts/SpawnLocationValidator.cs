using UnityEngine;
using UnityEngine.AI;

// 에디터에 연결된 실내 구역을 생성 금지와 플레이어 실내 소음 판정에 함께 쓴다.
public sealed class SpawnLocationValidator : MonoBehaviour {
    [SerializeField] private Collider[] indoorZones;
    [SerializeField] private BaseStorage baseTarget;
    [SerializeField, Min(0f)] private float maximumHeightSnap = 0.5f;

    public bool IsConfigured => baseTarget != null && indoorZones != null && indoorZones.Length > 0 &&
        System.Array.TrueForAll(indoorZones, zone => zone != null);

    private void Awake() {
        if (!IsConfigured)
            Debug.LogError("SpawnLocationValidator: Base Target과 Indoor Zones를 연결하세요. 실외 생성 검증은 연결 전까지 실패합니다.", this);
    }

    public bool IsIndoors(Vector3 position) {
        if (indoorZones == null) return false;
        foreach (Collider zone in indoorZones)
            if (zone != null && zone.enabled && zone.gameObject.activeInHierarchy &&
                (zone.ClosestPoint(position) - position).sqrMagnitude < 0.0001f) return true;
        return false;
    }

    public bool TrySnap(Vector3 candidate, float sampleDistance, int agentTypeId, int areaMask, out Vector3 position) {
        position = default;
        var filter = new NavMeshQueryFilter { agentTypeID = agentTypeId, areaMask = areaMask };
        if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleDistance, filter) ||
            Mathf.Abs(hit.position.y - candidate.y) > maximumHeightSnap) return false;
        position = hit.position;
        return true;
    }

    public bool Validate(Vector3 position, float radius, float height, LayerMask blockers,
        Vector3 playerPosition, float minPlayerDistance, float maxPlayerDistance, int agentTypeId, int areaMask) {
        if (!IsConfigured || IsIndoors(position + Vector3.up * height * 0.5f)) return false;
        Vector3 delta = position - playerPosition;
        delta.y = 0f;
        float distance = delta.magnitude;
        if (distance < minPlayerDistance || (maxPlayerDistance > 0f && distance > maxPlayerDistance)) return false;
        Vector3 bottom = position + Vector3.up * (radius + 0.15f);
        Vector3 top = position + Vector3.up * Mathf.Max(radius + 0.15f, height - radius);
        if (Physics.CheckCapsule(bottom, top, radius, blockers, QueryTriggerInteraction.Ignore)) return false;
        return baseTarget.TryGetApproach(position, agentTypeId, areaMask, radius, out _, out _);
    }
}
