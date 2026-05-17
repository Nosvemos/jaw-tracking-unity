using System;
using JawTracking.Data;
using JawTracking.Motion;
using UnityEngine;

namespace JawTracking.Network
{
    public sealed class UdpJawMotionSource : MonoBehaviour
    {
        [SerializeField] private JawModelController modelController;

        [Header("UDP")]
        [SerializeField] private string listenAddress = "0.0.0.0";
        [SerializeField] private int listenPort = 5055;
        [SerializeField] private bool allowCsvFallback = true;
        [SerializeField] private int maxPacketsPerFrame = 96;
        [SerializeField] private float trackingTimeoutSeconds = 1.5f;
        [SerializeField] private float statsUpdateIntervalSeconds = 0.25f;

        [Header("Coordinate Mapping")]
        [SerializeField] private bool useMillimeterPoseIfAvailable = true;
        [SerializeField] private float mmPerPixelX = 0.1f;
        [SerializeField] private float mmPerPixelY = 0.1f;
        [SerializeField] private float mmPerPixelZ = 0.1f;
        [SerializeField] private bool invertX = false;
        [SerializeField] private bool invertY = false;
        [SerializeField] private bool invertZ = false;

        [Header("Motion Mapping")]
        [SerializeField] private float openingAngleScale = 0.82f;
        [SerializeField] private float lateralYawScale = 0.55f;
        [SerializeField] private float maxOpeningMm = 50f;
        [SerializeField] private float maxLateralMm = 25f;
        [SerializeField] private float maxProtrusionMm = 20f;
        [SerializeField] private float maxOpeningAngle = 40f;

        private readonly UdpJawReceiver receiver = new UdpJawReceiver();
        private float lastValidFrameRealtime;
        private float nextStatsUpdateRealtime;
        private string lastParserError = string.Empty;
        private int droppedParseErrors;

        public event Action<JawMotionState> MotionUpdated;
        public event Action<string> StatusChanged;
        public event Action<float, bool> StatsUpdated;

        public bool IsReceiving => receiver.IsRunning;
        public string ListenAddress => listenAddress;
        public int ListenPort => listenPort;
        public float PacketRate => receiver.PacketsPerSecond;
        public bool IsMotionPaused { get; private set; }

        public void ConfigureNetwork(string address, int port)
        {
            if (IsReceiving)
            {
                return;
            }

            listenAddress = string.IsNullOrWhiteSpace(address) ? "0.0.0.0" : address.Trim();
            listenPort = Mathf.Clamp(port, 1, 65535);
        }

        public void SetModelController(JawModelController controller)
        {
            modelController = controller;
        }

        public void SetMotionPaused(bool paused)
        {
            IsMotionPaused = paused;
        }

        public void ToggleReceiving()
        {
            if (IsReceiving)
            {
                StopReceiving();
            }
            else
            {
                StartReceiving();
            }
        }

        public void StartReceiving()
        {
            EnsureModelController();
            try
            {
                receiver.Start(listenAddress, listenPort);
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"UDP başlatılamadı: {ex.Message}");
                StatsUpdated?.Invoke(0f, false);
                return;
            }

            lastValidFrameRealtime = Time.realtimeSinceStartup;
            nextStatsUpdateRealtime = 0f;
            StatusChanged?.Invoke($"UDP dinleniyor: {listenAddress}:{listenPort}");
        }

        public void StopReceiving()
        {
            receiver.Stop();
            modelController?.ForceRestPoseImmediate();
            StatusChanged?.Invoke("UDP durduruldu.");
            StatsUpdated?.Invoke(0f, false);
        }

        private void Update()
        {
            if (!receiver.IsRunning)
            {
                return;
            }

            int maxDrainCount = Mathf.Max(1, maxPacketsPerFrame, receiver.PendingPackets);
            if (receiver.TryDequeueLatest(out string packet, maxDrainCount, out _))
            {
                ProcessPacket(packet);
            }

            bool trackingFresh = Time.realtimeSinceStartup - lastValidFrameRealtime <= trackingTimeoutSeconds;
            if (Time.unscaledTime >= nextStatsUpdateRealtime)
            {
                nextStatsUpdateRealtime = Time.unscaledTime + Mathf.Max(0.05f, statsUpdateIntervalSeconds);
                StatsUpdated?.Invoke(receiver.PacketsPerSecond, trackingFresh);
            }
        }

        private void OnDestroy()
        {
            receiver.Dispose();
        }

        private void ProcessPacket(string packet)
        {
            if (!JawDataParser.TryParse(packet, allowCsvFallback, out JawFrame frame, out string error))
            {
                ReportParseError(error);
                return;
            }

            JawMotionState state = MapFrame(frame);
            if (state.TrackingValid)
            {
                lastValidFrameRealtime = Time.realtimeSinceStartup;
                if (IsMotionPaused)
                {
                    return;
                }

                modelController?.ApplyMotion(state);
            }

            if (IsMotionPaused)
            {
                return;
            }

            MotionUpdated?.Invoke(state);
        }

        private JawMotionState MapFrame(JawFrame frame)
        {
            float xMm = 0f;
            float yMm = 0f;
            float zMm = 0f;

            if (useMillimeterPoseIfAvailable && frame.HasPose())
            {
                xMm = frame.pose.x_mm;
                yMm = frame.pose.y_mm;
                zMm = frame.pose.z_mm;
            }
            else if (frame.HasRelative())
            {
                xMm = frame.relative.dx_px * mmPerPixelX;
                yMm = frame.relative.dy_px * mmPerPixelY;
                zMm = frame.relative.dz_px * mmPerPixelZ;
            }

            if (invertX)
            {
                xMm *= -1f;
            }

            if (invertY)
            {
                yMm *= -1f;
            }

            if (invertZ)
            {
                zMm *= -1f;
            }

            float openingMm = Mathf.Clamp(Mathf.Max(0f, yMm), 0f, maxOpeningMm);
            float lateralMm = Mathf.Clamp(xMm, -maxLateralMm, maxLateralMm);
            float protrusionMm = Mathf.Clamp(zMm, -maxProtrusionMm, maxProtrusionMm);
            float openingAngleDeg = Mathf.Clamp(openingMm * openingAngleScale, 0f, maxOpeningAngle);
            float lateralYawDeg = Mathf.Clamp(lateralMm * lateralYawScale, -12f, 12f);
            float confidence = frame.quality != null ? frame.quality.confidence : 1f;

            return new JawMotionState(
                openingMm,
                lateralMm,
                protrusionMm,
                openingAngleDeg,
                lateralYawDeg,
                confidence,
                frame.tracking_valid,
                frame.frame_id,
                frame.timestamp_ms);
        }

        private void ReportParseError(string error)
        {
            if (error == lastParserError)
            {
                droppedParseErrors++;
                return;
            }

            lastParserError = error;
            droppedParseErrors = 0;
            StatusChanged?.Invoke(error);
        }

        private void EnsureModelController()
        {
            if (modelController != null)
            {
                return;
            }

            modelController = FindFirstObjectByType<JawModelController>();
        }
    }
}
