using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Discord.Commands;
using Impostor.Plugins.Discostor.Config;

namespace Impostor.Plugins.Discostor.Discord.Commands
{
    [Remarks(":question:")]
    [Name("help")]
    [Summary("Show help")]
    public class HelpCommand : ModuleBase<SocketCommandContext>
    {
        private readonly ILogger<Discostor> _logger;
        private readonly DiscordBotConfig _config;
        private readonly CommandService _comms;
        private readonly HelpEmbedBuilder _builder;

        public HelpCommand(
                ILogger<Discostor> logger,
                ConfigRoot config,
                CommandService comms,
                HelpEmbedBuilder builder)
        {
            _logger = logger;
            _config = config.Discord;
            _comms = comms;
            _builder = builder;
        }

        [Command("help")]
        [Name("help")]
        [Alias("h")]
        [Summary("Show help message")]
        [Remarks("(command)")]
        public async Task HelpAsync([Remainder] string cmd=null)
        {
            await ReplyAsync(embed:_builder.Build(cmd));
        }
    }
}
