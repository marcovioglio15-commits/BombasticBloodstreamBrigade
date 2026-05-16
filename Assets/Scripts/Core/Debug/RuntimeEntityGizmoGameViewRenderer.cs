using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Renders runtime ECS gizmos directly inside the Game view camera output.
/// none.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class RuntimeEntityGizmoGameViewRenderer : MonoBehaviour
{
    #region Types
    /// <summary>
     /// Handles the Game view backend used by the shared runtime gizmo rendering utility.
     /// none.
     /// </summary>
    private sealed class GameViewPrimitiveDrawer : IRuntimeGizmoPrimitiveDrawer
    {
        #region Constants
        private const int CircleSegmentCount = 40;
        private const float MinimumMarkerSize = 7f;
        private const float MaximumMarkerSize = 14f;
        private const float ArrowHeadLength = 9f;
        private const float ArrowHeadAngleDegrees = 24f;
        #endregion

        #region Fields
        private Camera targetCamera;
        #endregion

        #region Constructor
        public GameViewPrimitiveDrawer()
        {
            targetCamera = null;
        }
        #endregion

        #region Methods

        #region Public Methods
        /// <summary>
        /// Initializes one new immediate-mode draw pass for the supplied game camera.
        /// </summary>
        /// <param name="camera">Camera that owns the current Game view render pass.</param>
        /// <param name="lineMaterial">Material used by the GL line pass.</param>
        public void Begin(Camera camera, Material lineMaterial)
        {
            targetCamera = camera;
            GL.PushMatrix();
            GL.LoadPixelMatrix(0f, targetCamera.pixelWidth, 0f, targetCamera.pixelHeight);
            lineMaterial.SetPass(0);
            GL.Begin(GL.LINES);
        }

        /// <summary>
        /// Completes the current immediate-mode draw pass.
        /// none.
        /// </summary>
        public void End()
        {
            GL.End();
            GL.PopMatrix();
        }

        /// <summary>
        /// Draws one projected wire disc by tessellating the gameplay circle into screen-space line segments.
        /// </summary>
        /// <param name="center">World-space center of the disc.</param>
        /// <param name="radius">Radius expressed in gameplay world units.</param>
        /// <param name="color">Final GL line color.</param>
        public void DrawWireDisc(Vector3 center, float radius, Color color)
        {
            if (targetCamera == null || radius <= 0f)
                return;

            float angleStep = Mathf.PI * 2f / CircleSegmentCount;
            Vector3 previousWorldPoint = ResolveDiscPoint(center, radius, 0f);

            // Sample the gameplay disc around the XZ plane and project each segment to the current Game view.
            for (int segmentIndex = 1; segmentIndex <= CircleSegmentCount; segmentIndex++)
            {
                float angle = angleStep * segmentIndex;
                Vector3 currentWorldPoint = ResolveDiscPoint(center, radius, angle);

                if (TryProjectPoint(previousWorldPoint, out Vector2 previousScreenPoint) &&
                    TryProjectPoint(currentWorldPoint, out Vector2 currentScreenPoint))
                {
                    DrawScreenLine(previousScreenPoint, currentScreenPoint, color);
                }

                previousWorldPoint = currentWorldPoint;
            }
        }

        /// <summary>
        /// Draws one projected direction vector with a screen-space arrow head.
        /// </summary>
        /// <param name="origin">Vector origin in world space.</param>
        /// <param name="direction">Direction expected to be safely normalizable.</param>
        /// <param name="length">Vector length expressed in gameplay world units.</param>
        /// <param name="color">Final GL line color.</param>
        public void DrawDirection(Vector3 origin, Vector3 direction, float length, Color color)
        {
            if (targetCamera == null || length <= 0f)
                return;

            if (direction.sqrMagnitude <= 0.0001f)
                return;

            Vector3 normalizedDirection = direction.normalized;
            Vector3 end = origin + normalizedDirection * length;

            if (!TryProjectPoint(origin, out Vector2 startScreenPoint) ||
                !TryProjectPoint(end, out Vector2 endScreenPoint))
            {
                return;
            }

            DrawScreenLine(startScreenPoint, endScreenPoint, color);
            DrawArrowHead(endScreenPoint, (endScreenPoint - startScreenPoint).normalized, color);
        }

        /// <summary>
        /// Draws one projected straight link.
        /// </summary>
        /// <param name="start">Link starting point in world space.</param>
        /// <param name="end">Link end point in world space.</param>
        /// <param name="color">Final GL line color.</param>
        public void DrawLink(Vector3 start, Vector3 end, Color color)
        {
            if (targetCamera == null)
                return;

            if (!TryProjectPoint(start, out Vector2 startScreenPoint) ||
                !TryProjectPoint(end, out Vector2 endScreenPoint))
            {
                return;
            }

            DrawScreenLine(startScreenPoint, endScreenPoint, color);
        }

        /// <summary>
        /// Draws one compact projected marker using a small cross.
        /// </summary>
        /// <param name="position">Marker position in world space.</param>
        /// <param name="radius">Marker size hint expressed in gameplay world units.</param>
        /// <param name="color">Final GL line color.</param>
        public void DrawMarker(Vector3 position, float radius, Color color)
        {
            if (targetCamera == null)
                return;

            if (!TryProjectPoint(position, out Vector2 screenPoint))
                return;

            float markerSize = Mathf.Clamp(radius * 40f, MinimumMarkerSize, MaximumMarkerSize);
            Vector2 horizontalOffset = new Vector2(markerSize, 0f);
            Vector2 verticalOffset = new Vector2(0f, markerSize);

            DrawScreenLine(screenPoint - horizontalOffset, screenPoint + horizontalOffset, color);
            DrawScreenLine(screenPoint - verticalOffset, screenPoint + verticalOffset, color);
        }

        /// <summary>
        /// Ignores labels in Game view so the gameplay HUD overlay stays unobstructed.
        /// </summary>
        /// <param name="position">World-space label anchor.</param>
        /// <param name="text">Text shown in the Game view.</param>
        public void DrawLabel(Vector3 position, string text)
        {
        }
        #endregion

        #region Private Methods
        private static Vector3 ResolveDiscPoint(Vector3 center, float radius, float angle)
        {
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            return new Vector3(center.x + x, center.y, center.z + z);
        }

        private void DrawArrowHead(Vector2 tip, Vector2 forward, Color color)
        {
            if (forward.sqrMagnitude <= 0.0001f)
                return;

            Vector2 leftDirection = Rotate(forward, 180f - ArrowHeadAngleDegrees);
            Vector2 rightDirection = Rotate(forward, 180f + ArrowHeadAngleDegrees);

            DrawScreenLine(tip, tip + leftDirection * ArrowHeadLength, color);
            DrawScreenLine(tip, tip + rightDirection * ArrowHeadLength, color);
        }

        private static Vector2 Rotate(Vector2 input, float angleDegrees)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            float x = input.x * cos - input.y * sin;
            float y = input.x * sin + input.y * cos;
            return new Vector2(x, y);
        }

        private bool TryProjectPoint(Vector3 worldPoint, out Vector2 screenPoint)
        {
            screenPoint = default;

            if (targetCamera == null)
                return false;

            Vector3 projectedPoint = targetCamera.WorldToScreenPoint(worldPoint);

            if (projectedPoint.z <= 0f)
                return false;

            Rect cameraRect = targetCamera.pixelRect;
            screenPoint = new Vector2(projectedPoint.x - cameraRect.x, projectedPoint.y - cameraRect.y);
            return true;
        }

        private static void DrawScreenLine(Vector2 start, Vector2 end, Color color)
        {
            GL.Color(color);
            GL.Vertex3(start.x, start.y, 0f);
            GL.Vertex3(end.x, end.y, 0f);
        }
        #endregion

        #endregion
    }
    #endregion

    #region Fields
    private static Material lineMaterial;
    private static bool materialWarningIssued;

    [Tooltip("Camera used to render runtime gizmos inside the Game view. Defaults to the Camera on the same GameObject.")]
    [SerializeField] private Camera targetCamera;
    private GameViewPrimitiveDrawer primitiveDrawer;
    #endregion

    #region Methods

    #region Unity Methods
    private void Awake()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        enabled = false;
        return;
