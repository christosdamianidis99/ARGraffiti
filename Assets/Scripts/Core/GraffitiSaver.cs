using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public class GraffitiSaver : MonoBehaviour
{
    private string RootPath =>
        Path.Combine(Application.persistentDataPath, "Graffiti");

    [Serializable]
    public class GraffitiRecord
    {
        public string graffitiId;

        public string ownerName;
        public string ownerEmail;

        public float posX;
        public float posY;
        public float posZ;

        public float rotX;
        public float rotY;
        public float rotZ;
        public float rotW;

        public string pngFilePath;
        public string createdAt;
    }

    public async Task SaveGraffitiAsync(Texture2D previewTexture,
                                        Pose pose,
                                        string graffitiId)
    {
        Directory.CreateDirectory(RootPath);

        string pngPath = Path.Combine(RootPath, graffitiId + ".png");
        string jsonPath = Path.Combine(RootPath, graffitiId + ".json");

        byte[] pngBytes = previewTexture.EncodeToPNG();
        await File.WriteAllBytesAsync(pngPath, pngBytes);

        var record = new GraffitiRecord
        {
            graffitiId = graffitiId,
            ownerName = AuthManager.Instance.DisplayName,
            ownerEmail = AuthManager.Instance.Email,

            posX = pose.position.x,
            posY = pose.position.y,
            posZ = pose.position.z,

            rotX = pose.rotation.x,
            rotY = pose.rotation.y,
            rotZ = pose.rotation.z,
            rotW = pose.rotation.w,

            pngFilePath = pngPath,
            createdAt = DateTime.UtcNow.ToString("o")
        };

        string json = JsonUtility.ToJson(record, true);
        await File.WriteAllTextAsync(jsonPath, json);

        Debug.Log($"[GraffitiSaver] Saved graffiti locally: {jsonPath}");
    }
}
