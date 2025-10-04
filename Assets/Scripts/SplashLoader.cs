using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SplashLoader : MonoBehaviour
{
    public float delay = 1.5f;
    IEnumerator Start()
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene("01_ARMain", LoadSceneMode.Single);
    }
}
