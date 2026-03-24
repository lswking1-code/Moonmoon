using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 根据 <see cref="PlayerShadowSpeedModifier.ShadowValue"/> 驱动全屏边缘向中心变黑（需配合 UI/ShadowValueVignette 材质）。
/// </summary>
/// <remarks>
/// 场景搭建步骤：
/// 1) 创建 Canvas，Render Mode = Screen Space - Overlay，Sort Order 调高（例如 100）。
/// 2) 子物体 Image：Anchor 全拉伸，Left/Right/Top/Bottom = 0，Color 白色（由 Shader 输出黑+Alpha）。可在编辑器中关闭 Image 以免挡预览；进入 Play 后本脚本会自动开启（请保持本脚本与所在物体处于启用状态）。
/// 3) Image 取消 Raycast Target，避免挡住点击。
/// 4) 将材质 <c>Assets/M/ShadowValueVignette.mat</c> 赋给 Image；本组件可指定同一材质并在 Instantiate Material 时自动赋给 Target Image。
/// 5) 将 <see cref="shadowSource"/> 指到玩家上的 <see cref="PlayerShadowSpeedModifier"/>。
/// </remarks>
public class ShadowValueVignetteController : MonoBehaviour
{
    [Tooltip("提供 ShadowValue / shadowValueMax 的玩家组件。")]
    public PlayerShadowSpeedModifier shadowSource;

    [Tooltip("使用 Shader UI/ShadowValueVignette 的材质（可用 Assets/M/ShadowValueVignette.mat）。")]
    public Material vignetteMaterial;

    [Tooltip("勾选后 Awake 时 Instantiate 一份材质，避免运行时改数值影响磁盘上的资源。")]
    public bool instantiateMaterial = true;

    [Tooltip("可选：生成实例后自动赋给该 Image（需与全屏暗角 Image 为同一对象或子物体）。")]
    public Image targetImage;

    [Tooltip("当 PlayerShadowSpeedModifier.shadowValueMax ≤ 0（无上限）时，用于把 ShadowValue 归一化到 0~1 的假定上限。")]
    public float visualMaxWhenUnlimited = 100f;

    [Tooltip("大于等于 0 时每帧写入材质的 _Feather；负数表示不覆盖，使用材质 Inspector 中的值。")]
    public float featherOverride = -1f;

    Material _runtimeMaterial;

    void Awake()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();

        if (shadowSource == null)
            shadowSource = FindFirstObjectByType<PlayerShadowSpeedModifier>();

        if (vignetteMaterial == null)
        {
            Debug.LogWarning("[ShadowValueVignetteController] 未指定 vignetteMaterial。");
            return;
        }

        _runtimeMaterial = instantiateMaterial ? Instantiate(vignetteMaterial) : vignetteMaterial;

        if (targetImage != null)
            targetImage.material = _runtimeMaterial;
        else if (instantiateMaterial)
            Debug.LogWarning(
                "[ShadowValueVignetteController] 已 Instantiate 材质但找不到 Target Image。请在同物体上加全屏 Image 并指定 Target Image，或关闭 Instantiate Material 并把 Image 的材质指到本组件使用的材质。");
    }

    private void Start()
    {
        ActivateImage();
    }

    void LateUpdate()
    {
        if (_runtimeMaterial == null || shadowSource == null)
            return;

        float denom = shadowSource.shadowValueMax > 0f
            ? shadowSource.shadowValueMax
            : Mathf.Max(1e-4f, visualMaxWhenUnlimited);

        float t = Mathf.Clamp01(shadowSource.ShadowValue / denom);
        _runtimeMaterial.SetFloat("_Strength", t);

        if (featherOverride >= 0f)
            _runtimeMaterial.SetFloat("_Feather", featherOverride);
    }

    public void ActivateImage()
    {
        if (targetImage != null)
            targetImage.enabled = true;
    }
}
