using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using UdpKit;
using UnityEngine;

namespace SjgPatch
{
    /// <summary>
    /// Harmony patches that remove the client's dependency on the dead services.
    /// </summary>
    internal static class Patches
    {
        // EPB.Networking.Authentication.CheckForAuth runs at BeforeSceneLoad and kicks off the
        // (failing) Steam -&gt; PlayFab login. We replace the authentication flow entirely, but we
        // still have to bring Steam up first: the original routine does it synchronously here, and
        // menu code (OptionsMenu.PopulateMenuItems) touches SteamApps.CurrentBetaName during the
        // next scene load.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(EPB.Networking.Authentication), "CheckForAuth")]
        private static bool CheckForAuth_Prefix()
        {
            try
            {
                if (!Steamworks.SteamClient.IsValid)
                {
                    SjgPlugin.Log.LogInfo("Initializing Steam client.");
                    Steamworks.SteamClient.Init(1072710u);
                    LobbyManager.Initialize();
                }
            }
            catch (Exception e)
            {
                SjgPlugin.Log.LogWarning("Steam initialization failed (continuing without Steam): " + e.Message);
            }

            return false;
        }

        // ForceUpdatePlayFab.AuthenticationRoutine blocks forever waiting on a PlayFab login that
        // will never succeed, and gates the menu behind ForceUpdatePlayFab.Initialized.
        // Skip it, flag Initialized, and hand back an empty coroutine so StartCoroutine stays happy.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ForceUpdatePlayFab), "AuthenticationRoutine")]
        private static bool AuthenticationRoutine_Prefix(ref IEnumerator __result)
        {
            ForceUpdatePlayFab.Initialized = true;
            __result = EmptyRoutine();
            return false;
        }

        private static IEnumerator EmptyRoutine()
        {
            yield break;
        }

        // Unlock the developer console / tools.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ClientPermission), "get_IsDeveloper")]
        private static void IsDeveloper_Postfix(ref bool __result)
        {
            if (SjgPlugin.ForceDeveloper.Value)
                __result = true;
        }

