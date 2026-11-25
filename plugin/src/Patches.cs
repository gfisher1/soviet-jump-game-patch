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
            // Server-side only. On a client the game assigns ControllerPlayer to the local player's
            // machine itself (PlayerMachine attach does "if (IsController() && ControllerPlayer == null)
            // ControllerPlayer = this"), so grabbing a machine here hijacks the local input and camera
            // (the player then falls through the floor and the HUD NREs on detached entities).
            if (!BoltNetwork.IsServer)
                return;

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

                    // Dedicated/headless server: there is no human player at all, only bots.
                    // RoundManager.Update() dereferences PlayerMachine.ControllerPlayer
                    // unguarded (IL_032d -> CurrentBiomeData), so leaving it null NRE-spams the
                    // round every frame and the round logic never runs. Fall back to any machine,
                    // bot or not, so the round can progress.
                    if (candidate == null)
                    {
                        foreach (PlayerMachine m in PlayerObjectRegistry.PlayerMachines)
                        {
                            if (m != null)
                            {
                                candidate = m;
                                break;
                            }
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

        // The retail headless server is a one-match server: RoundManager.UpdatePlayerCount() calls
        // ShutdownRoutine(ZeroPlayersWhileRoundInProgress) the instant a round starts with zero Bolt
        // connections, so an idle public server quits ~5s later and the container has to restart
        // (~40s of downtime). Bots are not Bolt connections, so even a server full of bots counts as
        // "empty". Suppress just that one shutdown so players can connect at any time; a completed
        // match still shuts the server down (PostMatchShutdown) as intended.
        private static bool _loggedKeepAlive;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(RoundManager), "ShutdownRoutine")]
        private static bool ShutdownRoutine_Prefix(RoundManager.ShutdownCode code, ref System.Collections.IEnumerator __result)
        {
            if (SjgPlugin.KeepAliveWhenEmpty == null || !SjgPlugin.KeepAliveWhenEmpty.Value)
                return true;

            if (code != RoundManager.ShutdownCode.ZeroPlayersWhileRoundInProgress)
                return true;

            __result = EmptyRoutine();
            if (!_loggedKeepAlive)
            {
                _loggedKeepAlive = true;
                SjgPlugin.Log.LogInfo("[server] keeping the empty server alive (suppressed ZeroPlayersWhileRoundInProgress).");
            }
            return false;
        }

        // The retail server starts a round automatically PreRoundDuration seconds after the map loads
        // (RoundManager.Update sets RoundInProgress once SyncTime.Time - state.PreRoundTimer >
        // PreRoundDuration + 1). With only bots present the round plays itself out - the closing wall
        // instakills anyone left - so a player who joins then spawns into the end of a round and loses
        // instantly without playing. While no human is connected, keep pushing PreRoundTimer forward so
        // the countdown never completes; the first player to connect then gets a full round from the start.
        private static bool _loggedHoldRound;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(RoundManager), "Update")]
        private static void RoundManagerUpdate_HoldRoundWithoutPlayers()
        {
            if (SjgPlugin.HoldRoundWithoutPlayers == null || !SjgPlugin.HoldRoundWithoutPlayers.Value)
                return;

            try
            {
                if (!BoltNetwork.IsServer)
                    return;

                foreach (BoltConnection c in BoltNetwork.Connections)
                    return; // somebody is connected - let the round run normally

                RoundManager rm = RoundManager.CurrentRound;
                if (rm == null)
                    return;

                IRoundState st = rm.state;
                if (st == null || st.RoundInProgress)
                    return;

                st.PreRoundTimer = SyncTime.Time;

                if (!_loggedHoldRound)
                {
                    _loggedHoldRound = true;
                    SjgPlugin.Log.LogInfo("[server] holding the pre-round countdown until a player connects.");
                }
            }
            catch (Exception e)
            {
                SjgPlugin.Log.LogWarning("Could not hold the round: " + e.Message);
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
                // The game's region selection runs a LINQ First()/Single() over this list, so an
                // EMPTY list throws "Sequence contains no elements" and aborts the vanilla join.
                var regions = new List<PlayFab.QoS.QosRegionResult>();
                var one = new PlayFab.QoS.QosRegionResult();
                try
                {
                    foreach (FieldInfo f in typeof(PlayFab.QoS.QosRegionResult)
                                 .GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        string n = f.Name.ToLowerInvariant();
                        if (f.FieldType == typeof(string))
                            f.SetValue(one, "USSR");
                        else if (f.FieldType == typeof(int))
                            f.SetValue(one, (n.Contains("latency") || n.Contains("ping")) ? 50 : 0);
                    }
                }
                catch (Exception ex)
                {
                    SjgPlugin.Log.LogWarning("Could not populate the QoS region entry: " + ex.Message);
                }
                regions.Add(one);

                var result = new PlayFab.QoS.QosResult
                {
                    RegionResults = regions,
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
                if (!SjgPlugin.ForceServerPort.Value)
                    return;

                ushort port = (ushort)Mathf.Clamp(SjgPlugin.HostPort.Value, 1, 65535);
                string before = endpoint == null ? "<null>" : endpoint.ToString();

                // Only pin a real listen endpoint (wildcard address, port 0). Session/id
                // endpoints carry Bolt's internal references and must be left alone, or the
                // server never manages to bind its UDP socket at all.
                if (before.IndexOf("0.0.0.0:0", StringComparison.Ordinal) < 0)
                {
                    SjgPlugin.Log.LogInfo("Bolt StartServer endpoint " + before + " -> left unchanged.");
                    return;
                }

                endpoint = new UdpEndPoint(UdpIPv4Address.Any, port);
                SjgPlugin.Log.LogInfo("Bolt StartServer endpoint " + before + " -> " + endpoint + " (pinned to " + port + ").");
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

        // --- malformed input packets must not drop the connection -----------------------------
        //
        // Bolt's EntityChannel.CommandChannel.ReadInput does "var cmd = Factory.NewCommand(typeId);
        // cmd.Sequence = ..." with no null check. If a client sends a command type this side has not
        // registered (Factory.Create returns null for an unknown TypeId), the NRE escapes
        // BoltConnection.PacketReceived, which treats it as fatal and drops the connection - the
        // player is kicked mid-match for no visible reason. Each Bolt packet is framed
        // independently, so swallowing the exception only discards the remainder of that one packet
        // and the connection survives.
        private static bool _loggedInputNre;

        private static Exception CommandChannelReadInput_Finalizer(Exception __exception)
        {
            if (__exception is NullReferenceException)
            {
                if (!_loggedInputNre)
                {
                    _loggedInputNre = true;
                    SjgPlugin.Log.LogWarning(
                        "[server] swallowed a malformed input packet (Bolt ReadInput NRE); the connection stays alive.");
                }
                return null;
            }

            return __exception;
        }

        /// <summary>Keep the connection alive when Bolt chokes on a malformed input packet.</summary>
        internal static void PatchBoltInputReader(Harmony harmony)
        {
            try
            {
                Type commandChannel = null;
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type t = asm.GetType("Bolt.EntityChannel+CommandChannel", false)
                        ?? asm.GetType("EntityChannel+CommandChannel", false);
                    if (t != null)
                    {
                        commandChannel = t;
                        break;
                    }
                }

                if (commandChannel == null)
                {
                    SjgPlugin.Log.LogWarning("Bolt CommandChannel not found; malformed packets may still drop players.");
                    return;
                }

                MethodInfo finalizer = typeof(Patches).GetMethod(
                    "CommandChannelReadInput_Finalizer",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

                int patched = 0;
                foreach (MethodInfo m in commandChannel.GetMethods(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (m.Name != "ReadInput")
                        continue;

                    ParameterInfo[] ps = m.GetParameters();
                    if (ps.Length != 1 || ps[0].ParameterType.Name != "Packet")
                        continue;

                    harmony.Patch(m, finalizer: new HarmonyMethod(finalizer));
                    patched++;
                }

                if (patched > 0)
                    SjgPlugin.Log.LogInfo("Guarded Bolt " + commandChannel.FullName + ".ReadInput against malformed packets.");
                else
                    SjgPlugin.Log.LogWarning("Bolt CommandChannel.ReadInput not found; malformed packets may still drop players.");
            }
            catch (Exception e)
            {
                SjgPlugin.Log.LogError("Failed to guard Bolt ReadInput: " + e);
            }
        }

        // --- offline-mode menu fixes (v1.1.9) ------------------------------------------------
        //
        // With no Steam session, Facepunch's SteamApps throws when the options menu asks for the
        // current beta branch. The exception escapes OptionsMenu.PopulateMenuItems -> RegisterButton,
        // so the menu's buttons never register (the panel is on screen but dead to input) and
        // OptionsButton.LateUpdate then throws an NRE every single frame (the console flood).
        // Returning an empty beta name keeps the menu building normally.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Steamworks.SteamApps), "get_CurrentBetaName")]
        private static bool CurrentBetaName_Prefix(ref string __result)
        {
            try
            {
                if (!Steamworks.SteamClient.IsValid)
                {
                    __result = "";
                    return false;
                }
            }
            catch (Exception e)
            {
                SjgPlugin.Log.LogWarning("SteamApps.CurrentBetaName guard failed: " + e.Message);
                __result = "";
                return false;
            }

            return true;
        }

        // Belt and braces: even if something else leaves an options button half-built, do not let it
        // spam a stack trace every frame (that is the flood seen in the command window).
        private static int _lateUpdateErrors;

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(OptionsButton), "LateUpdate")]
        private static Exception OptionsButton_LateUpdate_Finalizer(Exception __exception)
        {
            if (__exception == null)
                return null;

            if (_lateUpdateErrors++ < 5)
                SjgPlugin.Log.LogWarning("Suppressed OptionsButton.LateUpdate error: " + __exception.GetType().Name);

            return null;
        }
    }
}
