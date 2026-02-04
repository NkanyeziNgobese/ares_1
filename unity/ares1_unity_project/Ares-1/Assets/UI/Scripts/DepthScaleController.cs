using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class DepthScaleController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TelemetryManager telemetryManager;
    [SerializeField] private RectTransform scaleRoot;
    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform content;
    [SerializeField] private RectTransform markerLine;
    [SerializeField] private DepthTickView tickPrefab;

    [Header("Settings")]
    [SerializeField] private float halfWindowMeters = 150f;
    [SerializeField] private float minorStepMeters = 10f;
    [SerializeField] private float majorStepMeters = 50f;
    [SerializeField] private float pixelsPerMeter = 0.6f;
    [SerializeField] private int prewarmTicks = 60;

    [Header("Scale Mapping")]
    [SerializeField] private bool autoPixelsPerMeter = true;
    [SerializeField] private bool invertScroll = false;

    [Header("Insets (px)")]
    [SerializeField] private float topInsetPx = 0f;
    [SerializeField] private float bottomInsetPx = 0f;

    [Header("Debug")]
    [SerializeField] private bool debugEnabled = false;
    [SerializeField] private TMP_Text debugText;
    [SerializeField] private KeyCode debugToggleKey = KeyCode.F3;

    private readonly List<DepthTickView> _pool = new List<DepthTickView>(128);
    private float _lastDepth = float.PositiveInfinity;
    private float _lastMinDepth = float.PositiveInfinity;
    private float _lastMaxDepth = float.PositiveInfinity;
    private float _lastYCurrent;
    private bool _warnedZeroHeight;
    private bool _warnedRootNotChild;
    private bool _warnedViewportMissing;

    private void Awake()
    {
        ResolveContent();
        Prewarm();
        SetDebugVisible(debugEnabled);
    }

    private void OnValidate()
    {
        ResolveContent();
        if (minorStepMeters <= 0f) minorStepMeters = 1f;
        if (majorStepMeters < minorStepMeters) majorStepMeters = minorStepMeters;
        if (pixelsPerMeter <= 0f) pixelsPerMeter = 0.1f;
        float minHalfWindow = minorStepMeters * 10f;
        if (halfWindowMeters < minHalfWindow) halfWindowMeters = minHalfWindow;
        if (prewarmTicks < 0) prewarmTicks = 0;
        if (topInsetPx < 0f) topInsetPx = 0f;
        if (bottomInsetPx < 0f) bottomInsetPx = 0f;
        if (debugText) debugText.gameObject.SetActive(debugEnabled);
    }

    private void Update()
    {
        if (Input.GetKeyDown(debugToggleKey))
        {
            debugEnabled = !debugEnabled;
            SetDebugVisible(debugEnabled);
        }

        if (!telemetryManager || !content || !tickPrefab) return;

        if (!_warnedRootNotChild)
        {
            var rootTransform = content.transform;
            if (rootTransform != transform && !rootTransform.IsChildOf(transform))
            {
                Debug.LogWarning("DepthScaleController: content is not a child of this controller. Check wiring to DepthScaleContent.", this);
                _warnedRootNotChild = true;
            }
        }

        float currentDepth = telemetryManager.CurrentDepth;
        if (float.IsInfinity(_lastDepth) || Mathf.Abs(currentDepth - _lastDepth) >= minorStepMeters * 0.25f)
        {
            Rebuild(currentDepth);
            _lastDepth = currentDepth;
        }

        UpdateContentPosition(currentDepth);
        UpdateDebug(currentDepth);
    }

    private void Prewarm()
    {
        if (!content || !tickPrefab) return;
        for (int i = _pool.Count; i < prewarmTicks; i++)
        {
            var tick = Instantiate(tickPrefab, content);
            tick.gameObject.SetActive(false);
            _pool.Add(tick);
        }
    }

    private void Rebuild(float currentDepth)
    {
        float minDepth = currentDepth - halfWindowMeters;
        float maxDepth = currentDepth + halfWindowMeters;
        float step = Mathf.Max(0.0001f, minorStepMeters);
        float rawHeight = 0f;

        if (viewport)
        {
            rawHeight = viewport.rect.height;
        }
        else if (content)
        {
            rawHeight = content.rect.height;
            if (!_warnedViewportMissing)
            {
                Debug.LogWarning("DepthScaleController: viewport is not assigned; using content height for auto scaling.", this);
                _warnedViewportMissing = true;
            }
        }

        if (rawHeight <= 1f && !_warnedZeroHeight)
        {
            Debug.LogWarning("DepthScaleController: viewport height is 0/too small. Layout may not be built yet.", this);
            _warnedZeroHeight = true;
        }

        float usableHeight = Mathf.Max(1f, rawHeight - topInsetPx - bottomInsetPx);
        if (autoPixelsPerMeter)
        {
            pixelsPerMeter = usableHeight / Mathf.Max(1f, 2f * halfWindowMeters);
        }

        _lastMinDepth = minDepth;
        _lastMaxDepth = maxDepth;

        float start = Mathf.Floor(minDepth / step) * step;
        int activeCount = 0;

        for (float d = start; d <= maxDepth + 0.0001f; d += step)
        {
            var tick = GetTick(activeCount++);
            ConfigureTick(tick, d, minDepth);
        }

        for (int i = activeCount; i < _pool.Count; i++)
            _pool[i].gameObject.SetActive(false);
    }

    private DepthTickView GetTick(int index)
    {
        if (index >= _pool.Count)
        {
            var tick = Instantiate(tickPrefab, content);
            _pool.Add(tick);
        }

        var t = _pool[index];
        if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
        return t;
    }

    private void ConfigureTick(DepthTickView tick, float depth, float minDepth)
    {
        bool isMajor = IsMajorTick(depth);
        tick.SetMajor(isMajor);
        if (isMajor) tick.SetLabel(depth);

        var rt = tick.transform as RectTransform;
        if (!rt) return;

        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);

        float y = topInsetPx + (depth - minDepth) * pixelsPerMeter;
        rt.anchoredPosition = new Vector2(0f, -y);
    }

    private bool IsMajorTick(float depth)
    {
        float step = Mathf.Max(0.0001f, majorStepMeters);
        float remainder = Mathf.Abs(depth % step);
        return remainder < 0.001f || Mathf.Abs(step - remainder) < 0.001f;
    }

    private void UpdateContentPosition(float currentDepth)
    {
        if (!content) return;
        if (float.IsInfinity(_lastMinDepth)) return;

        float yCurrent = topInsetPx + (currentDepth - _lastMinDepth) * pixelsPerMeter;
        if (invertScroll) yCurrent = -yCurrent;
        _lastYCurrent = yCurrent;

        float markerY = markerLine ? markerLine.anchoredPosition.y : 0f;
        var pos = content.anchoredPosition;
        pos.y = markerY + yCurrent;
        content.anchoredPosition = pos;
    }

    private void UpdateDebug(float currentDepth)
    {
        if (!debugEnabled || !debugText) return;

        float contentY = content ? content.anchoredPosition.y : 0f;
        debugText.text =
            $"depth {currentDepth:0.0} | min {(_lastMinDepth):0.0} max {(_lastMaxDepth):0.0} | ppm {pixelsPerMeter:0.###} | yCur {_lastYCurrent:0.0} | contentY {contentY:0.0} | invert {invertScroll}";
    }

    private void ResolveContent()
    {
        if (!content && scaleRoot) content = scaleRoot;
        if (!scaleRoot && content) scaleRoot = content;
    }

    private void SetDebugVisible(bool visible)
    {
        if (debugText) debugText.gameObject.SetActive(visible);
    }
}
