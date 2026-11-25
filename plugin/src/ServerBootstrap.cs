using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Bolt;
using UdpKit;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SjgPatch
{
    /// <summary>
    /// Server-side bootstrap for the headless game server.
    ///
    /// The retail headless server boots Bolt with <c>BoltLauncher.StartServer(-1)</c> and never
    /// loads a map scene (that only happens once a Bolt session has been created). The game's
    /// <c>ServerCallbacks</c> - the one and only <see cref="GlobalEventListener"/> that answers
    /// <c>ConnectRequest</c> with <c>BoltNetwork.Accept</c> - is decorated
    /// <c>[BoltGlobalBehaviour(Server, map scenes)]</c>, so Bolt only instantiates it while one
    /// of those map scenes is active. With no map scene there is nobody to accept the client's
    /// ConnectRequest, so the client sits in "connecting" until it throws a TimeoutException.
    ///
    /// This component watches the Bolt server, makes sure a <c>ServerCallbacks</c> instance is
    /// alive (creating one on a persistent object if Bolt has not), and logs the whole
    /// handshake so the behaviour is visible in BepInEx's LogOutput.log.
    /// </summary>
    public class ServerBootstrapBehaviour : MonoBehaviour
    {
        private static ServerBootstrapBehaviour _instance;

        private GameObject _callbacksHost;
        private Component _callbacksComponent;
        private Type _callbacksType;
        private bool _warnedMissingType;
        private float _nextStateLog;
        private bool _forcedMapScene;
        private float _forceMapAfter;

        public static void Install()
        {
            if (_instance != null)
                return;

            GameObject host = new GameObject("SjgServerBootstrap");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<ServerBootstrapBehaviour>();
        }

        private void Awake()
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);

            // Attach the logging listener right away so we catch BoltStartDone and the session
            // callbacks, which fire shortly after the headless server calls BoltLauncher.StartServer.
            EnsureDiagnosticListener();

            StartCoroutine(Watch());
        }

        private IEnumerator Watch()
        {
            // Give Bolt time to finish StartServer before we start poking at it.
            yield return new WaitForSeconds(3f);

            while (true)
            {
                yield return new WaitForSeconds(2f);

                try
                {
                    if (!BoltNetwork.IsServer)
                        continue;

                    EnsureServerCallbacks();
                    ForceMapScene();
                    LogState();
                }
                catch (Exception e)
                {
                    SjgPlugin.Log.LogWarning("ServerBootstrap watch error: " + e.Message);
                }
            }
        }

        /// <summary>
        /// The headless server is supposed to load the map scene when its Bolt session is
        /// created (BoltCore.AutoLoadScene on SESSION_CREATION). If that never happens the
        /// server is left in its boot scene with no map, no bots and no scene for the client to
        /// sync to. Load the map ourselves after a grace period so the match becomes playable.
        /// </summary>
        private void ForceMapScene()
        {
            if (_forcedMapScene)
                return;

            string map = SjgPlugin.MatchMap == null ? null : SjgPlugin.MatchMap.Value;
            if (string.IsNullOrEmpty(map))
                return;

            string active = "";
            try { active = SceneManager.GetActiveScene().name; } catch { }

            if (active == map)
            {
                _forcedMapScene = true;
                SjgPlugin.Log.LogInfo("[server] the game loaded the map scene itself: " + map);
                return;
            }

            if (Time.realtimeSinceStartup < _forceMapAfter)
                return;

            if (_forceMapAfter == 0f)
            {
                _forceMapAfter = Time.realtimeSinceStartup + 8f;
                return;
            }

            _forcedMapScene = true;
            SjgPlugin.Log.LogWarning("[server] no session/map after the grace period; loading '"
                + map + "' directly (active scene was '" + active + "').");
            try
            {
                BoltNetwork.LoadScene(map);
            }
            catch (Exception e)
            {
                SjgPlugin.Log.LogError("[server] BoltNetwork.LoadScene failed: " + e);
            }
        }

        /// <summary>Attach our logging listener so the connection handshake shows up in the log.</summary>
        private void EnsureDiagnosticListener()
        {
            try
            {
                if (FindObjectsOfType<SjgServerListener>().Length == 0)
                {
                    GameObject go = new GameObject("SjgServerListener");
                    DontDestroyOnLoad(go);
                    go.AddComponent<SjgServerListener>();
                    SjgPlugin.Log.LogInfo("[server] diagnostic listener attached.");
                }
            }
            catch (Exception e)
            {
                SjgPlugin.Log.LogWarning("[server] could not attach diagnostic listener: " + e.Message);
            }
        }

        /// <summary>
        /// Make sure the game's ServerCallbacks is present. If Bolt has not created one (because
        /// no map scene is active) we create our own on a separate, persistent object. Bolt's
        /// own global-behaviour bookkeeping only ever touches its internal "BoltBehaviours"
        /// object, so our copy survives scene changes; if the game later creates its own we drop
        /// ours to avoid double-accepting connections.
        /// </summary>
        private void EnsureServerCallbacks()
        {
            if (_callbacksType == null)
            {
                _callbacksType = FindType("ServerCallbacks");
                if (_callbacksType == null)
                {
                    if (!_warnedMissingType)
                    {
                        _warnedMissingType = true;
                        SjgPlugin.Log.LogWarning("[server] could not find the game's ServerCallbacks type.");
                    }
                    return;
                }
            }

            int count = FindObjectsOfType(_callbacksType).Length;

            if (count == 0)
            {
                if (_callbacksHost == null)
                {
                    _callbacksHost = new GameObject("SjgServerCallbacks");
                    DontDestroyOnLoad(_callbacksHost);
                    _callbacksComponent = _callbacksHost.AddComponent(_callbacksType);
                    SjgPlugin.Log.LogInfo("[server] created a ServerCallbacks instance (Bolt had none).");
                }
                return;
            }

            // Someone (the game) has a real ServerCallbacks. Drop ours if it is a duplicate.
            if (_callbacksHost != null && count > 1)
            {
                SjgPlugin.Log.LogInfo("[server] the game created its own ServerCallbacks; removing ours.");
                Destroy(_callbacksHost);
                _callbacksHost = null;
                _callbacksComponent = null;
            }
        }

        private void LogState()
        {
            if (Time.realtimeSinceStartup < _nextStateLog)
                return;
            _nextStateLog = Time.realtimeSinceStartup + 15f;

            string scenes = "";
            try
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    scenes += (i == 0 ? "" : ", ") + SceneManager.GetSceneAt(i).name;
            }
            catch { }

            string active = "?";
            try { active = SceneManager.GetActiveScene().name; } catch { }

            int callbacks = 0;
            if (_callbacksType != null)
                callbacks = FindObjectsOfType(_callbacksType).Length;

            SjgPlugin.Log.LogInfo(string.Format(
                "[server] IsServer={0} activeScene='{1}' loadedScenes=[{2}] ServerCallbacks={3} connections={4}",
                BoltNetwork.IsServer, active, scenes, callbacks, CountConnections()));
        }

        private static int CountConnections()
        {
            try
            {
                int n = 0;
                foreach (BoltConnection c in BoltNetwork.Connections)
                    n++;
                return n;
            }
            catch
            {
                return -1;
            }
        }

        private static Type FindType(string simpleName)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type t = asm.GetType(simpleName, false);
                    if (t != null)
                        return t;
                }
                catch { }
            }
            return null;
        }
    }

    /// <summary>
    /// A global, always-present Bolt listener that only logs the server-side handshake.
    /// It never accepts or refuses - the game's own ServerCallbacks does that - so it is safe
    /// to keep around alongside it.
    /// </summary>
    public class SjgServerListener : GlobalEventListener
    {
        public override void BoltStartBegin()
        {
            SjgPlugin.Log.LogInfo("[server] BoltStartBegin");
        }

        public override void BoltStartDone()
        {
            SjgPlugin.Log.LogInfo("[server] BoltStartDone");
        }

        public override void BoltStartFailed()
        {
            SjgPlugin.Log.LogWarning("[server] BoltStartFailed");
        }

        public override void SessionCreatedOrUpdated(UdpSession session)
        {
            SjgPlugin.Log.LogInfo("[server] SessionCreatedOrUpdated: " + session);
        }

        public override void SessionCreationFailed(UdpSession session, UdpSessionError errorReason)
        {
            SjgPlugin.Log.LogWarning("[server] SessionCreationFailed: " + errorReason);
        }

        public override void ConnectRequest(UdpEndPoint endpoint, IProtocolToken token)
        {
            SjgPlugin.Log.LogInfo("[server] ConnectRequest from " + endpoint
                + " token=" + (token == null ? "<null>" : token.GetType().Name));
        }

        public override void ConnectAttempt(UdpEndPoint endpoint, IProtocolToken token)
        {
            SjgPlugin.Log.LogInfo("[server] ConnectAttempt from " + endpoint);
        }

        public override void ConnectFailed(UdpEndPoint endpoint, IProtocolToken token)
        {
            SjgPlugin.Log.LogWarning("[server] ConnectFailed from " + endpoint);
        }

        public override void Connected(BoltConnection connection)
        {
            SjgPlugin.Log.LogInfo("[server] Connected: " + connection.RemoteEndPoint);
        }

        public override void Disconnected(BoltConnection connection)
        {
            SjgPlugin.Log.LogInfo("[server] Disconnected: " + connection.RemoteEndPoint);
        }

        public override void SceneLoadLocalDone(string scene)
        {
            SjgPlugin.Log.LogInfo("[server] SceneLoadLocalDone: " + scene);
        }
    }
}
