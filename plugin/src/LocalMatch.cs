using System;
using System.Reflection;
using UnityEngine;
using UdpKit;

namespace SjgPatch
{
    /// <summary>
    /// Hosts a local Bolt server (the same mechanism the game's own PRACTICE button uses)
    /// on a normal map, with bots filling the lobby - so you can play a full match solo.
    ///
    /// On a non-tutorial map the server spawns <see cref="NetworkDebugSettings.ServerBotAmount"/>
    /// bots (see ServerCallbacks.HandleServerPlayerSpawning).
    /// </summary>
    internal static class LocalMatch
    {
        internal const string MapA = "Soviet Jump Game Map A";
        internal const string MapB = "Soviet Jump Game Map B";
        internal const string MapMoon = "Soviet Jump Game Map Moon";
        internal const string Tutorial = "Soviet Jump Game Tutorial";

        private static float _lastHost;
        private static MethodInfo _startServer;

        internal static void Host(string map = null)
        {
            // Debounce: avoid double-triggering from key repeat / double clicks.
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
                // PlayFab economy. With no catalog that list is empty, so the lookup returns null
                // and the NullReferenceException fires *before* the machine registers itself -
                // which leaves PlayerMachine.ControllerPlayer unset and stalls the round. We have
                // no catalog, so disable skin randomization.
                nds.ServerBotSkinRandomization = false;
                nds.ClientBotSkinRandomization = false;

                SjgPlugin.Log.LogInfo("Hosting local match: map='" + map + "', bots=" + bots);
                StartBoltServer(map);
            }
            catch (Exception e)
            {
                SjgPlugin.Log.LogError("Failed to host local match: " + e);
            }
        }

        // BoltLauncher lives in bolt.dll; its exact namespace differs between builds, so
        // resolve it by reflection and only ever bind to StartServer(UdpEndPoint, string).
        private static void StartBoltServer(string map)
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
                    "StartServer",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(UdpEndPoint), typeof(string) },
                    null);

                if (_startServer == null)
                    throw new InvalidOperationException("Could not find BoltLauncher.StartServer(UdpEndPoint,string).");
            }

            _startServer.Invoke(null, new object[] { UdpEndPoint.Any, map });
        }
    }
}
