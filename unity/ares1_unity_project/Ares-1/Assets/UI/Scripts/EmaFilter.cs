using UnityEngine;

public struct EmaFilter
{
    public bool Initialized { get; private set; }
    public float Value { get; private set; }

    public void Reset(float v)
    {
        Value = v;
        Initialized = true;
    }

    public float Update(float sample, float alpha)
    {
        if (!Initialized)
        {
            Value = sample;
            Initialized = true;
            return Value;
        }

        Value = Mathf.Lerp(Value, sample, alpha);
        return Value;
    }
}
