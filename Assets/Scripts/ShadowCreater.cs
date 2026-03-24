using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ShadowCreater : MonoBehaviour
{
    [Tooltip("光源参考物体，阴影沿「物体指向光源」的反方向投射")]
    public GameObject LightReference;

    [Tooltip("射线检测最大距离")]
    public float maxRayDistance = 100f;

    [Tooltip("Ground 物体的 Tag 名称，仅当射线首次命中该 Tag 时才视为有效命中")]
    public string groundTag = "Ground";

    [Tooltip("勾选后会在 Start 时自动执行一次投射并创建阴影 Collider")]
    public bool createOnStart = false;

    [Tooltip("勾选后每帧根据 LightReference 相对位置重新计算并更新阴影 Collider 的位置与形状")]
    public bool updateRealtime = true;

    [Tooltip("阴影碰撞体所在 GameObject 的 Tag（例如 Shadow）。玩家侧用 PlayerShadowSpeedModifier 按该 Tag 检测。")]
    public string shadowTag = "Shadow";

    [Tooltip("阴影物体使用的 Layer 名称（在 Edit > Project Settings > Tags and Layers 的 Layers 里新建，例如 Shadow）。留空则不改 Layer。生成时自动赋给 ShadowCollider。")]
    public string shadowLayerName = "Shadow";

    private GameObject _shadowObject;
    private Mesh _shadowMesh;
    private MeshCollider _shadowMeshCollider;
    private const int VertexCount = 8;

    private void Start()
    {
        if (createOnStart && !updateRealtime)
            CreateShadowCollider();
    }

    private void LateUpdate()
    {
        if (updateRealtime)
            UpdateShadowCollider();
    }

    /// <summary>
    /// 根据当前 LightReference 相对位置重新计算投射命中点，并创建或更新阴影 Collider。
    /// </summary>
    public void UpdateShadowCollider()
    {
        var box = GetComponent<BoxCollider>();
        if (box == null || LightReference == null) return;

        Vector3 lightPos = LightReference.transform.position;
        Vector3 dir = (lightPos - transform.position).normalized;
        dir = -dir;

        Vector3[] worldVertices = GetBoxColliderWorldVertices(box);
        List<Vector3> hitPoints = new List<Vector3>(VertexCount);
        int layerMask = ~0;

        for (int i = 0; i < worldVertices.Length; i++)
        {
            // 使用 RaycastAll：允许先穿过非 Ground 碰撞体，只取沿射线方向上最近的 Ground 命中点
            RaycastHit[] hits = Physics.RaycastAll(worldVertices[i], dir, maxRayDistance, layerMask);
            if (hits != null && hits.Length > 0)
            {
                float minDist = float.MaxValue;
                bool foundGround = false;
                Vector3 closestGroundPoint = Vector3.zero;

                foreach (var h in hits)
                {
                    if (!h.collider.CompareTag(groundTag))
                        continue;

                    float d = h.distance;
                    if (d < minDist)
                    {
                        minDist = d;
                        closestGroundPoint = h.point;
                        foundGround = true;
                    }
                }

                if (foundGround)
                {
                    hitPoints.Add(closestGroundPoint);
                    continue;
                }
            }

            // 该顶点未能命中 Ground：跳过即可（尽量生成，而不是要求 8 个顶点都命中）
        }

        // 至少需要 3 个点才能生成三角形网格
        if (hitPoints.Count < 3) return;

        if (_shadowObject == null)
            CreateColliderAtHitPoints(hitPoints);
        else
            UpdateShadowPositionAndMesh(hitPoints);
    }

    /// <summary>
    /// 计算当前物体 BoxCollider 的 8 个顶点沿 LightReference 方向的投射，
    /// 仅当射线首次命中 Tag 为 Ground 的碰撞体时才记录命中点，在所有顶点都命中 Ground 后在该位置创建新的 Collider。
    /// </summary>
    public void CreateShadowCollider()
    {
        var box = GetComponent<BoxCollider>();
        if (box == null)
        {
            Debug.LogWarning("ShadowCreater: 当前物体没有 BoxCollider。");
            return;
        }
        if (LightReference == null)
        {
            Debug.LogWarning("ShadowCreater: 未指定 LightReference。");
            return;
        }
        UpdateShadowCollider();
    }

    /// <summary>
    /// 获取 BoxCollider 的 8 个顶点在世界空间中的位置。
    /// </summary>
    private static Vector3[] GetBoxColliderWorldVertices(BoxCollider box)
    {
        Vector3 c = box.center;
        Vector3 s = box.size * 0.5f;
        Transform t = box.transform;
        var vertices = new Vector3[8];
        int idx = 0;
        for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                    vertices[idx++] = t.TransformPoint(c + new Vector3(x * s.x, y * s.y, z * s.z));
        return vertices;
    }

    /// <summary>
    /// 在所有命中点所在平面（近似为水平面）上，按绕中心的角度排序后生成多边形网格，并创建带 MeshCollider 的物体。
    /// </summary>
    private void CreateColliderAtHitPoints(List<Vector3> points)
    {
        if (points.Count < 3) return;

        Vector3 center = GetShadowCenter(points);
        List<Vector3> ordered = OrderPointsAroundCenter(points, center);

        _shadowMesh = new Mesh();
        _shadowMesh.name = "ShadowMesh";
        SetShadowMeshVertsAndTris(_shadowMesh, center, ordered);
        _shadowMesh.RecalculateBounds();
        _shadowMesh.RecalculateNormals();

        _shadowObject = new GameObject("ShadowCollider");
        _shadowObject.transform.position = center;
        _shadowObject.transform.rotation = Quaternion.identity;
        _shadowObject.transform.localScale = Vector3.one;

        var mf = _shadowObject.AddComponent<MeshFilter>();
        mf.sharedMesh = _shadowMesh;

        _shadowMeshCollider = _shadowObject.AddComponent<MeshCollider>();
        _shadowMeshCollider.sharedMesh = _shadowMesh;
        // 非凸网格不能作为 Trigger（Unity 限制），需 isTrigger=false。若不想与玩家发生实体碰撞，请用 Layer 在 Physics 碰撞矩阵中忽略与 Player 的碰撞。
        _shadowMeshCollider.convex = false;
        _shadowMeshCollider.isTrigger = false;

        TrySetShadowTag(_shadowObject, shadowTag);
        TrySetShadowLayer(_shadowObject, shadowLayerName);
    }

    static void TrySetShadowLayer(GameObject go, string layerName)
    {
        if (go == null || string.IsNullOrEmpty(layerName)) return;
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0)
        {
            Debug.LogWarning(
                "ShadowCreater: Layer \"" + layerName + "\" 不存在。请在 Edit > Project Settings > Tags and Layers 的 Layers 中新建该层，或清空 shadowLayerName。");
            return;
        }

        go.layer = layer;
    }

    static void TrySetShadowTag(GameObject go, string tagName)
    {
        if (go == null || string.IsNullOrEmpty(tagName)) return;
        try
        {
            go.tag = tagName;
        }
        catch (UnityException)
        {
            Debug.LogWarning(
                "ShadowCreater: Tag \"" + tagName + "\" 未在 Edit > Project Settings > Tags and Layers 中定义，无法赋给阴影物体。请添加该 Tag 或在 Inspector 里把 shadowTag 改成已有 Tag。");
        }
    }

    /// <summary>
    /// 仅更新已有阴影物体的位置和网格顶点，不重新创建物体。
    /// </summary>
    private void UpdateShadowPositionAndMesh(List<Vector3> points)
    {
        if (_shadowObject == null || _shadowMesh == null || _shadowMeshCollider == null) return;
        if (points.Count < 3) return;

        Vector3 center = GetShadowCenter(points);
        List<Vector3> ordered = OrderPointsAroundCenter(points, center);

        SetShadowMeshVertsAndTris(_shadowMesh, center, ordered);
        _shadowMesh.RecalculateBounds();
        _shadowMesh.RecalculateNormals();

        _shadowObject.transform.position = center;
        // 不要每帧 sharedMesh=null 再赋值：会打断 Trigger 的重叠状态，导致 OnTriggerExit 经常不触发。
        _shadowMeshCollider.sharedMesh = _shadowMesh;
    }

    private static Vector3 GetShadowCenter(List<Vector3> points)
    {
        Vector3 center = Vector3.zero;
        foreach (var p in points) center += p;
        return center / points.Count;
    }

    private static List<Vector3> OrderPointsAroundCenter(List<Vector3> points, Vector3 center)
    {
        return points
            .OrderBy(p => Mathf.Atan2((p - center).z, (p - center).x))
            .ToList();
    }

    private static void SetShadowMeshVertsAndTris(Mesh mesh, Vector3 center, List<Vector3> ordered)
    {
        var verts = new List<Vector3> { Vector3.zero };
        foreach (var p in ordered)
            verts.Add(p - center);
        mesh.SetVertices(verts);

        var tris = new List<int>();
        int n = ordered.Count;
        for (int i = 1; i <= n; i++)
        {
            tris.Add(0);
            tris.Add(i);
            tris.Add(i == n ? 1 : i + 1);
        }
        mesh.SetTriangles(tris, 0);
    }
}
