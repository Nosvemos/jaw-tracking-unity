using System;
using JawTracking.Data;
using JawTracking.Motion;
using UnityEngine;

namespace JawTracking.Simulation
{
    public sealed class JawDataSimulator : MonoBehaviour
    {
        [SerializeField] private JawModelController modelController;

        [Header("Simulation Shape")]
        [SerializeField] private float cycleSeconds = 3.2f;
        [SerializeField] private float maxOpeningMm = 32f;
        [SerializeField] private float maxLateralMm = 6f;
        [SerializeField] private float maxProtrusionMm = 4f;
        [SerializeField] private float openingAngleScale = 0.82f;
        [SerializeField] private float lateralYawScale = 0.55f;

        [Header("Noise")]
        [SerializeField] private bool addSmallNoise = true;
        [SerializeField] private float noiseMm = 0.25f;

        private float elapsedSeconds;
        private long frameId;

        public event Action<JawMotionState> MotionUpdated;

        public bool IsRunning { get; private set; }
        public bool IsMotionPaused { get; private set; }

        public float OpeningAngleScale => openingAngleScale;

        public void SetModelController(JawModelController controller)
        {
            modelController = controller;
        }

        public void SetOpeningAngleScale(float value)
        {
            openingAngleScale = Mathf.Clamp(value, 0.05f, 2f);
        }

        public void SetMotionPaused(bool paused)
        {
            IsMotionPaused = paused;
        }

        public void Toggle()
        {
            if (IsRunning)
            {
                Stop();
            }
            else
            {
                StartSimulation();
            }
        }

        public void StartSimulation()
        {
            EnsureModelController();
            modelController?.RecalculatePivotFromRestPose();
            modelController?.ForceRestPoseImmediate();
            elapsedSeconds = 0f;
            frameId = 0;
            IsRunning = true;
            IsMotionPaused = false;
        }

        public void Stop()
        {
            StopAndReturnToRest();
        }

        public void StopAndReturnToRest()
        {
            IsRunning = false;
            IsMotionPaused = false;
            elapsedSeconds = 0f;
            var restState = new JawMotionState(0f, 0f, 0f, 0f, 0f, 1f, true, frameId, CurrentTimestampMs());
            modelController?.ForceRestPoseImmediate();
            MotionUpdated?.Invoke(restState);
        }

        public void RefreshRestPose()
        {
            EnsureModelController();
            modelController?.RecalculatePivotFromRestPose();
            modelController?.ForceRestPoseImmediate();
        }

        private void Update()
        {
            if (!IsRunning)
            {
                return;
            }

            if (IsMotionPaused)
            {
                return;
            }

            EnsureModelController();
            elapsedSeconds += Time.deltaTime;
            frameId++;

            JawMotionState state = BuildCombinedMotion();
            modelController?.ApplyMotion(state);
            MotionUpdated?.Invoke(state);
        }

        private JawMotionState BuildCombinedMotion()
        {
            float phase = Mathf.Max(0.01f, elapsedSeconds / cycleSeconds) * Mathf.PI * 2f;
            float opening01 = Mathf.Clamp01(Mathf.Sin(phase) * 0.5f + 0.5f);
            float openingMm = opening01 * maxOpeningMm;
            float lateralMm = Mathf.Sin(phase * 0.82f + 0.55f) * maxLateralMm * opening01;
            float protrusionMm = Mathf.Sin(phase * 0.5f - 0.35f) * maxProtrusionMm * opening01;

            if (addSmallNoise)
            {
                openingMm += UnityEngine.Random.Range(-noiseMm, noiseMm);
                lateralMm += UnityEngine.Random.Range(-noiseMm, noiseMm);
                protrusionMm += UnityEngine.Random.Range(-noiseMm, noiseMm);
            }

            openingMm = Mathf.Max(0f, openingMm);
            float openingAngleDeg = Mathf.Clamp(openingMm * openingAngleScale, 0f, 40f);
            float lateralYawDeg = Mathf.Clamp(lateralMm * lateralYawScale, -12f, 12f);

            return new JawMotionState(
                openingMm,
                lateralMm,
                protrusionMm,
                openingAngleDeg,
                lateralYawDeg,
                0.96f,
                true,
                frameId,
                CurrentTimestampMs());
        }

        private void EnsureModelController()
        {
            if (modelController != null)
            {
                return;
            }

            modelController = FindFirstObjectByType<JawModelController>();
            if (modelController == null)
            {
                modelController = gameObject.AddComponent<JawModelController>();
            }
        }

        private static long CurrentTimestampMs()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
