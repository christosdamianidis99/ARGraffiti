using UnityEngine;
using UnityEngine.SceneManagement;

public class ARGatekeeper : MonoBehaviour
{
    private void Start()
    {
        if (AuthManager.Instance == null || !AuthManager.Instance.IsLoggedIn)
            SceneManager.LoadScene("01_LoginScene");
    }
}
