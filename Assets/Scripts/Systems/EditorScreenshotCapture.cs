#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

/// <summary>
/// Editor-only F12 capture support. Renders the game camera at the native pixel-art resolution
/// and excludes screen UI without changing the visible Game View.
/// </summary>
public static class EditorScreenshotCapture
{
    public const int CaptureWidth = 480;
    public const int CaptureHeight = 270;
    private const float HubHorizontalMargin = 1f;
    private static EditorScreenshotHotkey s_hotkey;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallHotkey()
    {
        if (s_hotkey != null)
            return;

        var listenerObject = new GameObject("Editor Screenshot Hotkey")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        UnityEngine.Object.DontDestroyOnLoad(listenerObject);
        s_hotkey = listenerObject.AddComponent<EditorScreenshotHotkey>();
    }

    private readonly struct DocumentState
    {
        public readonly VisualElement Root;
        public readonly StyleEnum<DisplayStyle> Display;

        public DocumentState(VisualElement root)
        {
            Root = root;
            Display = root.style.display;
        }
    }

    private readonly struct CanvasState
    {
        public readonly Canvas Canvas;
        public readonly bool Enabled;

        public CanvasState(Canvas canvas)
        {
            Canvas = canvas;
            Enabled = canvas.enabled;
        }
    }

    public static void CapturePixelPerfectWorld()
    {
        Camera camera = ResolveGameCamera();
        if (camera == null)
        {
            Debug.LogWarning("[Screenshot] Cannot capture: no active game camera was found.");
            return;
        }

        CaptureCamera(camera, CaptureWidth, CaptureHeight, "RelicKeeper", "480x270");
    }

    public static void CaptureHubPanorama()
    {
        Camera camera = ResolveGameCamera();
        if (camera == null)
        {
            Debug.LogWarning("[Screenshot] Cannot capture Hub panorama: no active game camera was found.");
            return;
        }

        if (!TryGetHubHorizontalBounds(out float left, out float right))
        {
            Debug.LogWarning(
                "[Screenshot] Cannot capture Hub panorama: Korchma and portal renderers were not found in the active scene.");
            return;
        }

        left -= HubHorizontalMargin;
        right += HubHorizontalMargin;

        float normalWorldHeight = camera.orthographicSize * 2f;
        float pixelsPerWorldUnit = CaptureHeight / normalWorldHeight;
        int panoramaWidth = Mathf.CeilToInt((right - left) * pixelsPerWorldUnit);
        panoramaWidth = Mathf.Max(CaptureWidth, panoramaWidth);
        panoramaWidth += panoramaWidth % 2;

        if (panoramaWidth > SystemInfo.maxTextureSize)
        {
            Debug.LogWarning(
                $"[Screenshot] Hub panorama needs {panoramaWidth}px, but this GPU supports at most " +
                $"{SystemInfo.maxTextureSize}px. Capture was cancelled instead of changing the pixel scale.");
            return;
        }

        Vector3 originalCameraPosition = camera.transform.position;
        float originalAspect = camera.aspect;
        float panoramaCenterX = (left + right) * 0.5f;
        Vector3 panoramaCameraPosition = originalCameraPosition;
        panoramaCameraPosition.x = panoramaCenterX;
        Vector3 cameraDelta = panoramaCameraPosition - originalCameraPosition;
        var parallaxStates = MoveParallaxForCapture(cameraDelta);

        try
        {
            camera.transform.position = panoramaCameraPosition;
            camera.aspect = (float)panoramaWidth / CaptureHeight;
            CaptureCamera(camera, panoramaWidth, CaptureHeight, "RelicKeeper_Hub", $"{panoramaWidth}x{CaptureHeight}");
        }
        finally
        {
            camera.transform.position = originalCameraPosition;
            camera.aspect = originalAspect;
            RestoreParallax(parallaxStates);
        }
    }

