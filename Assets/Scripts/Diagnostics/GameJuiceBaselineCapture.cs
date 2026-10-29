#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RelicKeeper.Diagnostics
{
    // Explicit opt-in recording only. Never drives input or modifies gameplay.
    // Four samples/second are for whole-run readability, not frame-timing analysis.
    public sealed class GameJuiceBaselineCapture : MonoBehaviour
    {
        private StreamWriter _timeline;
        private string _folder;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (Application.isEditor || !Environment.GetCommandLineArgs().Contains("-game-juice-baseline")) return;
            var host = new GameObject("Juice baseline recording");
            DontDestroyOnLoad(host);
            host.AddComponent<GameJuiceBaselineCapture>();
        }

        private IEnumerator Start()
        {
            _folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "GameJuiceBaseline",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(_folder);
            SceneManager.sceneLoaded += OnSceneLoaded;
            StartCoroutine(DescribeRendering());
            _timeline = new StreamWriter(Path.Combine(_folder, "frames.csv")) { AutoFlush = true };
            _timeline.WriteLine("frame,realtime,scene");
            File.WriteAllText(Path.Combine(_folder, "environment.txt"),
                "Development build; normal scenes and rules.\nSave folder: " + Application.persistentDataPath +
                "\nSampling: 4 fps maximum; actual unscaled timestamps in frames.csv. Not a performance test.\n");
            double start = Time.realtimeSinceStartupAsDouble;
            double next = start;
            int frame = 0;
            while (Time.realtimeSinceStartupAsDouble - start < 1200)
            {
                yield return new WaitForEndOfFrame();
                double now = Time.realtimeSinceStartupAsDouble;
                if (now < next) continue;
                var image = ScreenCapture.CaptureScreenshotAsTexture();
                try { File.WriteAllBytes(Path.Combine(_folder, frame.ToString("D5") + ".jpg"), image.EncodeToJPG(75)); }
                finally { Destroy(image); }
                _timeline.WriteLine(frame + "," + (now - start).ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                    "," + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                frame++;
                next = now + .25;
            }
            _timeline.Dispose();
            _timeline = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) { StartCoroutine(DescribeRendering()); }

        private IEnumerator DescribeRendering()
        {
            yield return new WaitForSecondsRealtime(1f);
            var report = new StringBuilder();
            report.AppendLine("Pipeline: " + UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline);
            report.AppendLine("Quality: " + QualitySettings.names[QualitySettings.GetQualityLevel()]);
            foreach (var group in FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .GroupBy(r => r.sharedMaterial).Take(30))
            {
                var material = group.Key;
                report.AppendLine(group.Count() + " renderers; " + group.First().name + "; " +
                    (material == null ? "NULL MATERIAL" : material.name + "; " + material.shader.name +
                    "; supported=" + material.shader.isSupported + "; passes=" + material.passCount +
                    "; keywords=" + string.Join(" ", material.shaderKeywords)));
            }
            File.WriteAllText(Path.Combine(_folder, SceneManager.GetActiveScene().name + "-rendering.txt"), report.ToString());
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _timeline?.Dispose();
        }
    }
}
#endif
