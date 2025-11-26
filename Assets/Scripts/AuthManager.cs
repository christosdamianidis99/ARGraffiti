using UnityEngine;

public class AuthManager : MonoBehaviour
{
    public static AuthManager Instance { get; private set; }

    public bool IsLoggedIn => AuthState.I != null && AuthState.I.IsSignedIn;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Ensure AuthState exists
        if (AuthState.I == null)
        {
            var authStateObj = new GameObject("AuthState");
            authStateObj.AddComponent<AuthState>();
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void OnGoogleSignedIn(string displayName, string email, string idToken)
    {
        if (AuthState.I != null)
        {
            AuthState.I.SetSignedIn(displayName, email, idToken);
        }
        else
        {
            Debug.LogError("[AuthManager] AuthState.I is null. Cannot save sign-in data.");
        }
    }

    public void SignOut()
    {
        if (AuthState.I != null)
        {
            AuthState.I.SignOutLocal();
        }
    }
}