    private static void CaptureCamera(Camera camera, int width, int height, string filePrefix, string sizeLabel)
    {
        var documents = HideUiDocuments();
        var canvases = HideCanvases();
        RenderTexture renderTexture = null;
        Texture2D texture = null;
        RenderTexture previousActive = RenderTexture.active;

        try
        {
            renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "EditorPixelPerfectScreenshot",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                antiAliasing = 1
            };
            renderTexture.Create();

            var request = new RenderPipeline.StandardRequest
            {
                destination = renderTexture,
                mipLevel = 0,
                slice = 0,
                face = CubemapFace.Unknown
            };
            RenderPipeline.SubmitRenderRequest(camera, request);

            RenderTexture.active = renderTexture;
            texture = new Texture2D(width, height, TextureFormat.RGB24, false, false)
            {
                filterMode = FilterMode.Point
            };
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            texture.Apply(false, false);

            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots"));
            Directory.CreateDirectory(directory);
            string fileName = $"{filePrefix}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.png";
            string filePath = Path.Combine(directory, fileName);
            File.WriteAllBytes(filePath, texture.EncodeToPNG());
            Debug.Log($"[Screenshot] Saved {sizeLabel} HUD-free screenshot: {filePath}");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            RenderTexture.active = previousActive;
            RestoreCanvases(canvases);
            RestoreUiDocuments(documents);

            if (texture != null)
                UnityEngine.Object.Destroy(texture);
            if (renderTexture != null)
            {
                renderTexture.Release();
                UnityEngine.Object.Destroy(renderTexture);
            }
        }
    }

    private readonly struct ParallaxState
    {
        public readonly Transform Transform;
        public readonly Vector3 Position;

        public ParallaxState(Transform transform)
        {
            Transform = transform;
            Position = transform.position;
        }
    }

    private static List<ParallaxState> MoveParallaxForCapture(Vector3 cameraDelta)
    {
        var states = new List<ParallaxState>();
        ParallaxEffect[] effects = UnityEngine.Object.FindObjectsByType<ParallaxEffect>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < effects.Length; i++)
        {
            ParallaxEffect effect = effects[i];
            if (effect == null || !effect.isActiveAndEnabled)
                continue;

            states.Add(new ParallaxState(effect.transform));
            float deltaY = effect.LockY ? 0f : cameraDelta.y * effect.ParallaxStrengthY;
            effect.transform.position += new Vector3(
                cameraDelta.x * effect.ParallaxStrengthX,
                deltaY,
                0f);
        }

        return states;
    }

    private static void RestoreParallax(List<ParallaxState> states)
    {
        for (int i = 0; i < states.Count; i++)
        {
            if (states[i].Transform != null)
                states[i].Transform.position = states[i].Position;
        }
    }

    private static bool TryGetHubHorizontalBounds(out float left, out float right)
    {
        left = float.PositiveInfinity;
        right = float.NegativeInfinity;

        bool hasTavern = TryIncludeRendererBounds("Korchma", ref left, ref right);
        bool hasFloorPortal = TryIncludeRendererBounds("FloorPortal", ref left, ref right);
        bool hasNextRoomPortal = TryIncludeRendererBounds("NextRoomPortal", ref left, ref right);
        return hasTavern && (hasFloorPortal || hasNextRoomPortal) && right > left;
    }

    private static bool TryIncludeRendererBounds(string objectName, ref float left, ref float right)
    {
        GameObject root = GameObject.Find(objectName);
        if (root == null)
            return false;

        bool found = false;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Bounds bounds = renderer.bounds;
            left = Mathf.Min(left, bounds.min.x);
            right = Mathf.Max(right, bounds.max.x);
            found = true;
        }

        return found;
    }

    private static Camera ResolveGameCamera()
    {
        if (Camera.main != null && Camera.main.isActiveAndEnabled)
            return Camera.main;

        Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].isActiveAndEnabled)
                return cameras[i];
        }

        return null;
    }

    private static List<DocumentState> HideUiDocuments()
    {
        var states = new List<DocumentState>();
        UIDocument[] documents = UnityEngine.Object.FindObjectsByType<UIDocument>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < documents.Length; i++)
        {
            VisualElement root = documents[i] != null ? documents[i].rootVisualElement : null;
            if (root == null)
                continue;

            states.Add(new DocumentState(root));
            root.style.display = DisplayStyle.None;
        }

        return states;
    }

    private static List<CanvasState> HideCanvases()
    {
        var states = new List<CanvasState>();
        Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null)
                continue;

            states.Add(new CanvasState(canvas));
            canvas.enabled = false;
        }

        return states;
    }

    private static void RestoreUiDocuments(List<DocumentState> states)
    {
        for (int i = 0; i < states.Count; i++)
        {
            if (states[i].Root != null)
                states[i].Root.style.display = states[i].Display;
        }
    }

    private static void RestoreCanvases(List<CanvasState> states)
    {
        for (int i = 0; i < states.Count; i++)
        {
            if (states[i].Canvas != null)
                states[i].Canvas.enabled = states[i].Enabled;
        }
    }
}

internal sealed class EditorScreenshotHotkey : MonoBehaviour
{
    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.f12Key.wasPressedThisFrame)
            return;

        if (keyboard.shiftKey.isPressed)
            EditorScreenshotCapture.CaptureHubPanorama();
        else
            EditorScreenshotCapture.CapturePixelPerfectWorld();
    }
}
#endif
