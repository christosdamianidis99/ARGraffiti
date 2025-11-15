using System;
using UnityEngine;

[Serializable]
public class GraffitiData
{
    public string id;                 // GUID
    public string title;              // optional (you can leave empty)
    public string pngPath;            // absolute path to PNG
    public string thumbPath;          // optional thumbnail
    public long createdUtcTicks;

    // Local AR pose (Unity world) saved at capture time
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 localScale = Vector3.one;

    // Optional geospatial fields (use if ARCore Extensions present)
    public bool hasGeospatial;
    public double latitude;
    public double longitude;
    public double altitude;
    public double heading;           // compass/heading if available
    public float horizontalAccMeters;
    public float verticalAccMeters;

    public DateTime CreatedUtc => new DateTime(createdUtcTicks, DateTimeKind.Utc);
}
