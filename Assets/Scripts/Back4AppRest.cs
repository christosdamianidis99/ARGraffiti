using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public static class Back4AppRest
{
    // Set these in ParseBootstrap (below) at runtime.
    public static string AppId;
    public static string RestKey;
    public static string ServerUrl = "https://parseapi.back4app.com"; // default

    public static UnityWebRequest NewRequest(string method, string path, string sessionToken = null, byte[] body = null, string contentType = "application/json")
    {
        var url = ServerUrl.TrimEnd('/') + path;
        var req = new UnityWebRequest(url, method);
        req.SetRequestHeader("X-Parse-Application-Id", AppId);
        req.SetRequestHeader("X-Parse-REST-API-Key", RestKey);
        req.SetRequestHeader("X-Parse-Revocable-Session", "1");
        if (!string.IsNullOrEmpty(sessionToken))
            req.SetRequestHeader("X-Parse-Session-Token", sessionToken);

        if (body != null)
        {
            req.uploadHandler = new UploadHandlerRaw(body);
            req.SetRequestHeader("Content-Type", contentType);
        }

        req.downloadHandler = new DownloadHandlerBuffer();
        return req;
    }

    public static IEnumerator Send(UnityWebRequest req, Action<long, string> done)
    {
        yield return req.SendWebRequest();
        var code = req.responseCode;
        var text = req.downloadHandler != null ? req.downloadHandler.text : "";
        done?.Invoke(code, text);
        req.Dispose();
    }

    public static byte[] JsonBytes(object o) => Encoding.UTF8.GetBytes(JsonUtility.ToJson(o));
}
