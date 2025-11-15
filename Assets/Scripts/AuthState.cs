using System;
using UnityEngine;

public class AuthState : MonoBehaviour
{
    public static AuthState I;

    public bool IsSignedIn { get; private set; }
    public string DisplayName { get; private set; }
    public string Email { get; private set; }
    public string IdToken { get; private set; }

    public event Action<bool> OnAuthChanged;

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        IsSignedIn = PlayerPrefs.GetInt("auth.signed", 0) == 1;
        DisplayName = PlayerPrefs.GetString("auth.name", "");
        Email = PlayerPrefs.GetString("auth.email", "");
        IdToken = PlayerPrefs.GetString("auth.idtoken", "");
    }

    public void SetSignedIn(string displayName, string email, string idToken)
    {
        bool prev = IsSignedIn;

        IsSignedIn = true;
        DisplayName = displayName ?? "";
        Email = email ?? "";
        IdToken = idToken ?? "";

        PlayerPrefs.SetInt("auth.signed", 1);
        PlayerPrefs.SetString("auth.name", DisplayName);
        PlayerPrefs.SetString("auth.email", Email);
        PlayerPrefs.SetString("auth.idtoken", IdToken);
        PlayerPrefs.Save();

        if (prev != IsSignedIn) OnAuthChanged?.Invoke(IsSignedIn);
    }

    public void SignOutLocal()
    {
        bool prev = IsSignedIn;

        IsSignedIn = false;
        DisplayName = Email = IdToken = "";

        PlayerPrefs.DeleteKey("auth.signed");
        PlayerPrefs.DeleteKey("auth.name");
        PlayerPrefs.DeleteKey("auth.email");
        PlayerPrefs.DeleteKey("auth.idtoken");

        if (prev != IsSignedIn) OnAuthChanged?.Invoke(IsSignedIn);
    }
}
