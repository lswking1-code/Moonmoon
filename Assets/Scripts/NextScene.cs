using UnityEngine;
using UnityEngine.SceneManagement;

public class NextScene : MonoBehaviour
{
    [SerializeField]
    private string targetSceneName;

    [SerializeField]
    private string playerTag = "Player";

    private void OnTriggerEnter(Collider other)
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogWarning("[NextScene] Non-set targetSceneName", this);
            return;
        }

        if (!other.CompareTag(playerTag))
            return;

        SceneManager.LoadScene(targetSceneName);
    }
}
