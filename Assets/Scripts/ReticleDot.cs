using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARRaycastManager))]
public class ReticleDot : MonoBehaviour
{
    public Image reticleUI;     // Canvas/Reticle (Raycast Target OFF)
    public ARPlane selectedPlane;
    
    [Header("Size Control")]
    [Tooltip("Reference to PhonePainter to get brush size")]
    public PhonePainter painter;
    
    [Tooltip("Reference to camera for distance calculation")]
    public Camera arCamera;
    
    [Tooltip("Minimum dot size in pixels (when brushSize is minimum)")]
    public float minDotSize = 20f;
    
    [Tooltip("Maximum dot size in pixels (when brushSize is maximum)")]
    public float maxDotSize = 120f;
    
    [Tooltip("Reference distance in meters for size calculation (default: 1 meter)")]
    public float referenceDistance = 1f;

    public bool isOverAnyPlane { get; private set; }
    public ARPlane planeUnderReticle { get; private set; }
    public Pose lastHitPose { get; private set; }

    ARRaycastManager rc;
    readonly List<ARRaycastHit> hits = new();
    private RectTransform reticleRectTransform;
    private float lastBrushSize = -1f;
    private Color lastColor = Color.clear;

    void Awake() 
    { 
        rc = GetComponent<ARRaycastManager>();
        
        // Get RectTransform for size control
        if (reticleUI != null)
        {
            reticleRectTransform = reticleUI.GetComponent<RectTransform>();
        }
        
        // Try to find painter if not assigned
        if (painter == null)
        {
            painter = FindObjectOfType<PhonePainter>();
        }
        
        // Try to find camera if not assigned
        if (arCamera == null)
        {
            arCamera = Camera.main;
            if (arCamera == null)
            {
                arCamera = FindObjectOfType<Camera>();
            }
        }
    }
    
    void Start()
    {
        // Initialize dot size and color based on current brush settings
        if (painter != null && reticleRectTransform != null)
        {
            UpdateDotSize();
            lastBrushSize = painter.brushSize;
        }
        if (painter != null && reticleUI != null)
        {
            UpdateDotColor();
            lastColor = painter.color;
        }
    }

    void Update()
    {
        var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        isOverAnyPlane = rc.Raycast(center, hits, TrackableType.PlaneWithinPolygon);
        if (isOverAnyPlane)
        {
            var h = hits[0];
            planeUnderReticle = h.trackable as ARPlane;
            lastHitPose = h.pose;
        }
        else
        {
            planeUnderReticle = null;
        }
        
        // Update dot size based on brush size and distance
        // Always update size because distance changes as camera moves
        if (painter != null && reticleRectTransform != null)
        {
            UpdateDotSize();
            lastBrushSize = painter.brushSize;
        }
        
        // Update dot color based on brush color and plane state
        // Always update color to reflect current state (plane detection + brush color)
        if (painter != null && reticleUI != null)
        {
            if (painter.color != lastColor)
            {
                UpdateDotColor();
                lastColor = painter.color;
            }
            else
            {
                // Also update color when plane state changes (even if color hasn't changed)
                UpdateDotColor();
            }
        }
    }
    
    void UpdateDotSize()
    {
        if (painter == null || reticleRectTransform == null) return;
        
        // Calculate dot size based on brush size in world space
        // Convert world space size to screen space pixels to match actual paint size
        float worldSize = painter.brushSize; // brush size in meters (this is what gets painted)
        
        float dotSize = minDotSize;
        
        if (arCamera != null)
        {
            float distance = referenceDistance;
            
            // If we have a hit point, use actual distance for accurate size calculation
            if (isOverAnyPlane && hits.Count > 0)
            {
                Vector3 hitWorldPos = hits[0].pose.position;
                distance = Vector3.Distance(arCamera.transform.position, hitWorldPos);
                
                // Ensure minimum distance to avoid division by zero
                if (distance < 0.1f) distance = 0.1f;
            }
            else if (selectedPlane != null)
            {
                // Use distance to selected plane center as fallback
                distance = Vector3.Distance(arCamera.transform.position, selectedPlane.transform.position);
                if (distance < 0.1f) distance = 0.1f;
            }
            
            // Convert world size (meters) to screen pixels
            // Formula: pixels = worldSize * (screenHeight / (2 * tan(FOV/2) * distance))
            float halfFOVRad = arCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float pixelsPerMeter = Screen.height / (2f * Mathf.Tan(halfFOVRad) * distance);
            
            // Calculate screen size for the brush
            float screenSize = worldSize * pixelsPerMeter;
            
            // Clamp to reasonable min/max range for UI visibility
            dotSize = Mathf.Clamp(screenSize, minDotSize, maxDotSize);
        }
        else
        {
            // Fallback: simple linear mapping when camera not available
            float minBrushSize = 0.02f;
            float maxBrushSize = 0.2f;
            float normalizedSize = Mathf.InverseLerp(minBrushSize, maxBrushSize, Mathf.Clamp(painter.brushSize, minBrushSize, maxBrushSize));
            dotSize = Mathf.Lerp(minDotSize, maxDotSize, normalizedSize);
        }
        
        // Update dot size to match paint size
        reticleRectTransform.sizeDelta = new Vector2(dotSize, dotSize);
    }
    
    void UpdateDotColor()
    {
        if (painter == null || reticleUI == null) return;
        
        // Set dot color to match brush color
        // If over selected plane, keep some visual indication (slightly brighter)
        if (isOverAnyPlane && selectedPlane && planeUnderReticle &&
            planeUnderReticle.trackableId == selectedPlane.trackableId)
        {
            // On selected plane: use brush color with full alpha
            reticleUI.color = new Color(painter.color.r, painter.color.g, painter.color.b, 1f);
        }
        else if (isOverAnyPlane)
        {
            // On plane but not selected: use brush color with slightly reduced alpha
            reticleUI.color = new Color(painter.color.r, painter.color.g, painter.color.b, 0.8f);
        }
        else
        {
            // Not on any plane: use brush color with reduced alpha
            reticleUI.color = new Color(painter.color.r, painter.color.g, painter.color.b, 0.6f);
        }
    }
}
