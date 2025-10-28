// Assets/Scripts/MainMenuController.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainMenuController : MonoBehaviour
{
    [Header("Login Panel")]
    public GameObject loginPanel;
    public TMP_InputField usernameInput;
    public Button loginButton;

    [Header("Main Menu Panel")]
    public GameObject mainMenuPanel;
    public TMP_Text welcomeText;
    public Button startScanButton;
    public Button myArtworksButton;
    public Button settingsButton;
    public Button logoutButton;

    [Header("Artworks Gallery")]
    public GameObject galleryPanel;
    public Transform galleryContent;
    public GameObject artworkItemPrefab;

    void Start()
    {
        loginButton.onClick.AddListener(OnLogin);
        startScanButton.onClick.AddListener(OnStartScan);
        myArtworksButton.onClick.AddListener(OnShowGallery);
        logoutButton.onClick.AddListener(OnLogout);

        ShowLoginPanel();
    }

    void OnLogin()
    {
        string username = usernameInput.text.Trim();
        if (string.IsNullOrEmpty(username))
        {
            Debug.LogWarning("Username cannot be empty");
            return;
        }

        UserAccountManager.Instance.Login(username);
        welcomeText.text = $"Welcome, {username}!";

        loginPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    void OnStartScan()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("01_ARMain");
    }

    void OnShowGallery()
    {
        // Clear existing
        foreach (Transform child in galleryContent)
        {
            Destroy(child.gameObject);
        }

        // Populate with saved artworks
        var artworks = UserAccountManager.Instance.CurrentUser?.artworks;
        if (artworks != null)
        {
            foreach (var artwork in artworks)
            {
                var item = Instantiate(artworkItemPrefab, galleryContent);
                var nameText = item.transform.Find("NameText").GetComponent<TMP_Text>();
                var dateText = item.transform.Find("DateText").GetComponent<TMP_Text>();
                var thumbnail = item.transform.Find("Thumbnail").GetComponent<RawImage>();
                var loadButton = item.transform.Find("LoadButton").GetComponent<Button>();

                nameText.text = artwork.name;
                dateText.text = artwork.createdDate.ToString("yyyy-MM-dd HH:mm");

                // Decode thumbnail
                if (!string.IsNullOrEmpty(artwork.thumbnailBase64))
                {
                    byte[] bytes = System.Convert.FromBase64String(artwork.thumbnailBase64);
                    Texture2D tex = new Texture2D(256, 256);
                    tex.LoadImage(bytes);
                    thumbnail.texture = tex;
                }

                loadButton.onClick.AddListener(() => LoadArtwork(artwork));
            }
        }

        galleryPanel.SetActive(true);
    }

    void LoadArtwork(SavedArtwork artwork)
    {
        // Store artwork to load in next scene
        PlayerPrefs.SetString("LoadArtworkId", artwork.artworkId);
        OnStartScan();
    }

    void OnLogout()
    {
        UserAccountManager.Instance.Logout();
        ShowLoginPanel();
    }

    void ShowLoginPanel()
    {
        loginPanel.SetActive(true);
        mainMenuPanel.SetActive(false);
        galleryPanel.SetActive(false);
    }
}