using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace SjgPatch
{
    /// <summary>
    /// Advertises a locally-hosted match to the backend (/server/heartbeat) so other players'
    /// server browsers can find it. Runs on its own DontDestroyOnLoad object because the
    /// BepInEx plugin object does not survive a scene load.
    /// </summary>
    internal class HeartbeatBehaviour : MonoBehaviour
    {
        private ushort _port;
        private string _map;
        private bool _started;

        internal static void Start(ushort port, string map)
        {
            var go = new GameObject("SjgHeartbeat");
            UnityEngine.Object.DontDestroyOnLoad(go);
            var hb = go.AddComponent<HeartbeatBehaviour>();
            hb._port = port;
            hb._map = map;
            SjgPlugin.Log.LogInfo("Heartbeat object created (port=" + port + ").");
        }

        private void Start()
        {
            if (!_started)
            {
                _started = true;
                StartCoroutine(Loop());
            }
        }

        private IEnumerator Loop()
        {
            // Wait for Bolt to actually be serving.
            float start = Time.realtimeSinceStartup;
            int i = 0;
            while (!IsServing() && Time.realtimeSinceStartup - start < 60f)
            {
                if (i++ % 60 == 0)
                    SjgPlugin.Log.LogInfo("Heartbeat waiting for Bolt to serve...");
                yield return null;
            }

            if (!IsServing())
            {
                SjgPlugin.Log.LogWarning("Not registering server: Bolt is not serving.");
                yield break;
            }

            string sid = System.Guid.NewGuid().ToString("N").Substring(0, 12);
            string name = "Comrade";
            try { if (Steamworks.SteamClient.IsValid) name = Steamworks.SteamClient.Name; } catch { }

            SjgPlugin.Log.LogInfo("Advertising hosted match to " + SjgPlugin.ApiUrl("server/heartbeat"));

            while (IsServing())
            {
                yield return Send(sid, name);
                yield return new WaitForSeconds(5f);
            }
        }

        private IEnumerator Send(string sid, string name)
        {
            string body = "{\"SessionId\":\"" + sid + "\",\"Address\":\"\",\"Port\":" + _port +
                          ",\"Players\":[],\"Region\":\"USSR\",\"State\":\"AcceptingPlayers\",\"Private\":false" +
                          ",\"Metadata\":{\"Map\":\"" + Esc(_map) + "\",\"MaxPlayers\":48,\"Name\":\"" + Esc(name) + "\"}}";

            UnityWebRequest req = null;
            try
            {
                req = new UnityWebRequest(SjgPlugin.ApiUrl("server/heartbeat"), "POST");
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 5;
            }
            catch (Exception e)
            {
                SjgPlugin.Log.LogWarning("Heartbeat setup failed: " + e.Message);
            }

            if (req != null)
            {
                yield return req.SendWebRequest();
                if (req.isNetworkError || req.isHttpError)
                    SjgPlugin.Log.LogWarning("Heartbeat failed: " + req.error);
                req.Dispose();
            }
        }

        private static string Esc(string s)
        {
            return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static bool IsServing()
        {
            try { return BoltNetwork.IsServer; }
            catch { return false; }
        }
    }
}
