using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

[RequireComponent(typeof(VideoPlayer))]
public class SplashVideoLoader : MonoBehaviour
{
    [SerializeField] string nextSceneName = "01_LoginScene";
    [SerializeField] float maxDurationSec = 1.2f;
    [SerializeField] bool skipOnTap = true;

    VideoPlayer vp; bool loading;

    void Awake()
    {
        vp = GetComponent<VideoPlayer>();
        vp.isLooping = false;
        vp.loopPointReached += _ => LoadNext();
        vp.errorReceived += (_, msg) => { Debug.LogError("Video error: " + msg); LoadNext(); };
    }
    void Start()
    {
        vp.playbackSpeed = 1.5f; // shorter
        vp.Play();
        StartCoroutine(HardTimeout());
    }
    System.Collections.IEnumerator HardTimeout()
    {
        yield return new WaitForSecondsRealtime(maxDurationSec);
        LoadNext();
    }
    void Update()
    {
#if UNITY_EDITOR
        if (skipOnTap && !loading && Input.GetMouseButtonDown(0)) LoadNext();
#endif
        if (skipOnTap && !loading && Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) LoadNext();
    }
    void LoadNext()
    {
        if (loading) return;
        loading = true;
        SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single); // REPLACE
    }
}
