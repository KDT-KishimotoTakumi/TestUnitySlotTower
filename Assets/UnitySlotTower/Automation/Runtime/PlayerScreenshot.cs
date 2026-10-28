#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace UnitySlotTower.Automation
{
    // Opt-in diagnostic for development players. No scene or prefab edits are needed.
    public sealed class PlayerScreenshot : MonoBehaviour
    {
        private string outputPath;
        private bool runtimeError;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (Application.isEditor)
                return;
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, "-automationScreenshot");
            if (index < 0)
                return;
            if (index + 1 >= arguments.Length || !Path.IsPathRooted(arguments[index + 1]))
            {
                Debug.LogError("-automationScreenshot requires an absolute PNG path.");
                Application.Quit(2);
                return;
            }
            var owner = new GameObject("SlotTower Screenshot Automation");
            DontDestroyOnLoad(owner);
            var capture = owner.AddComponent<PlayerScreenshot>();
            capture.outputPath = arguments[index + 1];
        }

        private void OnEnable() => Application.logMessageReceived += OnLog;
        private void OnDisable() => Application.logMessageReceived -= OnLog;

        private void OnLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                runtimeError = true;
        }

        private IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(3);
            yield return new WaitForEndOfFrame();
            bool saved = false;
            Texture2D image = null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                image = ScreenCapture.CaptureScreenshotAsTexture();
                if (image == null)
                    throw new InvalidOperationException("No rendered frame was available.");
                File.WriteAllBytes(outputPath, image.EncodeToPNG());
                saved = true;
                Debug.Log("[SlotTower Automation] Screenshot saved: " + outputPath);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                if (image != null)
                    Destroy(image);
            }
            Application.Quit(saved && !runtimeError ? 0 : 1);
        }
    }
}
#endif
