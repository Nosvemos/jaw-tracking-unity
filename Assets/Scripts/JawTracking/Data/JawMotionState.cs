using UnityEngine;

namespace JawTracking.Data
{
    public readonly struct JawMotionState
    {
        public JawMotionState(
            float openingMm,
            float lateralMm,
            float protrusionMm,
            float openingAngleDeg,
            float lateralYawDeg,
            float confidence,
            bool trackingValid,
            long frameId,
            long timestampMs)
        {
            OpeningMm = openingMm;
            LateralMm = lateralMm;
            ProtrusionMm = protrusionMm;
            OpeningAngleDeg = openingAngleDeg;
            LateralYawDeg = lateralYawDeg;
            Confidence = Mathf.Clamp01(confidence);
            TrackingValid = trackingValid;
            FrameId = frameId;
            TimestampMs = timestampMs;
        }

        public float OpeningMm { get; }
        public float LateralMm { get; }
        public float ProtrusionMm { get; }
        public float OpeningAngleDeg { get; }
        public float LateralYawDeg { get; }
        public float Confidence { get; }
        public bool TrackingValid { get; }
        public long FrameId { get; }
        public long TimestampMs { get; }
    }
}
