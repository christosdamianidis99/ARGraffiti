using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video; // Add this line

public class SplashLoader : MonoBehaviour
{
    public float minDisplayTime = 1.5f; // Minimum time to show splash
    public VideoPlayer splashVideoPlayer; // Assign in Inspector
    public string nextSceneName = "01_ARMain"; // Or your main scene name

    private float _startTime;

    IEnumerator Start()
    {
        _startTime = Time.time;

        if (splashVideoPlayer == null)
        {
            Debug.LogError("Splash Video Player not assigned in SplashLoader!");
            yield return new WaitForSeconds(minDisplayTime); // Fallback to delay
        }
        else
        {
            // Ensure video player is ready and playing
            splashVideoPlayer.Prepare();
            while (!splashVideoPlayer.isPrepared)
            {
                yield return null; // Wait until prepared
            }
            splashVideoPlayer.Play();

            // Wait until video finishes OR minDisplayTime passes
            while (splashVideoPlayer.isPlaying || (Time.time - _startTime) < minDisplayTime)
            {
                yield return null;
            }
        }

        // Ensure minimum display time has passed even if video was shorter
        float elapsedTime = Time.time - _startTime;
        if (elapsedTime < minDisplayTime)
        {
            yield return new WaitForSeconds(minDisplayTime - elapsedTime);
        }

        SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
    }
}