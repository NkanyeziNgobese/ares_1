using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class UiSparkline : MaskableGraphic
{
    [SerializeField] private int maxPoints = 40;
    [SerializeField] private float lineThickness = 2f;

    private readonly List<float> _samples = new List<float>(128);

    public void PushSample01(float v01)
    {
        v01 = Mathf.Clamp01(v01);
        _samples.Add(v01);
        if (_samples.Count > maxPoints) _samples.RemoveAt(0);
        SetVerticesDirty();
    }

    [ContextMenu("DEV: Fill With Test Wave")]
    private void DevFillWave()
    {
        for (int i = 0; i < 40; i++)
        {
            float v = 0.5f + 0.35f * Mathf.Sin(i * 0.35f);
            PushSample01(v);
        }
    }

    [ContextMenu("DEV: Clear Samples")]
    private void DevClear()
    {
        _samples.Clear();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_samples.Count < 2) return;

        Rect r = GetPixelAdjustedRect();
        float w = r.width;
        float h = r.height;

        float dx = w / (_samples.Count - 1);

        // Build thick line as quads per segment
        for (int i = 0; i < _samples.Count - 1; i++)
        {
            Vector2 p0 = new Vector2(r.xMin + dx * i, r.yMin + h * _samples[i]);
            Vector2 p1 = new Vector2(r.xMin + dx * (i + 1), r.yMin + h * _samples[i + 1]);

            AddSegment(vh, p0, p1, lineThickness, color);
        }
    }

    private static void AddSegment(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color32 col)
    {
        Vector2 dir = (b - a).normalized;
        Vector2 n = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);

        UIVertex v0 = UIVertex.simpleVert; v0.color = col; v0.position = a - n;
        UIVertex v1 = UIVertex.simpleVert; v1.color = col; v1.position = a + n;
        UIVertex v2 = UIVertex.simpleVert; v2.color = col; v2.position = b + n;
        UIVertex v3 = UIVertex.simpleVert; v3.color = col; v3.position = b - n;

        int idx = vh.currentVertCount;
        vh.AddVert(v0); vh.AddVert(v1); vh.AddVert(v2); vh.AddVert(v3);
        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx, idx + 2, idx + 3);
    }
}
