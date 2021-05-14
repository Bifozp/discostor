using Impostor.Api.Events;
using Impostor.Api.Plugins;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Impostor.Plugins.Discostor.Config;
using Impostor.Plugins.Discostor.Discord;
using Impostor.Plugins.Discostor.Mute;
using Impostor.Plugins.Discostor.Handlers;

namespace Impostor.Plugins.Discostor
{
    public class DiscostorStartup : IPluginStartup
    {
        public void ConfigureHost(IHostBuilder host)
        {
        }

        public void ConfigureServices(IServiceCollection services)
        {
            // Config
            services.AddSingleton<ConfigRoot>( cfg => ConfigRoot.Build("discostor.json") );

            // Discord bot
            services.AddSingleton<DiscordSocketClient>();
            services.AddSingleton<CommandService>();
            services.AddSingleton<AutomuteService>();
            services.AddTransient<HelpEmbedBuilder>();
            services.AddHostedService<Bot>();

            // EventListeners
            services.AddSingleton<IEventListener, GameEventListener>();
            services.AddSingleton<IEventListener, PlayerEventListener>();
            services.AddSingleton<IEventListener, MeetingEventListener>();
        }
    }
}
