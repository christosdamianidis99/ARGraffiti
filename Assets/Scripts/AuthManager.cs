using UnityEngine;

public class AuthManager : MonoBehaviour
{
    public static AuthManager Instance { get; private set; }

    public bool IsLoggedIn { get; private set; }
    public string DisplayName { get; private set; }
    public string Email { get; private set; }
    public string IdToken { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void OnGoogleSignedIn(string name, string email, string token)
    {
        DisplayName = name;
        Email = email;
        IdToken = token;
        IsLoggedIn = true;

        Debug.Log($"[Auth] Google signed in as {name} ({email})");
    }

    public void OnGuestLogin()
    {
        DisplayName = "Guest";
        Email = null;
        IdToken = null;
        IsLoggedIn = true;

        Debug.Log("[Auth] Guest login");
    }

    public void SignOut()
    {
        Debug.Log("[Auth] Sign out");

        DisplayName = null;
        Email = null;
        IdToken = null;
        IsLoggedIn = false;
    }
}
