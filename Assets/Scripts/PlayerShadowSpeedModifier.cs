using UnityEngine;

/// <summary>
/// 挂在带 <see cref="JoystickMovement"/> 的玩家物体上（与碰撞体同一层级或父级均可）。
/// 每帧在物理步内用 <see cref="Physics.OverlapBoxNonAlloc"/> 判断是否仍与 Tag 为 <see cref="shadowTag"/> 的阴影 MeshCollider 重叠（可为非凸、非 Trigger），
/// 不依赖 Trigger 消息，避免动态网格阴影导致速度无法恢复。
/// 同时维护 <see cref="ShadowValue"/>：在阴影内按速率上升，离开阴影后按速率衰减至 0。
/// </summary>
[DefaultExecutionOrder(-50)]
public class PlayerShadowSpeedModifier : MonoBehaviour
{
    [Tooltip("要改速度的 JoystickMovement；留空则从本物体或父级上获取。")]
    public JoystickMovement movement;

    [Tooltip("用于与阴影做穿透检测的玩家碰撞体；留空则在本物体及子级上找第一个非 Trigger 的 Collider。")]
    public Collider playerCollider;

    [Tooltip("阴影物体使用的 Tag（需在 Project Settings > Tags 中创建，例如 Shadow）。")]
    public string shadowTag = "Shadow";

    [Tooltip("处于任意阴影区域内时的 moveSpeed。")]
    public float moveSpeedInShadow = 1f;

    [Tooltip("Overlap 粗筛时的额外膨胀（米），略大于 0 可减少漏检。")]
    public float boundsPadding = 0.05f;

    [Header("Shadow value")]
    [Tooltip("在阴影内时，ShadowValue 每秒增加的量。")]
    public float shadowValueGrowthPerSecond = 1f;

    [Tooltip("不在阴影内时，ShadowValue 每秒减少的量，直到为 0。")]
    public float shadowValueDecayPerSecond = 1f;

    [Tooltip("ShadowValue 上限；≤0 表示不限制。")]
    public float shadowValueMax = 100f;

    [Tooltip("当前阴影累积值（运行时，可在 Play 模式下观察）。")]
    [SerializeField]
    float _shadowValue;

    /// <summary>当前阴影累积值：在阴影内上升，离开阴影后按速率下降至 0。</summary>
    public float ShadowValue => _shadowValue;

    const int OverlapCapacity = 32;
    readonly Collider[] _overlapBuffer = new Collider[OverlapCapacity];

    float _savedMoveSpeed;
    bool _wasInShadow;

    void Awake()
    {
        if (movement == null)
            movement = GetComponent<JoystickMovement>() ?? GetComponentInParent<JoystickMovement>();

        if (playerCollider == null)
        {
            var cols = GetComponentsInChildren<Collider>();
            for (int i = 0; i < cols.Length; i++)
            {
                if (!cols[i].isTrigger)
                {
                    playerCollider = cols[i];
                    break;
                }
            }
            if (playerCollider == null && cols.Length > 0)
                playerCollider = cols[0];
        }
    }

    void FixedUpdate()
    {
        if (playerCollider == null)
            return;

        bool inShadow = IsOverlappingAnyShadowCollider();
        float dt = Time.fixedDeltaTime;

        if (inShadow)
        {
            float add = shadowValueGrowthPerSecond * dt;
            if (shadowValueMax > 0f)
                _shadowValue = Mathf.Min(shadowValueMax, _shadowValue + add);
            else
                _shadowValue += add;
        }
        else
            _shadowValue = Mathf.Max(0f, _shadowValue - shadowValueDecayPerSecond * dt);

        if (movement == null)
            return;

        if (inShadow && !_wasInShadow)
            _savedMoveSpeed = movement.moveSpeed;

        if (inShadow)
            movement.moveSpeed = moveSpeedInShadow;
        else if (_wasInShadow)
            movement.moveSpeed = _savedMoveSpeed;

        _wasInShadow = inShadow;
    }

    bool IsOverlappingAnyShadowCollider()
    {
        Bounds b = playerCollider.bounds;
        if (boundsPadding > 0f)
            b.Expand(boundsPadding * 2f);

        Quaternion rot = playerCollider.transform.rotation;
        int count = Physics.OverlapBoxNonAlloc(
            b.center,
            b.extents,
            _overlapBuffer,
            rot,
            ~0,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            Collider c = _overlapBuffer[i];
            if (c == null || c == playerCollider)
                continue;
            if (!c.CompareTag(shadowTag))
                continue;

            return true;
        }

        return false;
    }
}
