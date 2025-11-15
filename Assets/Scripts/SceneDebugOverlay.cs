using UnityEngine;
using UnityEngine.SceneManagement;
public class SceneDebugOverlay : MonoBehaviour
{
    void Start()
    {
        string all = "";
        for (int i = 0; i < SceneManager.sceneCount; i++)
            all += SceneManager.GetSceneAt(i).name + (i < SceneManager.sceneCount - 1 ? ", " : "");
        Debug.Log("[SceneDebug] Loaded: " + all);
        Debug.Log("[SceneDebug] Active: " + SceneManager.GetActiveScene().name);
    }
}
