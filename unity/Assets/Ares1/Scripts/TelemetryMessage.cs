using System;

namespace Ares1
{
    [Serializable]
    public class TelemetryMessage
    {
        public string timestamp;
        public float value;
        public string unit;
        public string source;
    }
}
