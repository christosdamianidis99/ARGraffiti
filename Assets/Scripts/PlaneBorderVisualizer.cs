using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

[RequireComponent(typeof(ARPlane))]
[RequireComponent(typeof(LineRenderer))]
public class PlaneBorderVisualizer : MonoBehaviour
{
    public float lineWidth = 0.01f;
    public Color lineColor = new Color(0f, 1f, 0.8f, 0.9f);
    ARPlane _plane;
    LineRenderer _lr;
    readonly List<Vector3> pts = new();

    void Awake()
    {
        _plane = GetComponent<ARPlane>();
        _lr = GetComponent<LineRenderer>();
        _lr.loop = true; _lr.useWorldSpace = false; _lr.widthMultiplier = lineWidth;
        if (_lr.material == null)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.color = lineColor; _lr.material = m;
        }
        _plane.boundaryChanged += OnBoundary;
    }
    void OnDestroy() => _plane.boundaryChanged -= OnBoundary;

    void OnBoundary(ARPlaneBoundaryChangedEventArgs e)
    {
        var b = _plane.boundary;
        if (!b.IsCreated || b.Length < 3) { _lr.positionCount = 0; return; }
        pts.Clear();
        for (int i = 0; i < b.Length; i++) pts.Add(new Vector3(b[i].x, 0f, b[i].y));
        _lr.positionCount = pts.Count; _lr.SetPositions(pts.ToArray());
    }
}
