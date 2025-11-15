using System.IO;
using UnityEngine;

public class GraffitiARLoader : MonoBehaviour
{
    [Header("Spawn")]
    public Vector3 defaultSizeMeters = new Vector3(1.2f, 1.2f, 1f); // width/height of quad

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

        // Spawn a quad with the image
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

#if ARCORE_EXTENSIONS_PRESENT
        // If we have a geospatial record, prefer creating an AR geospatial anchor
        if (data.hasGeospatial)
        {
            var anchorMgr = Object.FindFirstObjectByType<Google.XR.ARCoreExtensions.ARAnchorManager>();
            if (GeospatialAnchorUtil.TryCreateAnchor(anchorMgr, data.latitude, data.longitude, data.altitude, out var geoAnchor))
            {
                go.transform.SetParent(geoAnchor.transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = data.localScale == Vector3.zero ? defaultSizeMeters : data.localScale;
                Debug.Log("[GraffitiARLoader] Geospatial anchor placed.");
            }
            else
            {
                Debug.LogWarning("[GraffitiARLoader] Geospatial not ready; using saved world pose as fallback.");
            }
        }
#endif

    }
}
