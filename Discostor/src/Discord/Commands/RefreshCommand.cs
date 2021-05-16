using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Discord.Commands;
using Discord.WebSocket;
using Impostor.Api.Games.Managers;
using Impostor.Plugins.Discostor.Config;
using Impostor.Plugins.Discostor.Mute;

namespace Impostor.Plugins.Discostor.Discord.Commands
{
    [Name("refresh")]
    [Remarks(":arrows_counterclockwise:")]
    [Summary("refresh embed code")]
    public class RefreshCommand : ModuleBase<SocketCommandContext>
    {
        private readonly ILogger<Discostor> _logger;
        private readonly DiscordBotConfig _config;
        private readonly CommandService _comms;
        private readonly IGameManager _gameManager;
        private readonly AutomuteService _automuteService;

        public RefreshCommand(
                ILogger<Discostor> logger,
                ConfigRoot config,
                CommandService comms,
                IGameManager gameManager,
                AutomuteService automuteService)
        {
            _logger = logger;
            _config = config.Discord;
            _comms = comms;
            _gameManager = gameManager;
            _automuteService = automuteService;
        }

        [Name("refresh")]
        [Command("refresh")]
        [Alias("r", "re")]
        [Summary("refresh embed code")]
        [Remarks("-")]
        public async Task RefreshAsync()
        {
            if(Context.IsPrivate)
            {
                await ReplyAsync("Cannot be used with DM.");
                return;
            }

            var user = Context.Message.Author as SocketGuildUser;
            if(user?.VoiceChannel == null)
            {
                await ReplyAsync($"{Context.User.Mention} Please join the voice chat on this server.");
                return;
            }

            // Search game
            var code = _automuteService.GetVCLinkedGameCode(user.VoiceChannel);
            if(code != null)
            {
                try
                {
                    if(!_automuteService.RefreshEmbed(code))
                    {
                        _logger.LogInformation("refresh failed.");
                    }
                }
                catch(Exception e)
                {
                    _logger.LogCritical(e.Message);
                    using(_logger.BeginScope("err"))
                    {
                        _logger.LogCritical(e.StackTrace);
                    }
                }
                return;
            }
            await ReplyAsync($":speaker: \"{user.VoiceChannel.Name}\" > There are no games to pair with.");
        }
    }
}
