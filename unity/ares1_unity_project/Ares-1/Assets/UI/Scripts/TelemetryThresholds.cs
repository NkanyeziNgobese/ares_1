using System;

[Serializable]
public class ThresholdRule
{
    public string key;
    public bool enabled = true;

    public bool useLow = false;
    public bool useHigh = true;

    public float warnHigh = 0f;
    public float dangerHigh = 0f;

    public float warnLow = 0f;
    public float dangerLow = 0f;

    public TelemetrySeverity Evaluate(float value)
    {
        if (!enabled) return TelemetrySeverity.Safe;

        TelemetrySeverity high = TelemetrySeverity.Safe;
        TelemetrySeverity low = TelemetrySeverity.Safe;

        if (useHigh)
        {
            if (dangerHigh > 0f && value >= dangerHigh)
                high = TelemetrySeverity.Danger;
            else if (warnHigh > 0f && value >= warnHigh)
                high = TelemetrySeverity.Warning;
        }

        if (useLow)
        {
            if (dangerLow != 0f && value <= dangerLow)
                low = TelemetrySeverity.Danger;
            else if (warnLow != 0f && value <= warnLow)
                low = TelemetrySeverity.Warning;
        }

        return Max(high, low);
    }

    private static TelemetrySeverity Max(TelemetrySeverity a, TelemetrySeverity b)
    {
        return Rank(a) >= Rank(b) ? a : b;
    }

    private static int Rank(TelemetrySeverity s)
    {
        return (int)s;
    }
}
