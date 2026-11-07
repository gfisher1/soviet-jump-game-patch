using System;
using System.Collections;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SjgPatch
{
    /// <summary>
    /// Soviet Jump Game - client patch.
    ///
    /// The game's live services (Steam-auth -&gt; PlayFab -&gt; api.fantasticpassion.games)
    /// are shut down, so the client hangs forever on the authentication screen.
    /// This plugin lets the client reach the main menu (and Practice / the local
    /// headless server) by short-circuiting the dead auth gate, and lets it be
    /// pointed at a self-hosted server instead of the dead backends.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    public class SjgPlugin : BaseUnityPlugin
    {
        public const string Guid = "games.gfi.sjg.patch";
        public const string Name = "Soviet Jump Game Patch";
        public const string Version = "0.1.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> OfflineMode;
        internal static ConfigEntry<bool> ForceDeveloper;
        internal static ConfigEntry<string> ApiBaseUrl;

        private void Awake()
        {
            Log = Logger;
            OfflineMode = Config.Bind("General", "OfflineMode", true,
                "Short-circuit the dead Steam/PlayFab/backend authentication and run a local offline session.");
            ForceDeveloper = Config.Bind("General", "ForceDeveloper", true,
                "Force ClientPermission.IsDeveloper = true (unlocks the in-game dev console and debug tools).");
            ApiBaseUrl = Config.Bind("Server", "ApiBaseUrl", "",
                "Base URL of a self-hosted SJG server, e.g. http://127.0.0.1:8080/ . Leave empty for pure offline mode.");

            var harmony = new Harmony(Guid);
            harmony.PatchAll(typeof(Patches));
            Log.LogInfo(Name + " " + Version + " loaded (OfflineMode=" + OfflineMode.Value + ").");

            StartCoroutine(Bootstrap());
        }

        private IEnumerator Bootstrap()
        {
            if (!string.IsNullOrEmpty(ApiBaseUrl.Value))
                Patches.RedirectApi(ApiBaseUrl.Value);

            // Wait for the real PlayFabManager (do NOT touch the Instance getter before
            // this, because it auto-creates a duplicate GameObject).
            while (UnityEngine.Object.FindObjectOfType<PlayFabManager>() == null)
                yield return null;

            if (OfflineMode.Value)
            {
                OfflineSession.Initialize();
                // Keep the fake session alive in case something resets it.
                while (true)
                {
                    yield return new WaitForSeconds(1f);
                    if (UnityEngine.Object.FindObjectOfType<PlayFabManager>() != null
                        && PlayFabManager.Instance.CurrentPlayer == null)
                    {
                        OfflineSession.Initialize();
                    }
                }
            }
        }
    }
}
