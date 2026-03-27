using UnityEngine;

public class NewMoonContorler : MonoBehaviour
{
    [Header("Moon reference")]
    public Transform lightReference;

    [Header("Directional light for moonlight")]
    public Light directionalLight;

    [Header("Scene center")]
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
