using System;
using System.Collections;
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

        private static FieldInfo _apiBaseField;

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
