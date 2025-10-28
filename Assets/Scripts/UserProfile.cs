// Assets/Scripts/UserAccountManager.cs
using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class UserProfile
{
    public string userId;
    public string username;
    public List<SavedArtwork> artworks = new List<SavedArtwork>();
}

[Serializable]
public class SavedArtwork
{
    public string artworkId;
    public string name;
    public DateTime createdDate;
    public string thumbnailBase64;
    public List<StrokeData> strokes = new List<StrokeData>();
}

[Serializable]
public class StrokeData
{
    public List<DabData> dabs = new List<DabData>();
    public SerializableColor color;
    public int shapeType; // 0=circle, 1=square
    public float brushSize;
}

[Serializable]
public class DabData
{
    public SerializableVector3 position;
    public SerializableQuaternion rotation;
}

[Serializable]
public struct SerializableVector3
{
    public float x, y, z;
    public SerializableVector3(Vector3 v) { x = v.x; y = v.y; z = v.z; }
    public Vector3 ToVector3() => new Vector3(x, y, z);
}

[Serializable]
public struct SerializableQuaternion
{
    public float x, y, z, w;
    public SerializableQuaternion(Quaternion q) { x = q.x; y = q.y; z = q.z; w = q.w; }
    public Quaternion ToQuaternion() => new Quaternion(x, y, z, w);
}

[Serializable]
public struct SerializableColor
{
    public float r, g, b, a;
    public SerializableColor(Color c) { r = c.r; g = c.g; b = c.b; a = c.a; }
    public Color ToColor() => new Color(r, g, b, a);
}

public class UserAccountManager : MonoBehaviour
{
    public static UserAccountManager Instance { get; private set; }

    public UserProfile CurrentUser { get; private set; }

    private const string PREF_USER_ID = "CurrentUserId";

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Login(string username)
    {
        // Simple local login - extend with real authentication
        string userId = username.GetHashCode().ToString();
        CurrentUser = LoadUserProfile(userId);

        if (CurrentUser == null)
        {
            CurrentUser = new UserProfile
            {
                userId = userId,
                username = username
            };
            SaveUserProfile();
        }

        PlayerPrefs.SetString(PREF_USER_ID, userId);
        PlayerPrefs.Save();
    }

    public void Logout()
    {
        CurrentUser = null;
        PlayerPrefs.DeleteKey(PREF_USER_ID);
    }

    public void SaveArtwork(SavedArtwork artwork)
    {
        if (CurrentUser == null) return;

        artwork.artworkId = Guid.NewGuid().ToString();
        artwork.createdDate = DateTime.Now;
        CurrentUser.artworks.Add(artwork);
        SaveUserProfile();
    }

    private void SaveUserProfile()
    {
        if (CurrentUser == null) return;
        string json = JsonUtility.ToJson(CurrentUser);
        PlayerPrefs.SetString($"UserProfile_{CurrentUser.userId}", json);
        PlayerPrefs.Save();
    }

    private UserProfile LoadUserProfile(string userId)
    {
        string key = $"UserProfile_{userId}";
        if (PlayerPrefs.HasKey(key))
        {
            string json = PlayerPrefs.GetString(key);
            return JsonUtility.FromJson<UserProfile>(json);
        }
        return null;
    }
}