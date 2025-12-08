using System.IO;
using UnityEngine;

public class GraffitiARLoader : MonoBehaviour
{
    [Header("Spawn")]
    public Vector3 defaultSizeMeters = new Vector3(1.2f, 1.2f, 1f); 

    [Tooltip("Optional parent for spawned quads (e.g., an ARSessionOrigin child)")]
    public Transform parent;

    void Start()
    {
        var id = PlayerPrefs.GetString("graffiti.last_id", "");
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("[GraffitiARLoader] No graffiti id provided.");
            return;
        }

        var data = GraffitiRepository.I.Get(id);
        if (data == null)
        {
            Debug.LogWarning($"[GraffitiARLoader] Not found: {id}");
            return;
        }

        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        if (parent) go.transform.SetParent(parent, false);

        go.name = "GraffitiView_" + data.id;
        go.transform.position = data.position;
        go.transform.rotation = data.rotation;
        go.transform.localScale = data.localScale == Vector3.zero ? defaultSizeMeters : data.localScale;

        var mr = go.GetComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Unlit/Texture"));
        mr.material = mat;

        if (File.Exists(data.pngPath))
        {
            var bytes = File.ReadAllBytes(data.pngPath);
            var tex = new Texture2D(2, 2);
            tex.LoadImage(bytes);
            mat.mainTexture = tex;
        }


    }
}
