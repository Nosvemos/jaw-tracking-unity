using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Serialization;

namespace JawTracking.Visualization
{
    public sealed class JawOrbitCameraController : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 targetOffset = Vector3.zero;

        [Header("Orbit")]
        [SerializeField] private float yaw = 0f;
        [SerializeField] private float pitch = 12f;
        [SerializeField] private float minPitch = -35f;
        [SerializeField] private float maxPitch = 65f;
        [FormerlySerializedAs("orbitSensitivity")]
        [SerializeField] private float mouseOrbitSensitivity = 0.18f;
        [SerializeField] private float touchOrbitSensitivity = 0.08f;

        [Header("Zoom")]
        [SerializeField] private float distance = 0.42f;
        [SerializeField] private float minDistance = 0.025f;
        [SerializeField] private float maxDistance = 2.5f;
        [FormerlySerializedAs("zoomSensitivity")]
        [SerializeField] private float mouseZoomSensitivity = 1.15f;
        [SerializeField] private float touchZoomSensitivity = 0.008f;
        [SerializeField] private float zoomStepMultiplier = 0.72f;

        [Header("Pan")]
        [FormerlySerializedAs("panSensitivity")]
        [SerializeField] private float mousePanSensitivity = 0.0008f;
        [SerializeField] private float touchPanSensitivity = 0.00045f;

        [Header("Clipping")]
        [SerializeField] private float nearClipAtMinDistance = 0.001f;
        [SerializeField] private float nearClipDistanceFactor = 0.02f;
        [SerializeField] private float farClipDistanceFactor = 24f;
        [SerializeField] private float minimumFarClipPlane = 10f;

        private Vector2 previousPrimaryTouch;
        private Vector2 previousSecondaryTouch;
        private bool hadTwoTouches;
        private Camera controlledCamera;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public void FrameBounds(Bounds bounds)
        {
            targetOffset = bounds.center;
            float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxSize > Mathf.Epsilon)
            {
                distance = Mathf.Clamp(maxSize * 2.4f, minDistance, maxDistance);
            }
        }

        private void LateUpdate()
        {
            if (controlledCamera == null)
            {
                controlledCamera = GetComponent<Camera>();
            }

            HandleMouseInput();
            HandleTouchInput();
            ApplyCameraTransform();
        }

        private void HandleMouseInput()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            Vector2 delta = mouse.delta.ReadValue();

            if (mouse.rightButton.isPressed)
            {
                yaw += delta.x * mouseOrbitSensitivity;
                pitch -= delta.y * mouseOrbitSensitivity;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            }

            if (mouse.middleButton.isPressed)
            {
                Vector3 pan = (-transform.right * delta.x - transform.up * delta.y) * mousePanSensitivity * distance;
                targetOffset += pan;
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > Mathf.Epsilon)
            {
                float wheelSteps = scroll / 120f;
                float multiplier = Mathf.Pow(zoomStepMultiplier, wheelSteps * mouseZoomSensitivity);
                distance = Mathf.Clamp(distance * multiplier, minDistance, maxDistance);
            }
        }

        private void HandleTouchInput()
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null)
            {
                hadTwoTouches = false;
                return;
            }

            TouchControl firstTouch = GetPressedTouch(touchscreen, 0);
            TouchControl secondTouch = GetPressedTouch(touchscreen, 1);

            if (firstTouch != null && secondTouch == null)
            {
                Vector2 delta = firstTouch.delta.ReadValue();
                yaw += delta.x * touchOrbitSensitivity;
                pitch -= delta.y * touchOrbitSensitivity;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
                hadTwoTouches = false;
                return;
            }

            if (firstTouch != null && secondTouch != null)
            {
                Vector2 firstPosition = firstTouch.position.ReadValue();
                Vector2 secondPosition = secondTouch.position.ReadValue();

                if (hadTwoTouches)
                {
                    float previousDistance = Vector2.Distance(previousPrimaryTouch, previousSecondaryTouch);
                    float currentDistance = Vector2.Distance(firstPosition, secondPosition);
                    float pinchDelta = currentDistance - previousDistance;
                    distance = Mathf.Clamp(distance - pinchDelta * touchZoomSensitivity, minDistance, maxDistance);

                    Vector2 previousCenter = (previousPrimaryTouch + previousSecondaryTouch) * 0.5f;
                    Vector2 currentCenter = (firstPosition + secondPosition) * 0.5f;
                    Vector2 panDelta = currentCenter - previousCenter;
                    targetOffset += (-transform.right * panDelta.x - transform.up * panDelta.y) * touchPanSensitivity * distance;
                }

                previousPrimaryTouch = firstPosition;
                previousSecondaryTouch = secondPosition;
                hadTwoTouches = true;
                return;
            }

            hadTwoTouches = false;
        }

        private static TouchControl GetPressedTouch(Touchscreen touchscreen, int pressedIndex)
        {
            int currentPressedIndex = 0;
            foreach (TouchControl touch in touchscreen.touches)
            {
                if (!touch.press.isPressed)
                {
                    continue;
                }

                if (currentPressedIndex == pressedIndex)
                {
                    return touch;
                }

                currentPressedIndex++;
            }

            return null;
        }

        private void ApplyCameraTransform()
        {
            Vector3 center = target != null ? target.position + targetOffset : targetOffset;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = center - rotation * Vector3.forward * distance;
            transform.rotation = rotation;

            if (controlledCamera != null)
            {
                controlledCamera.nearClipPlane = Mathf.Clamp(distance * nearClipDistanceFactor, nearClipAtMinDistance, 0.05f);
                controlledCamera.farClipPlane = Mathf.Max(minimumFarClipPlane, distance * farClipDistanceFactor);
            }
        }
    }
}
