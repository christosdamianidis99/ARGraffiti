using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;


[RequireComponent(typeof(ARPlaneManager))]
public class PlaneQualityFilter : MonoBehaviour
{
    [Header("External (assign)")]
    public ARPlaneManager planeManager;
    public ARRaycastManager raycaster;      
    public Camera arCamera;                 

    [Header("Visibility")]
    public bool hideAllUntilCriteriaPass = true;
    public bool showOnlyPrimary = true;

    [Header("Quality Gates")]
    [Tooltip("Minimum planar area (m²) required to consider a plane candidate.")]
    public float minArea = 0.15f;                   
    [Tooltip("Minimum seconds a plane must exist before we consider it.")]
    public float minAge = 0.25f;                    
    [Tooltip("Plane is 'stable' when its smoothed area growth rate is below this (m²/s) for stableDwell seconds.")]
    public float maxAreaGrowthRate = 0.15f;         
    public float stableDwell = 0.25f;               

    [Header("Geometry Constraints")]
    [Tooltip("Accept only planes whose normal tilt (deg) is within this angle of the target alignment.")]
    public float maxNormalTiltDeg = 20f;           
    [Tooltip("Ignore planes closer than this to camera.")]
    public float minDistance = 0.25f;               
    [Tooltip("Ignore planes further than this from camera.")]
    public float maxDistance = 3.5f;               

    [Header("Alignment Preference")]
    public bool autoPickAlignmentFromFirstHit = true;
    public PlaneDetectionMode preferredMode = PlaneDetectionMode.Horizontal; 

    ARPlane _primary;                                
    readonly Dictionary<TrackableId, Info> _info = new();

    class Info
    {
        public double firstSeen;     
        public float lastArea;
        public float emaGrowth;      
        public double lastUpdate;    
        public double stableSince;   
    }
    public ARPlane PrimaryPlane => _primary;

    public bool PrimaryIsStable()
    {
        if (_primary == null) return false;
        return PassesGates(_primary, Time.realtimeSinceStartupAsDouble);
    }


    void Reset()
    {
        planeManager = GetComponent<ARPlaneManager>();
    }

    void OnEnable()
    {
        if (planeManager) planeManager.trackablesChanged.AddListener(OnPlanesChanged);
    }
    void OnDisable()
    {
        if (planeManager) planeManager.trackablesChanged.RemoveListener(OnPlanesChanged);
    }



    void Start()
    {
        if (!planeManager) planeManager = GetComponent<ARPlaneManager>();
        if (hideAllUntilCriteriaPass) ToggleAllMeshes(false);
    }

    void Update()
    {
        if (_primary)
        {
            var root = GetRoot(_primary);
            if (root != _primary) _primary = root;
            if (showOnlyPrimary) ShowOnly(_primary);
        }
        else if (hideAllUntilCriteriaPass)
        {
            ToggleAllMeshes(false);
        }

        if (autoPickAlignmentFromFirstHit && _primary == null && raycaster && arCamera)
        {
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var hits = ListPool<ARRaycastHit>.Get();
            if (raycaster.Raycast(center, hits, TrackableType.PlaneWithinPolygon) && hits.Count > 0)
            {
#if ARFOUNDATION_5_1_OR_NEWER
                var tmpPlane = planeManager.GetPlane(hits[0].trackableId);
#else
                var tmpPlane = planeManager.GetPlane(hits[0].trackableId);
#endif
                tmpPlane = GetRoot(tmpPlane);
                if (tmpPlane)
                {
                    var align = tmpPlane.alignment;
                    preferredMode = (align == PlaneAlignment.HorizontalUp || align == PlaneAlignment.HorizontalDown)
                        ? PlaneDetectionMode.Horizontal : PlaneDetectionMode.Vertical;
                }
            }
            ListPool<ARRaycastHit>.Release(hits);
        }
    }


    public void ResetFilterForScan()
    {
        _primary = null;
        _info.Clear();
        if (hideAllUntilCriteriaPass)
            ToggleAllMeshes(false);
    }


    public void ForceHideAllMeshes() => ToggleAllMeshes(false);


