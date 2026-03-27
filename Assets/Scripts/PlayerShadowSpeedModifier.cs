using UnityEngine;

[DefaultExecutionOrder(-50)]
public class PlayerShadowSpeedModifier : MonoBehaviour
{
    [Header("References")]
    public JoystickMovement movement;
    public Collider playerCollider;

    [Header("Detection")]
    public string shadowTag = "Shadow";
    public float boundsPadding = 0.05f;

    [Header("Speed")]
    public float moveSpeedInShadow = 1f;

    [Header("Shadow value")]
    public float shadowValueGrowthPerSecond = 1f;
    public float shadowValueDecayPerSecond = 1f;
    public float shadowValueMax = 100f;

    [SerializeField]
    float _shadowValue;

    public float ShadowValue => _shadowValue;

    const int OverlapCapacity = 32;
    readonly Collider[] _overlapBuffer = new Collider[OverlapCapacity];

    float _savedMoveSpeed;
    bool _wasInShadow;

    void Awake()
    {
        movement ??= GetComponent<JoystickMovement>() ?? GetComponentInParent<JoystickMovement>();
        playerCollider ??= FindFirstSolidCollider();
    }

    Collider FindFirstSolidCollider()
    {
        var cols = GetComponentsInChildren<Collider>();
        foreach (var c in cols)
        {
            if (!c.isTrigger) return c;
        }
        return cols.Length > 0 ? cols[0] : null;
    }

    void FixedUpdate()
    {
        if (playerCollider == null) return;

        bool inShadow = IsOverlappingAnyShadowCollider();
        float dt = Time.fixedDeltaTime;

        if (inShadow)
        {
            float add = shadowValueGrowthPerSecond * dt;
            _shadowValue = shadowValueMax > 0f
                ? Mathf.Min(shadowValueMax, _shadowValue + add)
                : _shadowValue + add;
        }
        else
            _shadowValue = Mathf.Max(0f, _shadowValue - shadowValueDecayPerSecond * dt);

        if (movement == null) return;

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

        int count = Physics.OverlapBoxNonAlloc(
            b.center,
            b.extents,
            _overlapBuffer,
            playerCollider.transform.rotation,
            ~0,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            Collider c = _overlapBuffer[i];
            if (c == null || c == playerCollider || !c.CompareTag(shadowTag)) continue;
            return true;
        }

        return false;
    }
}
