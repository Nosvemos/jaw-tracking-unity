using System;

namespace JawTracking.Data
{
    [Serializable]
    public sealed class JawFrame
    {
        public string type = "jaw_frame";
        public long frame_id;
        public long timestamp_ms;
        public bool tracking_valid = true;
        public MarkerData reference_marker;
        public MarkerData jaw_marker;
        public RelativeMarkerData relative;
        public JawPoseData pose;
        public QualityData quality;

        public bool HasPose()
        {
            return pose != null;
        }

        public bool HasRelative()
        {
            return relative != null;
        }
    }

    [Serializable]
    public sealed class MarkerData
    {
        public int id;
        public float x_px;
        public float y_px;
        public float angle_deg;
        public float confidence;
    }

    [Serializable]
    public sealed class RelativeMarkerData
    {
        public float dx_px;
        public float dy_px;
        public float dz_px;
        public float dtheta_deg;
    }

    [Serializable]
    public sealed class JawPoseData
    {
        public float x_mm;
        public float y_mm;
        public float z_mm;
        public float yaw_deg;
        public float pitch_deg;
        public float roll_deg;
    }

    [Serializable]
    public sealed class QualityData
    {
        public float latency_ms;
        public float fps;
        public float confidence = 1f;
    }
}
