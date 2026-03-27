using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Builds a world-space shadow footprint by ray casting from each corner of a <see cref="BoxCollider"/>
/// toward the light, collecting hits on <see cref="groundTag"/>, then triangulating those points into a mesh used by a non-convex <see cref="MeshCollider"/>.
/// </summary>
public class ShadowCreater : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Object defining light position; shadow rays go from this volume toward the light (direction inverted for casting).")]
    public GameObject LightReference;

    [Header("Raycast")]
    [Tooltip("Maximum ray length from each box corner.")]
    public float maxRayDistance = 100f;
    [Tooltip("Only hits on colliders with this tag count as valid ground contacts.")]
    public string groundTag = "Ground";

    [Header("Lifecycle")]
    [Tooltip("If true, run one projection on Start when not using realtime updates.")]
    public bool createOnStart = false;
    [Tooltip("If true, recompute hit points and mesh every LateUpdate.")]
    public bool updateRealtime = true;

    [Header("Shadow object")]
    [Tooltip("Tag for the generated shadow GameObject (e.g. Shadow). PlayerShadowSpeedModifier detects this tag.")]
    public string shadowTag = "Shadow";
    [Tooltip("Layer name for the shadow object; leave empty to skip. Create the layer under Project Settings > Tags and Layers.")]
    public string shadowLayerName = "Shadow";

    GameObject _shadowObject;
    Mesh _shadowMesh;
    MeshCollider _shadowMeshCollider;
    const int VertexCount = 8;

    private void Start()
    {
        if (createOnStart && !updateRealtime)
            CreateShadowCollider();
    } // Cursor AI generated

    private void LateUpdate()
    {
        if (updateRealtime)
            UpdateShadowCollider();
    } // Cursor AI generated

    /// <summary>
    /// Recomputes ground hit points from the occluder box and creates or updates the shadow <see cref="MeshCollider"/>.
    /// </summary>
    public void UpdateShadowCollider()
    {
        var box = GetComponent<BoxCollider>();
        if (box == null || LightReference == null) return;

        Vector3 lightPos = LightReference.transform.position;
        Vector3 dir = (lightPos - transform.position).normalized;
        dir = -dir;

        Vector3[] worldVertices = GetBoxColliderWorldVertices(box);
        var hitPoints = new List<Vector3>(VertexCount);
        int layerMask = ~0;

        for (int i = 0; i < worldVertices.Length; i++)
        {
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
        }

        if (hitPoints.Count < 3) return;

        if (_shadowObject == null)
            CreateColliderAtHitPoints(hitPoints);
        else
            UpdateShadowPositionAndMesh(hitPoints);
    } // Cursor AI generated

    /// <summary>
    /// Validates components and runs the same update path as realtime mode.
    /// </summary>
    public void CreateShadowCollider()
    {
        var box = GetComponent<BoxCollider>();
        if (box == null)
        {
            Debug.LogWarning("ShadowCreater: No BoxCollider on this GameObject.");
            return;
        }
        if (LightReference == null)
        {
            Debug.LogWarning("ShadowCreater: LightReference is not assigned.");
            return;
        }
        UpdateShadowCollider();
    } // Cursor AI generated

    /// <summary>
    /// Returns the eight world-space corners of the axis-aligned box in the collider’s local space.
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
    } // Cursor AI generated

    /// <summary>
    /// Sorts hit points around their centroid in the XZ plane, builds a fan mesh, and spawns the shadow object.
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
        _shadowMeshCollider.convex = false;
        _shadowMeshCollider.isTrigger = false;

        TrySetShadowTag(_shadowObject, shadowTag);
        TrySetShadowLayer(_shadowObject, shadowLayerName);
    } // Cursor AI generated

    static void TrySetShadowLayer(GameObject go, string layerName)
    {
        if (go == null || string.IsNullOrEmpty(layerName)) return;
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0)
        {
            Debug.LogWarning(
                "ShadowCreater: Layer \"" + layerName + "\" does not exist. Add it under Project Settings > Tags and Layers > Layers, or clear shadowLayerName.");
            return;
        }

        go.layer = layer;
    } // Cursor AI generated

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
                "ShadowCreater: Tag \"" + tagName + "\" is not defined in Project Settings > Tags and Layers. Add it or change shadowTag.");
        }
    } // Cursor AI generated

    /// <summary>
    /// Repositions the existing shadow root and uploads new vertex/triangle data without destroying the GameObject.
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
        _shadowMeshCollider.sharedMesh = _shadowMesh;
    } // Cursor AI generated

    private static Vector3 GetShadowCenter(List<Vector3> points)
    {
        Vector3 center = Vector3.zero;
        foreach (var p in points) center += p;
        return center / points.Count;
    } // Cursor AI generated

    private static List<Vector3> OrderPointsAroundCenter(List<Vector3> points, Vector3 center)
    {
        return points
            .OrderBy(p => Mathf.Atan2((p - center).z, (p - center).x))
            .ToList();
    } // Cursor AI generated

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
    } // Cursor AI generated
}
