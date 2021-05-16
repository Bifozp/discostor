using System;
using Microsoft.Extensions.DependencyInjection;
using Discord;
using Discord.WebSocket;
using Impostor.Plugins.Discostor.Config;

namespace Impostor.Plugins.Discostor.Discord
{
    internal class ClientBuilder
    {
        internal static DiscordSocketClient Build(IServiceProvider provider)
        {
            var config = provider.GetService<ConfigRoot>().Discord;

            var socketConfig = new DiscordSocketConfig();
            socketConfig.MessageCacheSize = config.MessageCacheSize;
            socketConfig.RateLimitPrecision = RateLimitPrecision.Millisecond;

            var client = new DiscordSocketClient(socketConfig);
            return client;
        }
    }
}
