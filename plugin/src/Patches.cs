using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

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

        // MATCHMAKING button -> host a local server with bots instead of the dead services.
        // Patch the public entry point (not Impl) so none of the matchmaking/cancel-token
        // machinery runs - this mirrors the PRACTICE button, which starts a server cleanly.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(EPB.Networking.MatchConnection), "ConnectToMatchmaking")]
        private static bool ConnectToMatchmaking_Prefix()
        {
            if (!SjgPlugin.HostLocalMatches.Value)
                return true;

            LocalMatch.Host();
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

        // RoundManager maintains bots via GetTargetBotPlayerCount(), which maps the number of
        // *real* players through a curve (0 real players -> 0 bots). Playing solo therefore
        // spawns no bots and the gym countdown never starts (ActivePlayers stays 1). Pin the
        // target to our configured bot count so a solo host gets a full lobby.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(RoundManager), "GetTargetBotPlayerCount")]
        private static bool GetTargetBotPlayerCount_Prefix(ref int __result)
        {
            if (!SjgPlugin.HostLocalMatches.Value)
                return true;

            __result = Math.Max(0, SjgPlugin.BotCount.Value);
            return false;
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
    }
}