#endif

        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        if (primitiveDrawer == null)
            primitiveDrawer = new GameViewPrimitiveDrawer();
    }

    private void OnEnable()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        return;
#endif

        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        if (primitiveDrawer == null)
            primitiveDrawer = new GameViewPrimitiveDrawer();

        RenderPipelineManager.endCameraRendering += HandleEndCameraRendering;
        Camera.onPostRender += HandleCameraPostRender;
    }

    private void OnDisable()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        return;
#endif

        RenderPipelineManager.endCameraRendering -= HandleEndCameraRendering;
        Camera.onPostRender -= HandleCameraPostRender;
    }
    #endregion

    #region Rendering
    private void HandleEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        RenderForCamera(camera);
    }

    private void HandleCameraPostRender(Camera camera)
    {
        if (GraphicsSettings.currentRenderPipeline != null)
            return;

        RenderForCamera(camera);
    }

    private void RenderForCamera(Camera camera)
    {
        if (!Application.isPlaying)
            return;

        if (!RuntimeEntityGizmoRenderUtility.AnyRuntimeGizmoEnabled)
            return;

        if (!ShouldRenderForCamera(camera))
            return;

        if (!EnsureLineMaterial())
            return;

        primitiveDrawer.Begin(targetCamera, lineMaterial);
        RuntimeEntityGizmoRenderUtility.TryRender(primitiveDrawer);
        primitiveDrawer.End();
    }

    private bool ShouldRenderForCamera(Camera camera)
    {
        if (camera == null || targetCamera == null)
            return false;

        if (!ReferenceEquals(camera, targetCamera))
            return false;

        if (camera.cameraType != CameraType.Game)
            return false;

        if (!camera.isActiveAndEnabled)
            return false;

        if (camera.pixelWidth <= 0 || camera.pixelHeight <= 0)
            return false;

        return true;
    }
    #endregion

    #region Helpers
    private static bool EnsureLineMaterial()
    {
        if (lineMaterial != null)
            return true;

        Shader lineShader = Shader.Find("Hidden/Internal-Colored");

        if (lineShader == null)
        {
            if (!materialWarningIssued)
            {
                Debug.LogWarning("[RuntimeEntityGizmoGameViewRenderer] Hidden/Internal-Colored shader was not found. Game view runtime gizmos will stay disabled.",
                                 null);
                materialWarningIssued = true;
            }

            return false;
        }

        lineMaterial = new Material(lineShader);
        lineMaterial.hideFlags = HideFlags.HideAndDontSave;
        lineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        lineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        lineMaterial.SetInt("_Cull", (int)CullMode.Off);
        lineMaterial.SetInt("_ZWrite", 0);
        return true;
    }
    #endregion

    #endregion
}
