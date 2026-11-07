using System.Collections.Generic;
using QFSW.QC;

namespace SjgPatch
{
    /// <summary>
    /// In-game "server browser" and control commands, exposed through the game's Quantum Console
    /// (open it in-game, then e.g. `sjg-refresh` and `sjg-servers`).
    /// </summary>
    public static class SjgCommands
    {
        [Command("sjg-refresh", "Refresh the server list from the configured backend.")]
        public static string Refresh()
        {
            ServerBrowser.Refresh();
            return "Refreshing server list from " + SjgPlugin.ApiUrl("servers");
        }

        [Command("sjg-servers", "List servers from the configured backend.")]
        public static IEnumerable<string> Servers()
        {
            return ServerBrowser.Describe();
        }

        [Command("sjg-join", "Join a server by its index from sjg-servers.")]
        public static string Join(int index)
        {
            ServerBrowser.JoinIndex(index);
            return "Joining server #" + index;
        }

        [Command("sjg-join-address", "Join a server directly, e.g. sjg-join-address 192.168.1.50:42010")]
        public static string JoinAddress(string address)
        {
            int colon = address == null ? -1 : address.LastIndexOf(':');
            if (colon <= 0)
                return "Expected host:port";
            int port;
            if (!int.TryParse(address.Substring(colon + 1), out port))
                return "Bad port";
            LocalMatch.JoinAddress(address.Substring(0, colon), port);
            return "Connecting to " + address;
        }

        [Command("sjg-connect", "Join the server from Server.DirectConnectAddress.")]
        public static string Connect()
        {
            LocalMatch.JoinConfigured();
            return "Connecting to " + SjgPlugin.DirectConnectAddress.Value;
        }

        [Command("sjg-host", "Host a local bot match.")]
        public static string Host()
        {
            LocalMatch.Host();
            return "Hosting a local match on port " + SjgPlugin.HostPort.Value;
        }
    }
}
