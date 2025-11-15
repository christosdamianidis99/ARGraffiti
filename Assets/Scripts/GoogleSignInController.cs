using Google;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GoogleSignInController : MonoBehaviour
{
    [Header("Google OAuth")]
    public string webClientId;

    [Header("Scene Routing")]
    [SerializeField] private string AR_SCENE_NAME = "02_ARMain";

    [Header("UI")]
    public Button googleButton;
    public TMP_Text statusText;

    public bool requestIdToken = true;
    public bool requestAuthCode = false;
    public bool forceRefreshToken = false;

    GoogleSignInConfiguration config;

    void Awake()
    {
#if UNITY_ANDROID
        if (googleButton != null)
            googleButton.onClick.AddListener(SignInWithGoogle);

        config = new GoogleSignInConfiguration
        {
            WebClientId = webClientId != null ? webClientId.Trim() : "",
            RequestIdToken = requestIdToken,
            RequestEmail = true,
            RequestAuthCode = requestAuthCode,
            ForceTokenRefresh = forceRefreshToken
        };

        GoogleSignIn.Configuration = config;
        Log("Ready.");
#else
        Log("Google Sign-In requires Android build target.");
        if (googleButton != null)
            googleButton.interactable = false;
#endif
    }

    void Log(string msg)
    {
        if (statusText != null)
            statusText.text = msg;
        Debug.Log("[GoogleSignIn] " + msg);
    }

    public void SignInWithGoogle()
    {
#if UNITY_ANDROID
        Log("Signing in...");
        GoogleSignIn.Configuration = config;
        GoogleSignIn.DefaultInstance
            .SignIn()
            .ContinueWith(OnAuthFinished, TaskScheduler.FromCurrentSynchronizationContext());
#endif
    }

    void OnAuthFinished(Task<GoogleSignInUser> task)
    {
        if (task.IsFaulted)
        {
            var e = task.Exception != null && task.Exception.InnerException != null
                ? task.Exception.InnerException.Message
                : "Unknown error";
            Log("Google Sign-In failed: " + e);
            return;
        }

        if (task.IsCanceled)
        {
            Log("Google Sign-In canceled.");
            return;
        }

        var user = task.Result;
        var idToken = user != null ? user.IdToken : null;
        var email = user != null ? user.Email : null;
        var name = user != null ? user.DisplayName : null;

        if (AuthManager.Instance != null)
        {
            AuthManager.Instance.OnGoogleSignedIn(name, email, idToken);
            Debug.Log($"[GoogleSignIn] AuthManager updated. LoggedIn={AuthManager.Instance.IsLoggedIn}");
        }
        else
        {
            Debug.LogError("[GoogleSignIn] AuthManager.Instance is null – did you add it to the Login scene?");
        }

        Log("Signed in as " + name + " (" + email + ")");
        Debug.Log("ID Token length: " + (idToken != null ? idToken.Length : 0));

        if (!string.IsNullOrEmpty(AR_SCENE_NAME))
            SceneManager.LoadScene(AR_SCENE_NAME);
    }
}