    public void RefreshVisibility()
    {
        if (!planeManager) return;

        if (!showOnlyPrimary)
        {
            ToggleAllMeshes(true);
            return;
        }

        if (_primary)
            ShowOnly(_primary);
        else if (hideAllUntilCriteriaPass)
            ToggleAllMeshes(false);
    }
    private void OnPlanesChanged(ARTrackablesChangedEventArgs<ARPlane> args)
    {
        double now = Time.realtimeSinceStartupAsDouble;

        foreach (var p in args.added)
        {
            if (!_info.ContainsKey(p.trackableId))
                _info[p.trackableId] = new Info { firstSeen = now, lastUpdate = now, stableSince = -1 };
        }

        foreach (var p in planeManager.trackables)
        {
            var root = GetRoot(p);
            var id = root.trackableId;
            if (!_info.TryGetValue(id, out var inf))
            {
                inf = new Info { firstSeen = now, lastUpdate = now, stableSince = -1 };
                _info[id] = inf;
            }

            float area = ComputeArea(root);
            float dt = Mathf.Max(1e-3f, (float)(now - inf.lastUpdate));
            float growth = Mathf.Max(0f, (area - inf.lastArea) / dt); 
            inf.emaGrowth = (inf.emaGrowth * 0.5f) + (growth * 0.5f);

            if (inf.emaGrowth <= maxAreaGrowthRate)
            {
                if (inf.stableSince < 0) inf.stableSince = now;
            }
            else inf.stableSince = -1;

            inf.lastArea = area;
            inf.lastUpdate = now;
        }

        if (_primary == null)
        {
            ARPlane best = null;
            float bestScore = 0f;

            foreach (var p in planeManager.trackables)
            {
                var root = GetRoot(p);
                if (!PassesGates(root, now)) { Hide(root); continue; }

                float area = Mathf.Max(ComputeArea(root), 1e-4f);
                float dist = Vector3.Distance(arCamera ? arCamera.transform.position : Vector3.zero, root.transform.position);
                float score = area / Mathf.Max(dist, 0.2f);

                if (score > bestScore)
                {
                    best = root; bestScore = score;
                }
            }

            if (best != null)
            {
                _primary = best;
                if (showOnlyPrimary) ShowOnly(_primary); else Show(best);
            }
            else
            {
                if (hideAllUntilCriteriaPass) ToggleAllMeshes(false);
            }
        }
        else
        {
            if (showOnlyPrimary) ShowOnly(_primary);
        }
    }


    bool PassesGates(ARPlane p, double now)
    {
        if (!p) return false;

        if (preferredMode == PlaneDetectionMode.Horizontal)
        {
            if (!(p.alignment == PlaneAlignment.HorizontalUp || p.alignment == PlaneAlignment.HorizontalDown))
                return false;
        }
        else if (preferredMode == PlaneDetectionMode.Vertical)
        {
            if (!(p.alignment == PlaneAlignment.Vertical)) return false;
        }

        var n = p.transform.up;
        float tilt = 0f;
        if (preferredMode == PlaneDetectionMode.Horizontal)
        {
            tilt = Vector3.Angle(n, Vector3.up);
        }
        else if (preferredMode == PlaneDetectionMode.Vertical)
        {

            float toUp = Vector3.Angle(n, Vector3.up);
            tilt = Mathf.Abs(90f - toUp);
        }
        if (tilt > maxNormalTiltDeg) return false;

        float d = Vector3.Distance(arCamera ? arCamera.transform.position : Vector3.zero, p.transform.position);
        if (d < minDistance || d > maxDistance) return false;

        if (!_info.TryGetValue(p.trackableId, out var inf)) return false;

        double age = now - inf.firstSeen;
        if (age < minAge) return false;

        float area = ComputeArea(p);
        if (area < minArea) return false;

        if (inf.stableSince < 0) return false;
        if (now - inf.stableSince < stableDwell) return false;

        return true;
    }

    // ---------------- Utilities ----------------

    ARPlane GetRoot(ARPlane p)
    {
        while (p != null && p.subsumedBy != null) p = p.subsumedBy;
        return p;
    }

    void ShowOnly(ARPlane keep)
    {
        foreach (var p in planeManager.trackables)
        {
            var mr = p.GetComponent<MeshRenderer>();
            if (!mr) continue;
            mr.enabled = (GetRoot(p) == keep);
        }
    }

    void ToggleAllMeshes(bool visible)
    {
        foreach (var p in planeManager.trackables)
        {
            var mr = p.GetComponent<MeshRenderer>();
            if (mr) mr.enabled = visible;
        }
    }

    void Show(ARPlane p)
    {
        var mr = p.GetComponent<MeshRenderer>();
        if (mr) mr.enabled = true;
    }

    void Hide(ARPlane p)
    {
        var mr = p.GetComponent<MeshRenderer>();
        if (mr) mr.enabled = false;
    }

    static float ComputeArea(ARPlane p)
    {
        var b = p.boundary;
        if (!b.IsCreated || b.Length < 3) return 0f;

        double sum = 0.0;
        for (int i = 0; i < b.Length; i++)
        {
            int j = (i + 1) % b.Length;
            sum += (double)b[i].x * b[j].y - (double)b[j].x * b[i].y;
        }
        return Mathf.Abs((float)(0.5 * sum));
    }
}

static class ListPool<T>
{
    static readonly Stack<List<T>> pool = new();
    public static List<T> Get() => pool.Count > 0 ? pool.Pop() : new List<T>(16);
    public static void Release(List<T> list) { list.Clear(); pool.Push(list); }
}
