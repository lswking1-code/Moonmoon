using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Maps <see cref="PlayerShadowSpeedModifier.ShadowValue"/> to the UI/ShadowValueVignette material (<c>_Strength</c>, optional <c>_Feather</c>).
/// </summary>
/// <remarks>
/// Typical setup: Screen Space Overlay Canvas, full-screen <see cref="Image"/> (raycast off), assign vignette material, link <see cref="shadowSource"/>.
/// </remarks>
public class ShadowValueVignetteController : MonoBehaviour
{
    [Header("Source")]
    public PlayerShadowSpeedModifier shadowSource;

    [Header("Material")]
    public Material vignetteMaterial;
    public bool instantiateMaterial = true;
    public Image targetImage;

    [Header("Mapping")]
    public float visualMaxWhenUnlimited = 100f;
    public float featherOverride = -1f;

    Material _runtimeMaterial;

    void Awake()
    {
        targetImage ??= GetComponent<Image>();
        shadowSource ??= FindFirstObjectByType<PlayerShadowSpeedModifier>();

        if (vignetteMaterial == null)
        {
            Debug.LogWarning("[ShadowValueVignetteController] vignetteMaterial is not assigned.");
            return;
        }

        _runtimeMaterial = instantiateMaterial ? Instantiate(vignetteMaterial) : vignetteMaterial;
        if (targetImage != null)
            targetImage.material = _runtimeMaterial;
        else if (instantiateMaterial)
            Debug.LogWarning("[ShadowValueVignetteController] Material instantiated but no Image found; assign targetImage or disable instantiateMaterial.");
    }

    void Start() => ActivateImage();

    void LateUpdate()
    {
        if (_runtimeMaterial == null || shadowSource == null) return;

        float denom = shadowSource.shadowValueMax > 0f
            ? shadowSource.shadowValueMax
            : Mathf.Max(1e-4f, visualMaxWhenUnlimited);

        _runtimeMaterial.SetFloat("_Strength", Mathf.Clamp01(shadowSource.ShadowValue / denom));
        if (featherOverride >= 0f)
            _runtimeMaterial.SetFloat("_Feather", featherOverride);
    }

    public void ActivateImage()
    {
        if (targetImage != null) targetImage.enabled = true;
    }
}
