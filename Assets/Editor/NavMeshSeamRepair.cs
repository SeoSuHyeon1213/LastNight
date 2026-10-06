using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Repairs short, walkable seams after baking; never runs in a player build.</summary>
public static class NavMeshSeamRepair
{
    private const string RootName = "Navigation Seam Links";
    private const float MaxFloorGap = 0.5f;
    private const float Inset = 0.1f;
    private const float LinkWidth = 0.1f;
    private const float SampleSpacing = 0.5f;
    private const float CellSize = 1.5f;
    private const float BodyRadius = 0.4f;
    private static MeshFilter[] sceneMeshes;
    private static readonly System.Reflection.MethodInfo IntersectMesh = typeof(HandleUtility).GetMethod(
        "IntersectRayMesh", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic,
        null, new[] { typeof(Ray), typeof(Mesh), typeof(Matrix4x4), typeof(RaycastHit).MakeByRefType() }, null);

    [Serializable]
    public class Candidate
    {
        public Vector3 start;
        public Vector3 end;
        public string floor;
        public string house;
        public string before;
        public string after;
    }

    [Serializable]
    public class Report
    {
        public int boundaryEdges;
        public int blockedByGeometry;
        public int blockedByBarricade;
        public int unsupportedFloor;
        public int alreadyLinked;
        public int created;

        public List<Candidate> candidates = new List<Candidate>();
    }

    private struct Edge
    {
        public Vector3 a, b, inward;
        public int count;
    }

    [MenuItem("Tools/LastNight/Navigation/Preview Seam Links")]
    private static void PreviewMenu()
    {
        Debug.Log(JsonUtility.ToJson(Scan(), true));
    }

    [MenuItem("Tools/LastNight/Navigation/Apply Seam Links")]
    private static void ApplyMenu()
    {
        Debug.Log(JsonUtility.ToJson(Apply(), true));
    }

