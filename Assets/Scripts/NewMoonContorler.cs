using UnityEngine;

public class NewMoonContorler : MonoBehaviour
{
    [Header("月亮参考点（ShadowCreater 的 LightReference）")]
    public Transform lightReference;

    [Header("用于表现月光的方向光（Type 应为 Directional）")]
    public Light directionalLight;

    [Header("场景中心点（可选，不填则使用世界原点）")]
    public Transform sceneCenter;

    private void LateUpdate()
    {
        if (lightReference == null || directionalLight == null)
            return;

        Vector3 centerPos = sceneCenter != null ? sceneCenter.position : Vector3.zero;
        Vector3 dir = (centerPos - lightReference.position).normalized;

        directionalLight.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }
}
