using UnityEngine;
using UnityEngine.SceneManagement;
public class ForceUnloadSplash : MonoBehaviour
{
    void Start()
    {
        var s = SceneManager.GetSceneByName("00_Splash");
        if (s.IsValid() && s.isLoaded)
        {
            Debug.Log("[Login] Unloading lingering Splash");
            SceneManager.UnloadSceneAsync(s);
        }
    }
}
