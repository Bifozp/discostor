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
            services.AddSingleton<ConfigRoot>(_ => new ConfigBuilder<ConfigRoot>().Build("discostor.json") );

            // Emote config
            services.AddSingleton<EmoteConfig>(_ => new ConfigBuilder<EmoteConfig>().Build("discostor-emotes.json") );
            services.AddSingleton<EmoteManager>();

            // Discord bot services
            services.AddSingleton<DiscordSocketClient>(x => ClientBuilder.Build(x));
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
