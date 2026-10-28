using System;
using System.IO;
using MCPForUnity.Editor.Services;
using UnityEditor;
using UnityEngine;

namespace UnitySlotTower.Automation.Editor
{
    [InitializeOnLoad]
    public static class AutomationConnection
    {
        private const string Endpoint = "http://127.0.0.1:8080";
        private const double CheckIntervalSeconds = 5;
        private static bool connecting;
        private static bool disconnecting;
        private static double nextCheck;
        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
        private static string EnabledPath => Path.Combine(ProjectRoot, "UserSettings/SlotTowerAutomation.enabled");
        private static string DisconnectRequestPath => Path.Combine(ProjectRoot, "UserSettings/SlotTowerAutomation.disconnect");
        private static string DisconnectAcknowledgedPath => Path.Combine(ProjectRoot, "UserSettings/SlotTowerAutomation.disconnected");

        static AutomationConnection()
        {
            if (!Application.isBatchMode)
                EditorApplication.update += ConnectWhenReady;
        }

        private static void ConnectWhenReady()
        {
            if (EditorApplication.timeSinceStartup < nextCheck || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            nextCheck = EditorApplication.timeSinceStartup + CheckIntervalSeconds;
            if (!File.Exists(EnabledPath) || File.Exists(DisconnectRequestPath))
            {
                if (!connecting && !disconnecting)
                    Disconnect();
                return;
            }
            if (!connecting && !disconnecting && !MCPServiceLocator.Bridge.IsRunning &&
                MCPServiceLocator.Server.IsLocalHttpServerReachable())
                Connect();
        }

        private static async void Disconnect()
        {
            disconnecting = true;
            try
            {
                if (MCPServiceLocator.Bridge.IsRunning)
                    await MCPServiceLocator.Bridge.StopAsync();
                if (File.Exists(DisconnectRequestPath))
                    File.WriteAllText(DisconnectAcknowledgedPath, "disconnected");
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning("[SlotTower Automation] Disconnect failed: " + exception.Message);
            }
            finally
            {
                disconnecting = false;
            }
        }

        [MenuItem("Tools/UnitySlotTower/Automation/Connect")]
        public static async void Connect()
        {
            if (connecting || disconnecting || Application.isBatchMode || File.Exists(DisconnectRequestPath))
                return;
            string server = Path.Combine(ProjectRoot, ".tools/unity-mcp/Scripts/mcp-for-unity.exe");
            if (!File.Exists(server))
            {
                UnityEngine.Debug.LogWarning("[SlotTower Automation] Run scripts/unity/Setup-UnityAutomation.ps1 first.");
                return;
            }

            connecting = true;
            try
            {
                var configuration = EditorConfigurationCache.Instance;
                configuration.SetUseHttpTransport(true);
                configuration.SetHttpTransportScope("local");
                configuration.SetHttpBaseUrl(Endpoint);
                configuration.SetUvxPathOverride(Path.Combine(ProjectRoot, ".tools/unity-mcp/Scripts/uvx.exe"));
                if (MCPServiceLocator.Bridge.IsRunning)
                    return;
                if (!MCPServiceLocator.Server.IsLocalHttpServerReachable())
                {
                    UnityEngine.Debug.LogWarning("[SlotTower Automation] Run scripts/unity/Start-UnityAutomation.ps1 first.");
                    return;
                }
                if (await MCPServiceLocator.Bridge.StartAsync())
                    UnityEngine.Debug.Log("[SlotTower Automation] Connected to " + Endpoint);
                else
                    UnityEngine.Debug.LogWarning("[SlotTower Automation] Connection failed. Check the automation guide.");
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning("[SlotTower Automation] " + exception.Message);
            }
            finally
            {
                connecting = false;
            }
        }
    }
}
