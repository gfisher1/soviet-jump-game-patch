using System;
using System.Collections.Generic;
using System.Reflection;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;
using EPB.Networking.PlayFab;

namespace SjgPatch
{
    /// <summary>
    /// Builds a synthetic local PlayFab session so the game behaves as if it logged in.
    /// No network access is required.
    /// </summary>
    internal static class OfflineSession
    {
        internal const string FakePlayFabId = "0FF1CE0000000001";
        internal const string FakeTicket = "OFFLINE-SESSION-TICKET";

        private static FieldInfo _lastLoginResultField;
        private static ConstructorInfo _accountCtor;

        internal static void Initialize()
        {
            try
            {
                PlayFabManager mgr = PlayFabManager.Instance;
                if (mgr == null)
                {
                    SjgPlugin.Log.LogWarning("PlayFabManager.Instance was null.");
                    return;
                }

                Dictionary<string, string> titleData = BuildTitleData();
                mgr.TitleData = new EPB.Networking.TitleData(titleData);

                LoginResult login = BuildLoginResult(titleData);
                PlayFabAccount account = BuildAccount(login);
                mgr.CurrentPlayer = account;

                ForceUpdatePlayFab.Initialized = true;

                SjgPlugin.Log.LogInfo("Offline session ready (fake account " + FakePlayFabId + ").");
            }
            catch (Exception e)
            {
                SjgPlugin.Log.LogError("Offline session setup failed: " + e);
            }
        }

        private static Dictionary<string, string> BuildTitleData()
        {
            var d = new Dictionary<string, string>();
            d["ApplicationVersion"] = Application.version;
            d["BuildVersion"] = "2017-04-08";
            d["MapIndex"] = "0";
            d["MapNames"] = "[\"Soviet Jump Game Map A\",\"Soviet Jump Game Map B\",\"Soviet Jump Game Map Moon\",\"Soviet Jump Game Tutorial\"]";
            d["MatchSizes"] = "[0,20,30,40]";
            d["XP"] = "[0,100,2650,3900,4750,5600,6350,7100,7850,8150,8600,9050,9500,9950,10400,10850,11300,11750,12200,12650,13100,13450,13800,14150,14500,18000]";
            d["BotsEnabled"] = "true";
            d["BotCounts"] = "[0,0,3,7,10,12,15,5,0]";
            d["MaximumMatchLength"] = "900";
            d["MaximumServerLifetime"] = "1200";
            d["BotsEnabledOnServer"] = "true";
            return d;
        }

        private static LoginResult BuildLoginResult(Dictionary<string, string> titleData)
        {
            var login = new LoginResult();
            login.PlayFabId = FakePlayFabId;
            login.SessionTicket = FakeTicket;
            login.NewlyCreated = false;

            login.EntityToken = new EntityTokenResponse
            {
                Entity = new PlayFab.ClientModels.EntityKey { Id = FakePlayFabId, Type = "title_player_account" },
                EntityToken = "OFFLINE-ENTITY-TOKEN",
                TokenExpiration = DateTime.UtcNow.AddYears(10)
            };

            login.AuthenticationContext = new PlayFabAuthenticationContext(FakeTicket, "OFFLINE-ENTITY-TOKEN", FakePlayFabId, FakePlayFabId, "title_player_account");

            login.InfoResultPayload = new GetPlayerCombinedInfoResultPayload
            {
                TitleData = titleData,
                UserVirtualCurrency = new Dictionary<string, int>(),
                UserInventory = new List<ItemInstance>(),
                UserData = new Dictionary<string, UserDataRecord>(),
                UserReadOnlyData = new Dictionary<string, UserDataRecord>(),
                PlayerStatistics = new List<StatisticValue>()
            };

            return login;
        }

        private static PlayFabAccount BuildAccount(LoginResult login)
        {
            Type type = typeof(PlayFabAccount);

            if (_accountCtor == null)
            {
                _accountCtor = type.GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null);
            }

            if (_lastLoginResultField == null)
            {
                _lastLoginResultField = type.GetField(
                    "_lastLoginResult", BindingFlags.Instance | BindingFlags.NonPublic);
            }

            var account = (PlayFabAccount)_accountCtor.Invoke(null);
            _lastLoginResultField.SetValue(account, login);

            account.EntityKey = login.EntityToken.Entity;
            account.AuthenticationEntityKey = new PlayFab.MultiplayerModels.EntityKey
            {
                Id = login.AuthenticationContext.EntityId,
                Type = login.AuthenticationContext.EntityType
            };

            return account;
        }
    }
}
