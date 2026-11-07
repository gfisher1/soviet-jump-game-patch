using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace SjgPatch
{
    [Serializable]
    internal class ServerInfo
    {
        public string id;
        public string name;
        public string ip;
        public int port;
        public int players;
        public int maxPlayers;
        public string map;
        public string region;
        public bool @private;
        public bool password;
    }

    /// <summary>
    /// Server list, fetched from the backend's /servers endpoint. Surfaced in-game through the
    /// Quantum Console (see SjgCommands) since custom IMGUI does not render in this build.
    /// </summary>
    internal static class ServerBrowser
    {
        private static List<ServerInfo> _servers = new List<ServerInfo>();
        private static string _status = "not fetched yet";
        private static bool _loading;

        internal static void Refresh()
        {
            if (_loading)
                return;
            if (SjgPlugin.Instance == null || !SjgPlugin.HasApi)
            {
                _status = "no Server.ApiBaseUrl configured";
                return;
            }
            SjgPlugin.Instance.StartCoroutine(FetchRoutine());
        }

        private static IEnumerator FetchRoutine()
        {
            _loading = true;
            using (UnityWebRequest req = UnityWebRequest.Get(SjgPlugin.ApiUrl("servers")))
            {
                req.timeout = 6;
                yield return req.SendWebRequest();

                if (req.isNetworkError || req.isHttpError)
                {
                    _status = "error: " + req.error;
                }
                else
                {
                    try
                    {
                        _servers = JsonConvert.DeserializeObject<List<ServerInfo>>(req.downloadHandler.text)
                                   ?? new List<ServerInfo>();
                        _status = _servers.Count + " server(s)";
                    }
                    catch (Exception e)
                    {
                        _status = "bad response: " + e.Message;
                    }
                }
            }
            _loading = false;
        }

        internal static List<string> Describe()
        {
            var lines = new List<string>();
            lines.Add("status: " + _status);
            for (int i = 0; i < _servers.Count; i++)
            {
                ServerInfo s = _servers[i];
                lines.Add(string.Format("[{0}] {1}  {2}:{3}  {4}/{5}{6}{7}", i, s.name, s.ip, s.port,
                    s.players, s.maxPlayers,
                    string.IsNullOrEmpty(s.map) ? "" : ("  " + s.map),
                    s.@private ? "  [private]" : ""));
            }
            if (_servers.Count == 0)
                lines.Add("(no servers - run sjg-refresh)");
            return lines;
        }

        internal static void JoinIndex(int index)
        {
            if (index < 0 || index >= _servers.Count)
            {
                SjgPlugin.Log.LogWarning("No such server index: " + index + " (run sjg-servers)");
                return;
            }
            ServerInfo s = _servers[index];
            LocalMatch.JoinAddress(s.ip, s.port);
        }
    }
}
