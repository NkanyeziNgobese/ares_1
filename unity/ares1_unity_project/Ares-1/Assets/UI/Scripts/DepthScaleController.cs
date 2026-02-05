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
    private float _lastCenterFromTop;
    private bool _warnedZeroHeight;
    private bool _warnedRootNotChild;
    private bool _warnedViewportMissing;
<<<<<<< HEAD
    private bool _hasBuiltOnce;
=======
>>>>>>> 166b2df92058ed6c3937a4080f723aaaa63f5f19

    private void Awake()
    {
        Prewarm();
        SetDebugVisible(debugEnabled);
    }

    private void OnValidate()
    {
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

<<<<<<< HEAD
        if (!telemetryManager || !scaleRoot || !tickPrefab || !viewport) return;
=======
        if (!telemetryManager || !scaleRoot || !tickPrefab) return;
>>>>>>> 166b2df92058ed6c3937a4080f723aaaa63f5f19

        if (!_warnedRootNotChild)
        {
            var rootTransform = scaleRoot.transform;
            if (rootTransform != transform && !rootTransform.IsChildOf(transform))
            {
                Debug.LogWarning("DepthScaleController: scaleRoot is not a child of this controller. Check wiring to DepthScaleContent.", this);
                _warnedRootNotChild = true;
            }
        }

<<<<<<< HEAD
        if (viewport.rect.height < 2f) return;

        float currentDepth = telemetryManager.CurrentDepth;
        if (!_hasBuiltOnce)
        {
            Rebuild(currentDepth);
            _hasBuiltOnce = true;
            _lastDepth = currentDepth;
            return;
        }

        if (ScrollPauseController.IsPaused) return;

        if (Mathf.Abs(currentDepth - _lastDepth) >= minorStepMeters * 0.25f)
=======
        float currentDepth = telemetryManager.CurrentDepth;
        if (float.IsInfinity(_lastDepth) || Mathf.Abs(currentDepth - _lastDepth) >= minorStepMeters * 0.25f)
>>>>>>> 166b2df92058ed6c3937a4080f723aaaa63f5f19
        {
            Rebuild(currentDepth);
            _lastDepth = currentDepth;
        }

        UpdateDebug(currentDepth);
    }

    private void Prewarm()
    {
        if (!scaleRoot || !tickPrefab) return;
        for (int i = _pool.Count; i < prewarmTicks; i++)
        {
            var tick = Instantiate(tickPrefab, scaleRoot);
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
        else if (scaleRoot)
        {
            rawHeight = scaleRoot.rect.height;
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

        _lastCenterFromTop = topInsetPx + usableHeight * 0.5f;
        _lastMinDepth = minDepth;
        _lastMaxDepth = maxDepth;

        float start = Mathf.Floor(minDepth / step) * step;
        int activeCount = 0;

        for (float d = start; d <= maxDepth + 0.0001f; d += step)
        {
            var tick = GetTick(activeCount++);
            ConfigureTick(tick, d, currentDepth);
        }

        for (int i = activeCount; i < _pool.Count; i++)
            _pool[i].gameObject.SetActive(false);
    }

    private DepthTickView GetTick(int index)
    {
        if (index >= _pool.Count)
        {
            var tick = Instantiate(tickPrefab, scaleRoot);
            _pool.Add(tick);
        }

        var t = _pool[index];
        if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
        return t;
    }

    private void ConfigureTick(DepthTickView tick, float depth, float currentDepth)
    {
        bool isMajor = IsMajorTick(depth);
        tick.SetMajor(isMajor);
        if (isMajor) tick.SetLabel(depth);

        var rt = tick.transform as RectTransform;
        if (!rt) return;

        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);

        float metersFromMarker = depth - currentDepth;
        if (invertScroll) metersFromMarker = -metersFromMarker;
        float yFromMarker = metersFromMarker * pixelsPerMeter;
        float y = _lastCenterFromTop + yFromMarker;
        rt.anchoredPosition = new Vector2(0f, -y);
    }

    private bool IsMajorTick(float depth)
    {
        float step = Mathf.Max(0.0001f, majorStepMeters);
        float remainder = Mathf.Abs(depth % step);
        return remainder < 0.001f || Mathf.Abs(step - remainder) < 0.001f;
    }

    private void UpdateDebug(float currentDepth)
    {
        if (!debugEnabled || !debugText) return;

        debugText.text =
            $"depth {currentDepth:0.0} | ppm {pixelsPerMeter:0.###} | min {(_lastMinDepth):0.0} max {(_lastMaxDepth):0.0} | center {_lastCenterFromTop:0.0} | invert {invertScroll}";
    }

    private void SetDebugVisible(bool visible)
    {
        if (debugText) debugText.gameObject.SetActive(visible);
    }
}
