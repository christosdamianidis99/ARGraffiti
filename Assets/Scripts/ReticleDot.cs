using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARRaycastManager))]
public class ReticleDot : MonoBehaviour
{
    public Image reticleUI;     
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

        if (reticleUI != null)
        {
            reticleRectTransform = reticleUI.GetComponent<RectTransform>();
        }

        if (painter == null)
        {
            painter = FindFirstObjectByType<PhonePainter>();
        }

        if (arCamera == null)
        {
            arCamera = Camera.main;
            if (arCamera == null)
            {
                arCamera = FindAnyObjectByType<Camera>();
            }
        }
    }

    void Start()
    {
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
        if (ARSession.state == ARSessionState.SessionTracking)
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
        }
        else
        {
            isOverAnyPlane = false;
            planeUnderReticle = null;
        }


        if (painter != null && reticleRectTransform != null)
        {
            UpdateDotSize();
            lastBrushSize = painter.brushSize;
        }


        if (painter != null && reticleUI != null)
        {
            if (painter.color != lastColor)
            {
                UpdateDotColor();
                lastColor = painter.color;
            }
            else
            {
                UpdateDotColor();
            }
        }
    }

    void UpdateDotSize()
    {
        if (painter == null || reticleRectTransform == null) return;

        float worldSize = painter.brushSize; 

        float dotSize = minDotSize;

        if (arCamera != null)
        {
            float distance = referenceDistance;

            if (isOverAnyPlane && hits.Count > 0)
            {
                Vector3 hitWorldPos = hits[0].pose.position;
                distance = Vector3.Distance(arCamera.transform.position, hitWorldPos);

                if (distance < 0.1f) distance = 0.1f;
            }
            else if (selectedPlane != null)
            {
                distance = Vector3.Distance(arCamera.transform.position, selectedPlane.transform.position);
                if (distance < 0.1f) distance = 0.1f;
            }
            float halfFOVRad = arCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float pixelsPerMeter = Screen.height / (2f * Mathf.Tan(halfFOVRad) * distance);

            float screenSize = worldSize * pixelsPerMeter;

            dotSize = Mathf.Clamp(screenSize, minDotSize, maxDotSize);
        }
        else
        {
            float minBrushSize = 0.02f;
            float maxBrushSize = 0.2f;
            float normalizedSize = Mathf.InverseLerp(minBrushSize, maxBrushSize, Mathf.Clamp(painter.brushSize, minBrushSize, maxBrushSize));
            dotSize = Mathf.Lerp(minDotSize, maxDotSize, normalizedSize);
        }

        reticleRectTransform.sizeDelta = new Vector2(dotSize, dotSize);
    }

    void UpdateDotColor()
    {
        if (painter == null || reticleUI == null) return;

        if (isOverAnyPlane && selectedPlane && planeUnderReticle &&
            planeUnderReticle.trackableId == selectedPlane.trackableId)
        {
            reticleUI.color = new Color(painter.color.r, painter.color.g, painter.color.b, 1f);
        }
        else if (isOverAnyPlane)
        {
            reticleUI.color = new Color(painter.color.r, painter.color.g, painter.color.b, 0.8f);
        }
        else
        {
            reticleUI.color = new Color(painter.color.r, painter.color.g, painter.color.b, 0.6f);
        }
    }
}