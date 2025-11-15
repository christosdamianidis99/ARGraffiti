using UnityEngine;

// No Parse SDK used. We just push keys into Back4AppRest.
public class ParseBootstrap : MonoBehaviour
{
    [Header("Back4App (REST)")]
    [SerializeField] private string applicationId = "K0S8yerNa3kdXP5VgzQZPVUybaOYtEnqX4o5ZTNJ";
    [SerializeField] private string restApiKey = "w93ikuQlNmfm7Qp8aoBtggeEv8cyKWrDmmPN3hAm";
    [SerializeField] private string serverUrl = "https://parseapi.back4app.com";

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Back4AppRest.AppId = applicationId;
        Back4AppRest.RestKey = restApiKey;
        Back4AppRest.ServerUrl = serverUrl;
    }
}
