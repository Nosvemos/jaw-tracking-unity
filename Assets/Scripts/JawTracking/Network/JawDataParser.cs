using System;
using System.Globalization;
using JawTracking.Data;
using UnityEngine;

namespace JawTracking.Network
{
    public static class JawDataParser
    {
        public static bool TryParse(string packet, bool allowCsv, out JawFrame frame, out string error)
        {
            frame = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(packet))
            {
                error = "Boş UDP paketi.";
                return false;
            }

            string trimmed = packet.Trim();
            if (trimmed.StartsWith("{", StringComparison.Ordinal))
            {
                return TryParseJson(trimmed, out frame, out error);
            }

            if (allowCsv)
            {
                return TryParseCsv(trimmed, out frame, out error);
            }

            error = "Desteklenmeyen UDP formatı.";
            return false;
        }

        private static bool TryParseJson(string json, out JawFrame frame, out string error)
        {
            frame = null;
            error = string.Empty;

            try
            {
                frame = JsonUtility.FromJson<JawFrame>(json);
            }
            catch (Exception ex)
            {
                error = $"JSON okunamadı: {ex.Message}";
                return false;
            }

            if (frame == null)
            {
                error = "JSON jaw frame üretmedi.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(frame.type) && frame.type != "jaw_frame")
            {
                error = $"Geçersiz paket tipi: {frame.type}";
                return false;
            }

            frame.type = string.IsNullOrWhiteSpace(frame.type) ? "jaw_frame" : frame.type;
            return true;
        }

        private static bool TryParseCsv(string csv, out JawFrame frame, out string error)
        {
            frame = null;
            error = string.Empty;

            string[] values = csv.Split(',');
            if (values.Length < 6)
            {
                error = "CSV paketinde yeterli alan yok.";
                return false;
            }

            try
            {
                frame = new JawFrame
                {
                    frame_id = ParseLong(values, 0),
                    timestamp_ms = ParseLong(values, 1),
                    tracking_valid = ParseInt(values, 2) != 0,
                    relative = new RelativeMarkerData
                    {
                        dx_px = ParseFloat(values, 3),
                        dy_px = ParseFloat(values, 4),
                        dtheta_deg = ParseFloat(values, 5)
                    },
                    pose = values.Length >= 9
                        ? new JawPoseData
                        {
                            x_mm = ParseFloat(values, 6),
                            y_mm = ParseFloat(values, 7),
                            z_mm = ParseFloat(values, 8),
                            yaw_deg = values.Length > 9 ? ParseFloat(values, 9) : 0f,
                            pitch_deg = values.Length > 10 ? ParseFloat(values, 10) : 0f,
                            roll_deg = values.Length > 11 ? ParseFloat(values, 11) : 0f
                        }
                        : null,
                    quality = new QualityData
                    {
                        confidence = values.Length > 12 ? ParseFloat(values, 12) : 1f
                    }
                };
            }
            catch (Exception ex)
            {
                error = $"CSV okunamadı: {ex.Message}";
                return false;
            }

            return true;
        }

        private static float ParseFloat(string[] values, int index)
        {
            return float.Parse(values[index], CultureInfo.InvariantCulture);
        }

        private static int ParseInt(string[] values, int index)
        {
            return int.Parse(values[index], CultureInfo.InvariantCulture);
        }

        private static long ParseLong(string[] values, int index)
        {
            return long.Parse(values[index], CultureInfo.InvariantCulture);
        }
    }
}
