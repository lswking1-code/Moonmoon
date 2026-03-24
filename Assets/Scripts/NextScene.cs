using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 玩家进入本物体上的触发 Collider 时，加载指定场景（目标场景须已加入 Build Settings）。
/// </summary>
public class NextScene : MonoBehaviour
{
    [SerializeField]
    [Tooltip("要切换到的场景名（与 File → Build Settings 中的场景名称一致）。")]
    private string targetSceneName;

    [SerializeField]
    [Tooltip("仅当进入触发器的物体 Tag 与此一致时才切换场景。")]
    private string playerTag = "Player";

    private void OnTriggerEnter(Collider other)
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogWarning("[NextScene] 未设置 targetSceneName。", this);
            return;
        }

        if (!other.CompareTag(playerTag))
            return;

        SceneManager.LoadScene(targetSceneName);
    }
}
