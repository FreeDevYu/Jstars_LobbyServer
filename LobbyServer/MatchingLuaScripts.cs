using System.Reflection;
using StackExchange.Redis;

namespace LobbyServer
{
    internal static class MatchingLuaScripts
    {
        public const string MatchPlayersResourceSuffix = "MatchPlayers.lua";

        public static readonly LuaScript MatchPlayers = LoadEmbeddedScript(MatchPlayersResourceSuffix);

        private static LuaScript LoadEmbeddedScript(string resourceSuffix)
        {
            Assembly assembly = typeof(MatchingLuaScripts).Assembly;
            string? resourceName = assembly.GetManifestResourceNames()
                .SingleOrDefault(name => name.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
            {
                throw new InvalidOperationException(
                    $"Embedded Lua script not found (suffix: {resourceSuffix}). " +
                    "Ensure Lua/MatchPlayers.lua is included as EmbeddedResource.");
            }

            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                throw new InvalidOperationException($"Failed to open embedded resource: {resourceName}");
            }

            using var reader = new StreamReader(stream);
            return LuaScript.Prepare(reader.ReadToEnd());
        }
    }
}
