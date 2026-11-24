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
    /// This plugin lets the client reach the main menu, play offline (Practice), host a
    /// local bot match, browse/join servers from a self-hosted backend, and point the
    /// client at that backend.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    public class SjgPlugin : BaseUnityPlugin
    {
        public const string Guid = "games.gfi.sjg.patch";
        public const string Name = "Soviet Jump Game Patch";
        public const string Version = "1.1.1";

        internal static SjgPlugin Instance;
        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> OfflineMode;
        internal static ConfigEntry<bool> ForceDeveloper;
        internal static ConfigEntry<string> ApiBaseUrl;

        internal static ConfigEntry<bool> HostLocalMatches;
        internal static ConfigEntry<string> MatchMap;
        internal static ConfigEntry<int> BotCount;
        internal static ConfigEntry<string> HostHotkey;
        internal static ConfigEntry<int> HostPort;
        internal static ConfigEntry<bool> RegisterWithServer;

        internal static ConfigEntry<string> DirectConnectAddress;
        internal static ConfigEntry<string> ConnectHotkey;
        internal static ConfigEntry<string> ServerBrowserHotkey;
        internal static ConfigEntry<string> MatchmakingAction;

        /// <summary>
        /// The configured backend URL, normalised to something the game can use. A bare host
        /// (the default, "sjg.dnd.wtf") is assumed to be HTTPS; an explicit scheme is honoured
        /// so a local server can be given as e.g. http://127.0.0.1:8080/ .
        /// </summary>
        internal static string ApiBase
        {
            get
            {
                string value = ApiBaseUrl == null ? "" : ApiBaseUrl.Value.Trim();
                if (value.Length == 0)
                    return "";
                if (value.IndexOf("://", StringComparison.Ordinal) < 0)
                    value = "https://" + value;
                return value.TrimEnd('/');
            }
        }

        internal static bool HasApi => !string.IsNullOrEmpty(ApiBase);

        internal static string ApiUrl(string path)
        {
            return ApiBase + "/" + path.TrimStart(new[] { '/' });
        }

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            OfflineMode = Config.Bind("General", "OfflineMode", true,
                "Short-circuit the dead Steam/PlayFab/backend authentication and run a local offline session.");
            ForceDeveloper = Config.Bind("General", "ForceDeveloper", true,
                "Force ClientPermission.IsDeveloper = true (unlocks the in-game dev console and debug tools).");
            ApiBaseUrl = Config.Bind("Server", "ApiBaseUrl", "sjg.dnd.wtf",
                "Base URL of the Soviet Jump Game backend. A bare host is assumed to be HTTPS "
                + "(sjg.dnd.wtf -> https://sjg.dnd.wtf/); include a scheme for a local server, "
                + "e.g. http://127.0.0.1:8080/ . Enables the server browser and matchmaking.");

            HostLocalMatches = Config.Bind("LocalMatch", "HostLocalMatches", true,
                "When you pick MATCHMAKING, host a local server with bots instead of contacting the dead services.");
            MatchMap = Config.Bind("LocalMatch", "MatchMap", "Soviet Jump Game Map A",
                "Map to host locally: 'Soviet Jump Game Map A' / 'Soviet Jump Game Map B' / 'Soviet Jump Game Map Moon' / 'Soviet Jump Game Tutorial'.");
            BotCount = Config.Bind("LocalMatch", "BotCount", 20,
                "Number of bots a locally-hosted server spawns.");
            HostPort = Config.Bind("LocalMatch", "HostPort", 42010,
                "UDP port a locally-hosted match listens on.");
            HostHotkey = Config.Bind("LocalMatch", "HostHotkey", "F8",
                "Hotkey to host a local bot match from anywhere (empty to disable).");
            RegisterWithServer = Config.Bind("LocalMatch", "RegisterWithServer", true,
                "Advertise a locally-hosted match to the backend so it shows up in server browsers.");

            DirectConnectAddress = Config.Bind("Server", "DirectConnectAddress", "",
                "Simple fallback: a fixed server to join as host:port (e.g. 192.168.1.50:42010).");
            ConnectHotkey = Config.Bind("Server", "ConnectHotkey", "F6",
                "Hotkey to join Server.DirectConnectAddress (empty to disable).");
            ServerBrowserHotkey = Config.Bind("Server", "ServerBrowserHotkey", "F9",
                "Hotkey to log the server list (requires Server.ApiBaseUrl). Empty to disable.");
            MatchmakingAction = Config.Bind("Server", "MatchmakingAction", "Host",
                "What the MATCHMAKING button does: 'Host' (local bot match), 'Connect' (join Server.DirectConnectAddress), or 'Vanilla'.");

            var harmony = new Harmony(Guid);
            harmony.PatchAll(typeof(Patches));
            Log.LogInfo(Name + " " + Version + " loaded (OfflineMode=" + OfflineMode.Value + ").");

            StartCoroutine(Bootstrap());
        }

        private void Update()
        {
            HandleHotkey(HostHotkey, delegate { LocalMatch.Host(); });
            HandleHotkey(ConnectHotkey, delegate { LocalMatch.JoinConfigured(); });
            HandleHotkey(ServerBrowserHotkey, delegate
            {
                ServerBrowser.Refresh();
                foreach (string line in ServerBrowser.Describe())
                    Log.LogInfo("[servers] " + line);
            });
        }

        private void HandleHotkey(ConfigEntry<string> entry, Action action)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Value))
                return;

            try
            {
                KeyCode key;
                if (Enum.TryParse(entry.Value, true, out key) && Input.GetKeyDown(key))
                    action();
            }
            catch (Exception e)
            {
                Log.LogError("Invalid hotkey '" + entry.Value + "': " + e.Message);
                entry.Value = "";
            }
        }

        internal void OnHosted(ushort port, string map)
        {
            if (!HasApi || !RegisterWithServer.Value)
                return;
            HeartbeatBehaviour.Start(port, map);
        }

        private IEnumerator Bootstrap()
        {
            if (HasApi)
                Patches.RedirectApi(ApiBase);

            while (UnityEngine.Object.FindObjectOfType<PlayFabManager>() == null)
                yield return null;

            if (OfflineMode.Value)
            {
                OfflineSession.Initialize();
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
