using System.Collections;
using Engine;
using Map.CameraControllers;
using Map.Rendering;
using Map.Rendering.Border;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Aoerlunduo
{
    public sealed class AoerlunduoCameraControls : MonoBehaviour
    {
        [SerializeField] private float zoomSensitivity = 0.22f;
        [SerializeField] private float keyboardSpeed = 0.75f;
        [SerializeField] private float smoothing = 12f;

        private Camera mapCamera;
        private PerspectiveCameraController engineController;
        private BorderComputeDispatcher borderDispatcher;
        private Material mapMaterial;
        private Bounds mapBounds;
        private Vector3 targetPosition;
        private float targetSize;
        private float minSize;
        private float maxSize;
        private bool initialized;
        private bool dragging;
        private Vector3 lastPointerWorld;
        private float lastPinchDistance;
        private bool showingProvinceBorders;

        public bool IsInitialized => initialized;
        public float Zoom => mapCamera != null ? mapCamera.orthographicSize : 0f;
        public Vector3 Position => mapCamera != null ? mapCamera.transform.position : Vector3.zero;
        public bool ShowingProvinceBorders => showingProvinceBorders;

        private IEnumerator Start()
        {
            yield return new WaitUntil(() => ArchonEngine.Instance != null && ArchonEngine.Instance.IsInitialized);
            yield return null;

            engineController = FindFirstObjectByType<PerspectiveCameraController>();
            borderDispatcher = FindFirstObjectByType<BorderComputeDispatcher>();
            mapMaterial = ArchonEngine.Instance.MapMaterial;
            mapCamera = engineController != null && engineController.mapCamera != null
                ? engineController.mapCamera
                : Camera.main;
            if (mapCamera == null) yield break;

            GameObject mapPlane = engineController != null ? engineController.mapPlane : null;
            Renderer mapRenderer = mapPlane != null ? mapPlane.GetComponent<Renderer>() : null;
            if (mapRenderer != null)
            {
                mapBounds = mapRenderer.bounds;
            }
            else if (mapPlane != null)
            {
                Vector3 scale = mapPlane.transform.localScale;
                mapBounds = new Bounds(mapPlane.transform.position, new Vector3(scale.x * 10f, 0f, scale.z * 10f));
            }
            else
            {
                mapBounds = new Bounds(Vector3.zero, new Vector3(300f, 0f, 200f));
            }

            if (engineController != null)
            {
                engineController.enableHorizontalWrapping = false;
                engineController.enabled = false;
            }

            float fitByHeight = mapBounds.size.z * 0.52f;
            float fitByWidth = mapBounds.size.x / Mathf.Max(0.1f, 2f * mapCamera.aspect) * 1.02f;
            maxSize = Mathf.Max(fitByHeight, fitByWidth);
            minSize = Mathf.Max(8f, maxSize * 0.16f);
            targetSize = maxSize;
            targetPosition = new Vector3(mapBounds.center.x, Mathf.Max(80f, mapCamera.transform.position.y), mapBounds.center.z);

            mapCamera.orthographic = true;
            mapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            mapCamera.transform.position = targetPosition;
            mapCamera.orthographicSize = targetSize;
            mapCamera.backgroundColor = new Color(0.031f, 0.12f, 0.34f, 1f);
            ClampTarget();
            UpdateMapDetailLevel(true);
            initialized = true;
        }

        private void Update()
        {
            if (!initialized) return;

            HandleKeyboard();
            if (Input.touchCount > 0) HandleTouch();
            else HandleMouse();

            if (Input.GetKeyDown(KeyCode.Home) || Input.GetKeyDown(KeyCode.F)) ResetView();

            ClampTarget();
            float blend = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            mapCamera.transform.position = Vector3.Lerp(mapCamera.transform.position, targetPosition, blend);
            mapCamera.orthographicSize = Mathf.Lerp(mapCamera.orthographicSize, targetSize, blend);
            UpdateMapDetailLevel(false);
        }

        private void HandleKeyboard()
        {
            Vector3 direction = Vector3.zero;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) direction.x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) direction.x += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) direction.z -= 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) direction.z += 1f;
            if (direction.sqrMagnitude > 0f)
                targetPosition += direction.normalized * keyboardSpeed * targetSize * Time.unscaledDeltaTime;
        }

        private void HandleMouse()
        {
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            float wheel = Input.GetAxisRaw("Mouse ScrollWheel");
            if (!overUi && Mathf.Abs(wheel) > 0.0001f)
                ZoomAtScreenPoint(Mathf.Exp(-wheel * zoomSensitivity * 5f), Input.mousePosition);

            bool held = Input.GetMouseButton(0) || Input.GetMouseButton(2);
            if (!dragging && held && !overUi && TryGroundPoint(Input.mousePosition, out lastPointerWorld))
                dragging = true;
            else if (dragging && !held)
                dragging = false;

            if (dragging && TryGroundPoint(Input.mousePosition, out Vector3 current))
            {
                Vector3 delta = lastPointerWorld - current;
                targetPosition += new Vector3(delta.x, 0f, delta.z);
                mapCamera.transform.position += new Vector3(delta.x, 0f, delta.z);
                TryGroundPoint(Input.mousePosition, out lastPointerWorld);
            }
        }

        private void HandleTouch()
        {
            if (Input.touchCount == 1)
            {
                Touch touch = Input.GetTouch(0);
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId)) return;
                if (touch.phase == TouchPhase.Began && TryGroundPoint(touch.position, out lastPointerWorld)) dragging = true;
                if (dragging && touch.phase == TouchPhase.Moved && TryGroundPoint(touch.position, out Vector3 current))
                {
                    Vector3 delta = lastPointerWorld - current;
                    targetPosition += new Vector3(delta.x, 0f, delta.z);
                    mapCamera.transform.position += new Vector3(delta.x, 0f, delta.z);
                    TryGroundPoint(touch.position, out lastPointerWorld);
                }
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) dragging = false;
            }
            else if (Input.touchCount >= 2)
            {
                dragging = false;
                Touch first = Input.GetTouch(0);
                Touch second = Input.GetTouch(1);
                float distance = Vector2.Distance(first.position, second.position);
                if (first.phase != TouchPhase.Began && second.phase != TouchPhase.Began && lastPinchDistance > 0f)
                {
                    float factor = Mathf.Clamp(lastPinchDistance / Mathf.Max(distance, 1f), 0.75f, 1.25f);
                    ZoomAtScreenPoint(factor, (first.position + second.position) * 0.5f);
                }
                lastPinchDistance = distance;
            }
            else
            {
                lastPinchDistance = 0f;
            }
        }

        private void ZoomAtScreenPoint(float factor, Vector2 screenPoint)
        {
            if (!TryGroundPoint(screenPoint, out Vector3 before)) return;
            targetSize = Mathf.Clamp(targetSize * factor, minSize, maxSize);
            mapCamera.orthographicSize = targetSize;
            if (TryGroundPoint(screenPoint, out Vector3 after))
                targetPosition += new Vector3(before.x - after.x, 0f, before.z - after.z);
        }

        private bool TryGroundPoint(Vector2 screenPoint, out Vector3 point)
        {
            Ray ray = mapCamera.ScreenPointToRay(screenPoint);
            Plane plane = new Plane(Vector3.up, new Vector3(0f, mapBounds.center.y, 0f));
            if (plane.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }
            point = default;
            return false;
        }

        private void ClampTarget()
        {
            float halfHeight = targetSize;
            float halfWidth = targetSize * mapCamera.aspect;
            float minX = mapBounds.min.x + halfWidth;
            float maxX = mapBounds.max.x - halfWidth;
            float minZ = mapBounds.min.z + halfHeight;
            float maxZ = mapBounds.max.z - halfHeight;
            targetPosition.x = minX > maxX ? mapBounds.center.x : Mathf.Clamp(targetPosition.x, minX, maxX);
            targetPosition.z = minZ > maxZ ? mapBounds.center.z : Mathf.Clamp(targetPosition.z, minZ, maxZ);
        }

        private void UpdateMapDetailLevel(bool force)
        {
            bool showProvince = mapCamera.orthographicSize <= maxSize * 0.55f;
            if (!force && showProvince == showingProvinceBorders) return;
            showingProvinceBorders = showProvince;

            if (borderDispatcher != null)
            {
                borderDispatcher.SetPixelPerfectParameters(0, 0, 0f);
                borderDispatcher.SetBorderMode(showProvince ? BorderMode.Dual : BorderMode.Country);
            }

            if (mapMaterial != null)
            {
                mapMaterial.SetColor("_CountryBorderColor", new Color(0.025f, 0.03f, 0.04f, 1f));
                mapMaterial.SetColor("_ProvinceBorderColor", new Color(0.025f, 0.035f, 0.055f, 1f));
                mapMaterial.SetFloat("_CountryBorderStrength", 0.92f);
                mapMaterial.SetFloat("_ProvinceBorderStrength", showProvince ? 0.42f : 0f);
            }
        }

        public void ResetView()
        {
            targetSize = maxSize;
            targetPosition = new Vector3(mapBounds.center.x, targetPosition.y, mapBounds.center.z);
        }

        public void PanByWorld(Vector2 delta)
        {
            targetPosition += new Vector3(delta.x, 0f, delta.y);
            ClampTarget();
            mapCamera.transform.position = targetPosition;
        }

        public void ZoomBySteps(float steps)
        {
            targetSize = Mathf.Clamp(targetSize * Mathf.Exp(-steps * zoomSensitivity), minSize, maxSize);
            mapCamera.orthographicSize = targetSize;
            ClampTarget();
        }
    }
}
