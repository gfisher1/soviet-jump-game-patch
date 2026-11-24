using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UdpKit;
using PlayFab.Internal;

namespace SjgPatch
{
    /// <summary>
    /// Hosting / joining local Bolt servers, plus the fixed host port.
    /// </summary>
    internal static class LocalMatch
    {
        internal const string MapA = "Soviet Jump Game Map A";
        internal const string MapB = "Soviet Jump Game Map B";
        internal const string MapMoon = "Soviet Jump Game Map Moon";
        internal const string Tutorial = "Soviet Jump Game Tutorial";

        private static float _lastHost;

        internal static void Host(string map = null)
        {
            if (Time.unscaledTime - _lastHost < 3f)
                return;
            _lastHost = Time.unscaledTime;

            try
            {
                map = string.IsNullOrEmpty(map) ? SjgPlugin.MatchMap.Value : map;
                if (string.IsNullOrEmpty(map))
                    map = MapA;

                int bots = Mathf.Max(0, SjgPlugin.BotCount.Value);
                NetworkDebugSettings nds = NetworkDebugSettings.Instance;
                nds.ServerBotAmount = (uint)bots;
                // PlayerMachine.Attached() randomizes bot skins by picking a palette out of the
                // PlayFab economy; with no catalog that list is empty and the lookup NREs *before*
                // the machine registers (which stalls the round). Disable it.
                nds.ServerBotSkinRandomization = false;
                nds.ClientBotSkinRandomization = false;

                ushort port = (ushort)Mathf.Clamp(SjgPlugin.HostPort.Value, 1, 65535);
                SjgPlugin.Log.LogInfo("Hosting local match: map='" + map + "', bots=" + bots + ", port=" + port);
                StartBoltServer(new UdpEndPoint(UdpIPv4Address.Any, port), map);

                SjgPlugin.Instance?.OnHosted(port, map);
            }
            catch (Exception e)
            {
                SjgPlugin.Log.LogError("Failed to host local match: " + e);
            }
        }

        /// <summary>Directly connect to a server (server browser / config).</summary>
        internal static void JoinAddress(string host, int port)
        {
            try
            {
                if (string.IsNullOrEmpty(host) || port <= 0 || port > 65535)
                {
                    SjgPlugin.Log.LogError("Invalid server address: " + host + ":" + port);
                    return;
                }

                SjgPlugin.Log.LogInfo("Connecting to " + host + ":" + port);
                BoltPro.ConnectDirect(host, (ushort)port,
                    SingletonMonoBehaviour<PlayFabManager>.Instance?.CurrentPlayer?.CreateToken());
            }
            catch (Exception e)
            {
                SjgPlugin.Log.LogError("Connect failed: " + e);
            }
        }

        /// <summary>Connect to the address from the config ("host:port").</summary>
        internal static void JoinConfigured()
        {
            string addr = SjgPlugin.DirectConnectAddress.Value;
            if (string.IsNullOrEmpty(addr))
            {
                SjgPlugin.Log.LogWarning("DirectConnectAddress is empty.");
                return;
            }

            int colon = addr.LastIndexOf(':');
            if (colon <= 0)
            {
                SjgPlugin.Log.LogError("DirectConnectAddress must be host:port, got '" + addr + "'");
                return;
            }

            string host = addr.Substring(0, colon);
            int port;
            if (!int.TryParse(addr.Substring(colon + 1), out port))
            {
                SjgPlugin.Log.LogError("Bad port in DirectConnectAddress: '" + addr + "'");
                return;
            }

            JoinAddress(host, port);
        }

        /// <summary>
        /// The default MATCHMAKING action: join the public server if one is listed, otherwise host a
        /// local bot match, so clicking MATCHMAKING always puts the player in a game.
        /// </summary>
        internal static void Matchmake()
        {
            if (SjgPlugin.Instance == null || !SjgPlugin.HasApi)
            {
                Host();
                return;
            }

            SjgPlugin.Instance.StartCoroutine(MatchmakeRoutine());
        }

        private static IEnumerator MatchmakeRoutine()
        {
            ServerBrowser.Refresh();

            float waited = 0f;
            while (ServerBrowser.Loading && waited < 8f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            ServerInfo pick = ServerBrowser.First();

            // Nothing advertised? Fall back to the configured default server address.
            if (pick == null && !string.IsNullOrEmpty(SjgPlugin.DirectConnectAddress.Value))
            {
                string addr = SjgPlugin.DirectConnectAddress.Value;
                int colon = addr.LastIndexOf(':');
                int port;
                if (colon > 0 && int.TryParse(addr.Substring(colon + 1), out port))
                    pick = new ServerInfo { ip = addr.Substring(0, colon), port = port, name = "default" };
            }

            if (pick != null)
            {
                SjgPlugin.Log.LogInfo("Matchmaking: joining '" + (pick.name ?? "") + "' at " + pick.ip + ":" + pick.port);
                JoinAddress(pick.ip, pick.port);
            }
            else
            {
                SjgPlugin.Log.LogInfo("Matchmaking: no server listed; hosting a local bot match.");
                Host();
            }
        }

        private static MethodInfo _startServer;

        private static void StartBoltServer(UdpEndPoint endpoint, string map)
        {
            if (_startServer == null)
            {
                Type launcher = null;
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type t = asm.GetType("Bolt.BoltLauncher", false) ?? asm.GetType("BoltLauncher", false);
                    if (t != null)
                    {
                        launcher = t;
                        break;
                    }
                }

                if (launcher == null)
                    throw new InvalidOperationException("Could not find BoltLauncher.");

                _startServer = launcher.GetMethod(
                    "StartServer", BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(UdpEndPoint), typeof(string) }, null);

                if (_startServer == null)
                    throw new InvalidOperationException("Could not find BoltLauncher.StartServer(UdpEndPoint,string).");
            }

            _startServer.Invoke(null, new object[] { endpoint, map });
        }
    }
}
