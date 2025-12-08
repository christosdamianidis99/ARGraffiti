using System;
using UnityEngine;

[Serializable]
public class GraffitiData
{
    public string id;                
    public string title;    
    public string pngPath;            
    public string thumbPath;
    public long createdUtcTicks;

    
    public string ownerEmail;
    public string ownerName;

    
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 localScale = Vector3.one;

    
    public bool hasGeospatial;
    public double latitude;
    public double longitude;
    public double altitude;
    public double heading;           
    public float horizontalAccMeters;
    public float verticalAccMeters;

    public DateTime CreatedUtc => new DateTime(createdUtcTicks, DateTimeKind.Utc);
}