    public static Report Scan()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Repair NavMesh seams in Edit Mode after baking.");
        var surfaces = UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
        if (surfaces.Length != 1 || surfaces[0].navMeshData == null)
            throw new InvalidOperationException("Select a scene with exactly one baked NavMeshSurface.");
        var surface = surfaces[0];
        var settings = surface.GetBuildSettings();
        var filter = new NavMeshQueryFilter { agentTypeID = surface.agentTypeID, areaMask = NavMesh.AllAreas };
        Physics.SyncTransforms();
        if (IntersectMesh == null)
            throw new InvalidOperationException("This Unity Editor does not expose the mesh intersection API; no links were created.");
        sceneMeshes = UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
        var obstacles = UnityEngine.Object.FindObjectsByType<NavMeshObstacle>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var links = UnityEngine.Object.FindObjectsByType<NavMeshLink>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var houseBounds = new List<Bounds>();
        foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (!renderer.transform.root.name.StartsWith("House") || !renderer.name.ToLowerInvariant().Contains("floor")) continue;
            var bounds = renderer.bounds;
            bounds.Expand(new Vector3(4f, 4f, 4f));
            houseBounds.Add(bounds);
        }
        var edges = GetBoundaryEdges();
        edges.RemoveAll(edge => !houseBounds.Exists(bounds => bounds.Contains(edge.a) || bounds.Contains(edge.b) ||
            bounds.Contains((edge.a + edge.b) * 0.5f)));
        var report = new Report { boundaryEdges = edges.Count };
        var cells = new Dictionary<Vector2Int, HashSet<int>>();
        for (int i = 0; i < edges.Count; i++)
        {
            var edge = edges[i];
            int steps = Mathf.CeilToInt(Horizontal(edge.b - edge.a).magnitude / SampleSpacing);
            for (int s = 0; s <= steps; s++)
            {
                var cell = Cell(Vector3.Lerp(edge.a, edge.b, s / (float)Mathf.Max(1, steps)));
                if (!cells.TryGetValue(cell, out var bucket))
                    cells[cell] = bucket = new HashSet<int>();
                bucket.Add(i);
            }
        }

        var pairs = new HashSet<(int, int)>();
        float maxDistance = 2f * settings.agentRadius + MaxFloorGap + 2f * Inset;
        for (int i = 0; i < edges.Count; i++)
        {
            var edge = edges[i];
            int steps = Mathf.CeilToInt(Horizontal(edge.b - edge.a).magnitude / SampleSpacing);
            for (int s = 0; s <= steps; s++)
            {
                var cell = Cell(Vector3.Lerp(edge.a, edge.b, s / (float)Mathf.Max(1, steps)));
                for (int x = -1; x <= 1; x++)
                for (int z = -1; z <= 1; z++)
                {
                    if (!cells.TryGetValue(cell + new Vector2Int(x, z), out var bucket)) continue;
                    foreach (int j in bucket)
                    {
                        if (j <= i || !pairs.Add((i, j))) continue;
                        var other = edges[j];
                        var axis = Horizontal(edge.b - edge.a).normalized;
                        if (Mathf.Abs(Vector3.Dot(axis, Horizontal(other.b - other.a).normalized)) < 0.7f ||
                            Vector3.Dot(edge.inward, other.inward) > -0.4f) continue;
                        float b0 = Vector3.Dot(other.a - edge.a, axis);
                        float b1 = Vector3.Dot(other.b - edge.a, axis);
                        float low = Mathf.Max(0f, Mathf.Min(b0, b1));
                        float high = Mathf.Min(Horizontal(edge.b - edge.a).magnitude, Mathf.Max(b0, b1));
                        if (high - low < LinkWidth + 0.02f) continue;
                        int locations = Mathf.Max(1, Mathf.CeilToInt((high - low) / 0.1f));
                        for (int n = 0; n < locations; n++)
                        {
                            var a = PointAt(edge, axis, Mathf.Lerp(low, high, (n + 0.5f) / locations));
                            var b = ClosestHorizontal(other, a);
                            var delta = Horizontal(b - a);
                            if (delta.magnitude > maxDistance || delta.magnitude < 0.03f ||
                                Vector3.Dot(delta.normalized, edge.inward) > -0.4f ||
                                Mathf.Abs(a.y - b.y) > settings.agentClimb) continue;
                            a += edge.inward * Inset;
                            b += other.inward * Inset;
                            if (!NavMesh.SamplePosition(a, out var ah, 0.16f, filter) ||
                                !NavMesh.SamplePosition(b, out var bh, 0.16f, filter)) continue;
                            a = ah.position;
                            b = bh.position;
                            if (!NavMesh.Raycast(a, b, out _, filter)) continue;
                            if (HasLink(a, b, links, surface.agentTypeID)) { report.alreadyLinked++; continue; }
                            if (NearCandidate(a, b, report.candidates)) continue;
                            string reason = ValidateSpan(a, b, BodyRadius, settings.agentHeight,
                                settings.agentClimb, obstacles, out string floor);
                            if (reason == "barricade") { report.blockedByBarricade++; continue; }
                            if (reason == "geometry") { report.blockedByGeometry++; continue; }
                            if (reason == "floor") { report.unsupportedFloor++; continue; }
                            report.candidates.Add(new Candidate { start = a, end = b, floor = floor, house = NearestHouse((a+b)*0.5f) });
                        }
                    }
                }
            }
        }
        return report;
    }

    public static Report Apply()
    {
        CheckPrefabClearance();
        // Re-scan immediately before applying, including current obstacle and collider state.
        var report = Scan();
        if (report.candidates.Count == 0) return report;
        var surface = UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None)[0];
        var scene = surface.gameObject.scene;
        Transform root = null;
        foreach (var obj in scene.GetRootGameObjects())
            if (obj.name == RootName) root = obj.transform;
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Repair NavMesh seams");
        if (root == null)
        {
            var obj = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(obj, scene);
            Undo.RegisterCreatedObjectUndo(obj, "Create navigation link group");
            root = obj.transform;
        }
        foreach (var candidate in report.candidates)
        {
            var path = new NavMeshPath();
            NavMesh.CalculatePath(candidate.start, candidate.end, NavMesh.AllAreas, path);
            candidate.before = path.status.ToString();
            var obj = new GameObject(candidate.house + " - Seam " + (root.childCount + 1).ToString("D3"));
            SceneManager.MoveGameObjectToScene(obj, scene);
            Undo.RegisterCreatedObjectUndo(obj, "Create seam link");
            Undo.SetTransformParent(obj.transform, root, "Group seam link");
            obj.transform.position = (candidate.start + candidate.end) * 0.5f;
            var link = Undo.AddComponent<NavMeshLink>(obj);
            Undo.RecordObject(link, "Configure seam link");
            link.agentTypeID = surface.agentTypeID;
            link.startPoint = obj.transform.InverseTransformPoint(candidate.start);
            link.endPoint = obj.transform.InverseTransformPoint(candidate.end);
            link.width = LinkWidth;
            link.bidirectional = true;
            link.area = NavMesh.GetAreaFromName("Walkable");
            link.costModifier = -1f;
            NavMesh.CalculatePath(candidate.start, candidate.end, NavMesh.AllAreas, path);
            candidate.after = path.status.ToString();
            bool valid = path.status == NavMeshPathStatus.PathComplete;
            NavMesh.CalculatePath(candidate.end, candidate.start, NavMesh.AllAreas, path);
            if (!valid || path.status != NavMeshPathStatus.PathComplete)
            {
                Undo.DestroyObjectImmediate(obj);
                continue;
            }
            report.created++;
        }
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        return report;
    }

    private static string NearestHouse(Vector3 point)
    {
        string house = "House";
        float best = float.PositiveInfinity;
        foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (!renderer.transform.root.name.StartsWith("House") || !renderer.name.ToLowerInvariant().Contains("floor")) continue;
            float distance = renderer.bounds.SqrDistance(point);
            if (distance >= best) continue;
            best = distance;
            house = renderer.transform.root.name;
        }
        return house;
    }

    private static void CheckPrefabClearance()
    {
        var surface = UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
        if (surface.Length != 1) throw new InvalidOperationException("Expected one NavMeshSurface.");
        float height = surface[0].GetBuildSettings().agentHeight;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Zombie" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab.TryGetComponent<NavMeshAgent>(out var agent)) continue;
            if (agent.agentTypeID != surface[0].agentTypeID || agent.height > height + 0.01f)
                throw new InvalidOperationException($"{path}: match the baked agent type and height before applying links.");
            foreach (var body in prefab.GetComponentsInChildren<CapsuleCollider>(true))
            {
                if (!body.enabled || body.isTrigger) continue;
                var scale = body.transform.lossyScale;
                if (body.direction != 1 || body.height * Mathf.Abs(scale.y) > height + 0.01f ||
                    body.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) > BodyRadius + 0.01f)
                    throw new InvalidOperationException($"{path}: body exceeds the tested link clearance.");
            }
        }
    }

    private static List<Edge> GetBoundaryEdges()
    {
        var tri = NavMesh.CalculateTriangulation();
        var weld = new Dictionary<Vector3Int, int>();
        var ids = new int[tri.vertices.Length];
        for (int i = 0; i < ids.Length; i++)
        {
            var p = tri.vertices[i] * 1000f;
            var key = new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z));
            if (!weld.TryGetValue(key, out int id)) weld[key] = id = weld.Count;
            ids[i] = id;
        }
        var edges = new Dictionary<(int, int), Edge>();
        for (int i = 0; i < tri.indices.Length; i += 3)
        for (int side = 0; side < 3; side++)
        {
            int ai = tri.indices[i + side], bi = tri.indices[i + (side + 1) % 3];
            int ci = tri.indices[i + (side + 2) % 3];
            var key = (Mathf.Min(ids[ai], ids[bi]), Mathf.Max(ids[ai], ids[bi]));
            if (edges.TryGetValue(key, out var existing))
            {
                existing.count++;
                edges[key] = existing;
                continue;
            }
            var a = tri.vertices[ai]; var b = tri.vertices[bi];
            var axis = Horizontal(b - a).normalized;
            var inward = Vector3.Cross(axis, Vector3.up).normalized;
            if (Vector3.Dot(inward, tri.vertices[ci] - a) < 0f) inward = -inward;
            edges[key] = new Edge { a = a, b = b, inward = inward, count = 1 };
        }
        var result = new List<Edge>();
        foreach (var edge in edges.Values)
            if (edge.count == 1 && Horizontal(edge.b - edge.a).magnitude >= LinkWidth + 0.02f)
                result.Add(edge);
        return result;
    }

    private static string ValidateSpan(Vector3 a, Vector3 b, float radius, float height, float climb,
        NavMeshObstacle[] obstacles, out string floor)
    {
        floor = "";
        var across = Vector3.Cross(Horizontal(b - a).normalized, Vector3.up) * LinkWidth * 0.5f;
        var bodyBounds = new Bounds((a + b) * 0.5f + Vector3.up * height * 0.5f,
            new Vector3(Mathf.Abs(b.x - a.x) + 2f * radius + LinkWidth, height + climb,
                Mathf.Abs(b.z - a.z) + 2f * radius + LinkWidth));
        var meshes = new List<MeshFilter>();
        if (sceneMeshes != null)
            foreach (var mesh in sceneMeshes)
                if (mesh != null && mesh.sharedMesh != null && mesh.TryGetComponent<MeshRenderer>(out var renderer) &&
                    renderer.enabled && !Ignored(mesh) && renderer.bounds.Intersects(bodyBounds)) meshes.Add(mesh);
        // Disabled barricades still represent intended entrances, so never bridge their footprint.
        foreach (var obstacle in obstacles)
        {
            if (obstacle.gameObject.scene != SceneManager.GetActiveScene()) continue;
            var center = obstacle.transform.TransformPoint(obstacle.center);
            var extent = obstacle.shape == NavMeshObstacleShape.Box ? obstacle.size * 0.5f :
                new Vector3(obstacle.radius, obstacle.height * 0.5f, obstacle.radius);
            var scale = obstacle.transform.lossyScale;
            extent = Vector3.Scale(extent, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            var inverseRotation = Quaternion.Inverse(obstacle.transform.rotation);
            var localA = inverseRotation * (a - center);
            var localB = inverseRotation * (b - center);
            var bounds = new Bounds(Vector3.zero, 2f * (extent + new Vector3(radius + LinkWidth * 0.5f, height, radius + LinkWidth * 0.5f)));
            var direction = localB - localA;
            if (bounds.Contains(localA) || bounds.Contains(localB) ||
                (bounds.IntersectRay(new Ray(localA, direction.normalized), out float distance) && distance <= direction.magnitude))
                return "barricade";
        }
        for (int lane = -1; lane <= 1; lane++)
        {
            var from = a + across * lane; var to = b + across * lane;
            // A capsule above the higher floor rejects walls and low ceilings without hitting the floor itself.
            float baseY = Mathf.Max(a.y, b.y) + 0.02f;
            var bottom = new Vector3(from.x, baseY + radius, from.z);
            var top = bottom + Vector3.up * Mathf.Max(0f, height - 2f * radius);
            var travel = Horizontal(to - from);
            if (Physics.CheckCapsule(bottom,top,radius,~0,QueryTriggerInteraction.Ignore)){
                var overlaps=Physics.OverlapCapsule(bottom,top,radius,~0,QueryTriggerInteraction.Ignore);
                floor=overlaps.Length>0?overlaps[0].transform.root.name+"/"+overlaps[0].name:"physical overlap";return "geometry";
            }
            if (Physics.CapsuleCast(bottom,top,radius,travel.normalized,out var solidHit,travel.magnitude,~0,QueryTriggerInteraction.Ignore)){
                floor=solidHit.collider.transform.root.name+"/"+solidHit.collider.name;return "geometry";
            }            // RenderMeshes baking also sees walls without colliders. Test those meshes in both directions.
            var side = Vector3.Cross(travel.normalized, Vector3.up);
            for (int offset = -1; offset <= 1; offset++)
            for (float y = baseY + 0.1f; y <= baseY + height; y += 0.1f)
            {
                var p = new Vector3(from.x, y, from.z) + side * radius * offset;
                if (MeshHit(p, travel.normalized, travel.magnitude, meshes, out _, out floor) ||
                    MeshHit(p + travel, -travel.normalized, travel.magnitude, meshes, out _, out floor)) { if (floor.Length == 0) {var overlaps=Physics.OverlapCapsule(bottom,top,radius,~0,QueryTriggerInteraction.Ignore);floor=overlaps.Length>0?overlaps[0].name:"physical cast";} return "geometry"; }
            }
            int samples = Mathf.Max(2, Mathf.CeilToInt(travel.magnitude / 0.05f));
            int unsupported = 0;
            for (int i = 0; i <= samples; i++)
            {
                var point = Vector3.Lerp(from, to, i / (float)samples);
                var bodyPoint = new Vector3(point.x, baseY, point.z);
                if (MeshHit(bodyPoint, Vector3.up, height, meshes, out _, out floor) ||
                    MeshHit(bodyPoint + Vector3.up * height, Vector3.down, height, meshes, out _, out floor)) { if (floor.Length == 0) {var overlaps=Physics.OverlapCapsule(bottom,top,radius,~0,QueryTriggerInteraction.Ignore);floor=overlaps.Length>0?overlaps[0].name:"physical cast";} return "geometry"; }
                var hits = Physics.RaycastAll(point + Vector3.up * (climb + 0.15f), Vector3.down,
                    climb * 2f + 0.3f, ~0, QueryTriggerInteraction.Ignore);
                bool supported = false;
                foreach (var hit in hits)
                    if (hit.normal.y > 0.7f && Mathf.Abs(hit.point.y - point.y) <= climb + 0.12f)
                    {
                        supported = true;
                        if (floor.Length == 0) floor = hit.collider.transform.root.name;
                        break;
                    }
                if (!supported && MeshHit(point + Vector3.up * (climb + 0.15f), Vector3.down,
                        climb * 2f + 0.3f, meshes, out var meshHit, out var meshFloor) &&
                    meshHit.normal.y > 0.7f && Mathf.Abs(meshHit.point.y - point.y) <= climb + 0.12f)
                {
                    supported = true;
                    if (floor.Length == 0) floor = meshFloor;
                }
                if (supported) unsupported = 0; else unsupported++;
                if ((!supported && (i == 0 || i == samples)) || unsupported * travel.magnitude / samples > MaxFloorGap)
                    return "floor";
            }
        }
        return null;
    }

    private static bool MeshHit(Vector3 origin, Vector3 direction, float length, List<MeshFilter> meshes,
        out RaycastHit closest, out string floor)
    {
        closest = default;
        floor = "";
        bool found = false;
        foreach (var mesh in meshes)
        {
            var args = new object[] { new Ray(origin, direction), mesh.sharedMesh, mesh.transform.localToWorldMatrix, default(RaycastHit) };
            if (!(bool)IntersectMesh.Invoke(null, args)) continue;
            var hit = (RaycastHit)args[3];
            if (hit.distance > length || (found && hit.distance >= closest.distance)) continue;
            closest = hit;
            floor = mesh.transform.root.name + "/" + mesh.name;
            found = true;
        }
        return found;
    }

    private static bool HasLink(Vector3 a, Vector3 b, NavMeshLink[] links, int agentType)
    {
        foreach (var link in links)
        {
            if (link.agentTypeID != agentType || !link.enabled || !link.activated) continue;
            var start = link.transform.TransformPoint(link.startPoint);
            var end = link.transform.TransformPoint(link.endPoint);
            if ((Vector3.Distance(start, a) < 0.5f && Vector3.Distance(end, b) < 0.5f) ||
                (Vector3.Distance(start, b) < 0.5f && Vector3.Distance(end, a) < 0.5f)) return true;
        }
        return false;
    }

    private static bool NearCandidate(Vector3 a, Vector3 b, List<Candidate> candidates)
    {
        foreach (var candidate in candidates)
            if (Vector3.Distance((candidate.start + candidate.end) * 0.5f, (a + b) * 0.5f) < 0.8f &&
                Mathf.Abs(Vector3.Dot(Horizontal(candidate.end - candidate.start).normalized, Horizontal(b - a).normalized)) > 0.9f)
                return true;
        return false;
    }

    private static Vector3 PointAt(Edge edge, Vector3 axis, float distance)
    {
        return Vector3.Lerp(edge.a, edge.b, distance / Horizontal(edge.b - edge.a).magnitude);
    }

    private static Vector3 ClosestHorizontal(Edge edge, Vector3 point)
    {
        var delta = Horizontal(edge.b - edge.a);
        return Vector3.Lerp(edge.a, edge.b, Mathf.Clamp01(Vector3.Dot(Horizontal(point - edge.a), delta) / delta.sqrMagnitude));
    }

    private static Vector3 Horizontal(Vector3 vector) => new Vector3(vector.x, 0f, vector.z);
    private static Vector2Int Cell(Vector3 point) => new Vector2Int(Mathf.FloorToInt(point.x / CellSize), Mathf.FloorToInt(point.z / CellSize));
    private static bool Ignored(MeshFilter mesh)
    {
        var parent = mesh.transform;
        while (parent != null)
        {
            if (parent.TryGetComponent<NavMeshModifier>(out var modifier) && modifier.isActiveAndEnabled &&
                (parent == mesh.transform || modifier.applyToChildren) && modifier.AffectsAgentType(0))
                return modifier.ignoreFromBuild;
            parent = parent.parent;
        }
        return false;
    }
}
