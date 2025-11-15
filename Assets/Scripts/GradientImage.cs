using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[RequireComponent(typeof(Image))]
public class GradientImage : BaseMeshEffect
{
    [ColorUsage(false, true)] public Color top = new Color(0.09f, 0.10f, 0.14f, 1f);   // #171A24
    [ColorUsage(false, true)] public Color bottom = new Color(0.16f, 0.18f, 0.26f, 1f); // #2A2E42

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;

        var verts = new List<UIVertex>();
        vh.GetUIVertexStream(verts);

        // y-range
        float minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < verts.Count; i++)
        {
            var y = verts[i].position.y;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }
        float height = maxY - minY + Mathf.Epsilon;

        for (int i = 0; i < verts.Count; i++)
        {
            var v = verts[i];
            float t = (v.position.y - minY) / height;
            v.color = Color.Lerp(bottom, top, t);
            verts[i] = v;
        }
        vh.Clear();
        vh.AddUIVertexTriangleStream(verts);
    }
}