        private static FieldInfo _retrievedNameField;
        // When the server spawns the local player it asks PlayFab for that player's
        // customization. Those calls throw synchronously ("Must be logged in"), which aborts
        // ServerCallbacks.PlayerSpawnRoutine and leaves us without a player. Feed the local
        // default values instead (the game already falls back to local defaults for null ids).
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerObject), "RetrievePlayFabCustomizeData")]
        private static bool RetrievePlayFabCustomizeData_Prefix(PlayerObject __instance)
        {
            if (!SjgPlugin.OfflineMode.Value)
                return true;

            try
            {
                if (_retrievedNameField == null)
                {
                    _retrievedNameField = typeof(PlayerObject)
                        .GetField("RetrievedName", BindingFlags.Instance | BindingFlags.NonPublic);
                }

                string name = "Comrade";
                try
                {
                    if (Steamworks.SteamClient.IsValid && !string.IsNullOrEmpty(Steamworks.SteamClient.Name))
                        name = Steamworks.SteamClient.Name;
                }
                catch { }

                _retrievedNameField.SetValue(__instance, name);
                __instance.UserAccountInfoRetrieved = true;
                __instance.UserDataRetrieved = true;
                __instance.UserStatisticsRetrieved = true;
            }
            catch (Exception e)
            {
                SjgPlugin.Log.LogError("Could not stub player customization: " + e);
            }

            return false;
        }

        // MATCHMAKING button -> host a local bot match, or join a configured server, instead of
        // contacting the dead services. Patched at the public entry point so none of the
        // matchmaking/cancel-token machinery runs (mirrors the PRACTICE button).
        [HarmonyPrefix]
        [HarmonyPatch(typeof(EPB.Networking.MatchConnection), "ConnectToMatchmaking")]
        private static bool ConnectToMatchmaking_Prefix()
        {
            string action = SjgPlugin.MatchmakingAction.Value;

            if (string.Equals(action, "Vanilla", StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(action, "Connect", StringComparison.OrdinalIgnoreCase))
            {
                LocalMatch.JoinConfigured();
                return false;
            }

            if (string.Equals(action, "Host", StringComparison.OrdinalIgnoreCase))
            {
                LocalMatch.Host();
                return false;
            }

            // "Auto" (the default): join the default public server, else host a local bot match.
            LocalMatch.Matchmake();
            return false;
        }

        private static MethodInfo _assignAllDefaults;

        // PlayerObject.RandomizeCustomization (used when the server spawns bots on a normal map)
        // contains `do { ... } while (palette == null)`. With no PlayFab catalog the economy has
        // no palettes, so the loop never terminates and bot spawning hangs. Provide a bounded
        // version that just assigns local defaults.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerObject), "RandomizeCustomization")]
        private static bool RandomizeCustomization_Prefix(PlayerObject __instance)
        {
            if (!SjgPlugin.OfflineMode.Value)
                return true;

            try
            {
                if (_assignAllDefaults == null)
                {
                    _assignAllDefaults = typeof(PlayerObject)
                        .GetMethod("AssignAllDefaults", BindingFlags.Instance | BindingFlags.NonPublic);
                }

                if (_retrievedNameField == null)
                {
                    _retrievedNameField = typeof(PlayerObject)
                        .GetField("RetrievedName", BindingFlags.Instance | BindingFlags.NonPublic);
                }

                string name = "Comrade";
                try
                {
                    List<string> pool = PlayerMachine.BotNamePool;
                    if (pool != null && pool.Count > 0)
                        name = pool[UnityEngine.Random.Range(0, pool.Count)];
                }
                catch { }

                _assignAllDefaults.Invoke(__instance, null);
                _retrievedNameField.SetValue(__instance, name);
            }
            catch (Exception e)
            {
                SjgPlugin.Log.LogError("Safe bot customization failed: " + e);
            }

            return false;
        }

        // PlayerObjectRegistry.CreateBotPlayer(null, ...) internally calls CreateServerPlayer(), so
        // bots are created with IsServer=true and pollute PlayerObjectRegistry.ServerPlayer. That
        // makes ServerCallbacks.HandleServerPlayerSpawning bail out early (the "real" local server
        // player is never spawned), leaving PlayerMachine.ControllerPlayer null and the round stuck.
        // Make ServerPlayer ignore bots.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerObjectRegistry), "get_ServerPlayer")]
        private static void GetServerPlayer_Postfix(ref PlayerObject __result)
        {
            if (__result != null && !__result.IsBot)
                return;

            __result = null;
            foreach (PlayerObject p in PlayerObjectRegistry.Players)
            {
                if (p != null && p.IsServer && !p.IsBot)
                {
                    __result = p;
                    return;
                }
            }
        }

        // When you host/play as the server, the local player is the "server player" and its
        // PlayerMachine is supposed to become PlayerMachine.ControllerPlayer. Depending on spawn
        // order it can still be null when RoundManager.Update() dereferences
        // PlayerMachine.ControllerPlayer.CurrentBiomeData (null -> NRE spam that stalls the round).
        // Make sure it's set before the round logic runs.
        private static bool _loggedControllerDiag;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(RoundManager), "Update")]
        private static void RoundManagerUpdate_EnsureControllerPlayer()
        {
            if (PlayerMachine.ControllerPlayer != null)
                return;

            try
            {
                PlayerMachine candidate = null;

                PlayerObject server = PlayerObjectRegistry.ServerPlayer;
                if (server != null && server.Machine != null && !server.Machine.IsBot)
                {
                    candidate = server.Machine;
                }
                else
                {
                    // Fallback: the local player's machine is the only non-bot machine in a
                    // solo host. (ServerPlayer can lag behind machine registration.)
                    foreach (PlayerMachine m in PlayerObjectRegistry.PlayerMachines)
                    {
                        if (m != null && !m.IsBot)
                        {
                            candidate = m;
                            break;
                        }
                    }
                }

                if (candidate != null && PlayerMachine.ControllerPlayer == null)
                {
                    PlayerMachine.ControllerPlayer = candidate;
                    SjgPlugin.Log.LogInfo("Assigned ControllerPlayer = " + candidate.Name + ".");
                }
                else if (candidate == null && !_loggedControllerDiag)
                {
                    _loggedControllerDiag = true;
                    SjgPlugin.Log.LogWarning("No controller player candidate yet (serverPlayer="
                        + (server == null ? "null" : "ok")
                        + ", playerMachines=" + PlayerObjectRegistry.PlayerMachines.Count + ").");
                }
            }
            catch (Exception e)
            {
                SjgPlugin.Log.LogError("Could not assign ControllerPlayer: " + e);
            }
        }

        private static FieldInfo _apiBaseField;

        // RoundManager maintains bots via GetTargetBotPlayerCount(). Keep the lobby topped up: with
        // fewer humans than BotCount, spawn the difference as bots so nobody waits for other
        // players - and let the bots yield their slots as real players join.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(RoundManager), "GetTargetBotPlayerCount")]
        private static bool GetTargetBotPlayerCount_Prefix(ref int __result)
        {
            if (!SjgPlugin.HostLocalMatches.Value)
                return true;

            int humans = 0;
            try
            {
                foreach (PlayerObject p in PlayerObjectRegistry.Players)
                {
                    if (p != null && !p.IsBot)
                        humans++;
                }
            }
            catch { }

            if (humans < 1)
                humans = 1;

            __result = Math.Max(0, SjgPlugin.BotCount.Value - (humans - 1));
            return false;
        }

        // The real QoS probe pings PlayFab's QoS endpoints, which are gone; with no PlayFab login
        // it throws and aborts matchmaking before the join. Our own matchmaking interception never
        // uses it, so return an empty (successful) result rather than letting it explode.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayFab.QoS.PlayFabQosApi), "GetQosResultAsync")]
        private static bool GetQosResultAsync_Prefix(ref Task<PlayFab.QoS.QosResult> __result)
        {
            try
            {
                var result = new PlayFab.QoS.QosResult
                {
                    RegionResults = new List<PlayFab.QoS.QosRegionResult>(),
                    ErrorCode = 0,
                    ErrorMessage = null
                };
                __result = Task.FromResult(result);
                return false;
            }
            catch (Exception e)
            {
                SjgPlugin.Log.LogWarning("QoS stub failed (continuing with the real probe): " + e.Message);
                return true;
            }
        }

        /// <summary>Point the custom backend calls at a self-hosted server instead of the dead domain.</summary>
        internal static void RedirectApi(string url)
        {
            try
            {
                if (_apiBaseField == null)
                {
                    _apiBaseField = typeof(EPB.Networking.Backend.SovietAPI)
                        .GetField("BASE_API_URL", BindingFlags.Static | BindingFlags.NonPublic);
                }

                if (_apiBaseField == null)
                {
                    SjgPlugin.Log.LogError("Could not find SovietAPI.BASE_API_URL field.");
                    return;
                }

                _apiBaseField.SetValue(null, new Uri(url));
                SjgPlugin.Log.LogInfo("SovietAPI base URL redirected to " + url);
            }
            catch (Exception e)
            {
                SjgPlugin.Log.LogError("Failed to redirect API base URL: " + e);
            }
        }

        /// <summary>Find the game's BoltLauncher type (namespace varies between builds).</summary>
        internal static Type FindBoltLauncher()
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = asm.GetType("Bolt.BoltLauncher", false) ?? asm.GetType("BoltLauncher", false);
                if (t != null)
                    return t;
            }

            return null;
        }

        /// <summary>
        /// Pin every Bolt server to SjgPlugin.HostPort.
        ///
        /// The headless game server starts its session with a wildcard endpoint (port 0), so the OS
        /// hands out a random UDP port and nobody can connect. Rewriting the endpoint makes the
        /// server bind a known, published port instead.
        /// </summary>
        internal static void ForceServerPort_Prefix(ref UdpEndPoint endpoint)
        {
            try
            {
                ushort port = (ushort)Mathf.Clamp(SjgPlugin.HostPort.Value, 1, 65535);
                if (!SjgPlugin.ForceServerPort.Value)
                    return;

                SjgPlugin.Log.LogInfo("Bolt StartServer endpoint " + endpoint + " -> forcing port " + port + ".");
                endpoint = new UdpEndPoint(UdpIPv4Address.Any, port);
            }
            catch (Exception e)
            {
                SjgPlugin.Log.LogWarning("Could not force the Bolt server port: " + e.Message);
            }
        }

        /// <summary>Reflection-patch every BoltLauncher.StartServer overload (compile-safe).</summary>
        internal static void PatchBoltServerPort(Harmony harmony)
        {
            try
            {
                Type launcher = FindBoltLauncher();
                if (launcher == null)
                {
                    SjgPlugin.Log.LogWarning("BoltLauncher not found; the server port is not pinned.");
                    return;
                }

                MethodInfo prefix = typeof(Patches).GetMethod(
                    "ForceServerPort_Prefix",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

                int patched = 0;
                foreach (MethodInfo m in launcher.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (m.Name != "StartServer")
                        continue;

                    ParameterInfo[] ps = m.GetParameters();
                    if (ps.Length < 1 || ps[0].ParameterType != typeof(UdpEndPoint))
                        continue;

                    harmony.Patch(m, new HarmonyMethod(prefix));
                    patched++;
                    SjgPlugin.Log.LogInfo("Pinned the Bolt port on " + launcher.FullName + "." + m.Name + "(" + ps.Length + " args).");
                }

                if (patched == 0)
                    SjgPlugin.Log.LogWarning("No BoltLauncher.StartServer overload found to pin the port.");
            }
            catch (Exception e)
            {
                SjgPlugin.Log.LogError("Failed to pin the Bolt server port: " + e);
            }
        }
    }
}
