using JawTracking.Data;
using UnityEngine;

namespace JawTracking.Motion
{
    public sealed class JawModelController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Transform lowerJawPivot;
        [SerializeField] private Transform lowerJawRoot;
        [SerializeField] private Renderer lowerJawRenderer;

        [Header("Pivot Tuning")]
        [SerializeField] private bool autoEstimatePivotFromMesh = true;
        [SerializeField] private Vector3 pivotBoundsAnchor = new Vector3(0f, 0.72f, -0.58f);
        [SerializeField] private Vector3 manualPivotWorldOffset = Vector3.zero;
        [SerializeField] private float minimumPivotLift = 0.012f;

        [Header("Motion Mapping")]
        [SerializeField] private Vector3 openingAxis = Vector3.right;
        [SerializeField] private Vector3 lateralYawAxis = Vector3.up;
        [SerializeField] private Vector3 lateralOffsetAxis = Vector3.right;
        [SerializeField] private Vector3 protrusionAxis = Vector3.forward;
        [SerializeField] private float millimetersToUnityUnits = 0.001f;
        [SerializeField] private float lateralTranslationWeight = 1f;
        [SerializeField] private float protrusionTranslationWeight = 1f;

        [Header("Smoothing")]
        [SerializeField] private bool enableSmoothing = true;
        [SerializeField] private float positionLerpSpeed = 14f;
        [SerializeField] private float rotationLerpSpeed = 14f;

        private Vector3 restPivotLocalPosition;
        private Quaternion restPivotLocalRotation;
        private bool hasRestPose;
        private float openingDirection = 1f;

        public Vector3 PivotBoundsAnchor => pivotBoundsAnchor;
        public Vector3 ManualPivotWorldOffset => manualPivotWorldOffset;
        public float OpeningDirection => openingDirection;

        public void ResetRestPose()
        {
            RecalculatePivotFromRestPose();
        }

        public void RecalculatePivotFromRestPose()
        {
            AutoWireReferences();
            if (lowerJawPivot == null)
            {
                return;
            }

            ReturnToRestImmediately();

            if (autoEstimatePivotFromMesh)
            {
                RepositionPivotFromMeshBounds();
            }

            restPivotLocalPosition = lowerJawPivot.localPosition;
            restPivotLocalRotation = lowerJawPivot.localRotation;
            hasRestPose = true;
        }

        public void ForceRestPoseImmediate()
        {
            AutoWireReferences();
            if (lowerJawPivot == null)
            {
                return;
            }

            if (!hasRestPose)
            {
                RecalculatePivotFromRestPose();
                return;
            }

            ReturnToRestImmediately();
        }

        public void SetPivotBoundsAnchor(Vector3 anchor)
        {
            pivotBoundsAnchor = anchor;
            RecalculatePivotFromRestPose();
        }

        public void SetManualPivotWorldOffset(Vector3 offset)
        {
            manualPivotWorldOffset = offset;
            RecalculatePivotFromRestPose();
        }

        public void ToggleOpeningDirection()
        {
            openingDirection *= -1f;
            ForceRestPoseImmediate();
        }

        public void RepositionPivotFromMeshBounds()
        {
            AutoWireReferences();
            if (lowerJawPivot == null || lowerJawRoot == null || lowerJawRenderer == null)
            {
                return;
            }

            Bounds bounds = lowerJawRenderer.bounds;
            if (bounds.size.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            Vector3 desiredPivotWorldPosition = bounds.center + Vector3.Scale(bounds.extents, pivotBoundsAnchor);
            desiredPivotWorldPosition.y = Mathf.Max(
                desiredPivotWorldPosition.y,
                bounds.max.y + minimumPivotLift);
            desiredPivotWorldPosition += manualPivotWorldOffset;

            Vector3 rootWorldPosition = lowerJawRoot.position;
            Quaternion rootWorldRotation = lowerJawRoot.rotation;
            Vector3 rootLocalScale = lowerJawRoot.localScale;
            Quaternion pivotWorldRotation = lowerJawPivot.rotation;

            lowerJawPivot.position = desiredPivotWorldPosition;
            lowerJawPivot.rotation = pivotWorldRotation;

            lowerJawRoot.position = rootWorldPosition;
            lowerJawRoot.rotation = rootWorldRotation;
            lowerJawRoot.localScale = rootLocalScale;
        }

        public void ApplyMotion(JawMotionState state)
        {
            AutoWireReferences();
            if (lowerJawPivot == null)
            {
                return;
            }

            if (!hasRestPose)
            {
                ResetRestPose();
            }

            Vector3 lateralOffset = SafeNormalized(lateralOffsetAxis) *
                (state.LateralMm * millimetersToUnityUnits * lateralTranslationWeight);
            Vector3 protrusionOffset = SafeNormalized(protrusionAxis) *
                (state.ProtrusionMm * millimetersToUnityUnits * protrusionTranslationWeight);

            Vector3 targetPosition = restPivotLocalPosition + lateralOffset + protrusionOffset;
            Quaternion targetRotation =
                restPivotLocalRotation *
                Quaternion.AngleAxis(state.OpeningAngleDeg * openingDirection, SafeNormalized(openingAxis)) *
                Quaternion.AngleAxis(state.LateralYawDeg, SafeNormalized(lateralYawAxis));

            if (!enableSmoothing)
            {
                lowerJawPivot.localPosition = targetPosition;
                lowerJawPivot.localRotation = targetRotation;
                return;
            }

            float positionT = 1f - Mathf.Exp(-positionLerpSpeed * Time.deltaTime);
            float rotationT = 1f - Mathf.Exp(-rotationLerpSpeed * Time.deltaTime);
            lowerJawPivot.localPosition = Vector3.Lerp(lowerJawPivot.localPosition, targetPosition, positionT);
            lowerJawPivot.localRotation = Quaternion.Slerp(lowerJawPivot.localRotation, targetRotation, rotationT);
        }

        private void Awake()
        {
            ResetRestPose();
        }

        private void ReturnToRestImmediately()
        {
            if (!hasRestPose || lowerJawPivot == null)
            {
                return;
            }

            lowerJawPivot.localPosition = restPivotLocalPosition;
            lowerJawPivot.localRotation = restPivotLocalRotation;
        }

        private void AutoWireReferences()
        {
            if (lowerJawPivot == null)
            {
                lowerJawPivot = FindSceneTransformByName("LowerJawPivot");
            }

            if (lowerJawRoot == null)
            {
                lowerJawRoot = FindSceneTransformByName("LowerJawRoot");
            }

            if (lowerJawRenderer == null && lowerJawRoot != null)
            {
                lowerJawRenderer = lowerJawRoot.GetComponentInChildren<Renderer>();
            }
        }

        private static Vector3 SafeNormalized(Vector3 axis)
        {
            return axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.right;
        }

        private static Transform FindSceneTransformByName(string objectName)
        {
            Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Transform sceneTransform in transforms)
            {
                if (sceneTransform.name == objectName && sceneTransform.gameObject.scene.IsValid())
                {
                    return sceneTransform;
                }
            }

            return null;
        }
    }
}
