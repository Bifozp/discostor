using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Discord.Commands;
using Discord.WebSocket;
using Impostor.Plugins.Discostor.Config;
using Impostor.Plugins.Discostor.Mute;

namespace Impostor.Plugins.Discostor.Discord.Commands
{
    [Name("end")]
    [Remarks(":stop_sign:")]
    [Summary("End game")]
    public class EndGameCommand : ModuleBase<SocketCommandContext>
    {
        private readonly ILogger<Discostor> _logger;
        private readonly DiscordBotConfig _config;
        private readonly AutomuteService _automuteService;

        public EndGameCommand(
                ILogger<Discostor> logger,
                ConfigRoot config,
                AutomuteService automuteService)
        {
            _logger = logger;
            _config = config.Discord;
            _automuteService = automuteService;
        }

        [Name("end")]
        [Command("end")]
        [Alias("e", "eg")]
        [Summary("End game")]
        [Remarks("-")]
        public async Task EndAsync()
        {
            if(Context.IsPrivate)
            {
                await ReplyAsync("プライベートメッセージでは機能しません");
                return;
            }

            var user = Context.Message.Author as SocketGuildUser;
            if(user?.VoiceChannel == null)
            {
                await ReplyAsync($"{Context.User.Mention} ボイスチャンネルに参加してください");
                return;
            }

            // Search game
            if(_automuteService.VCLinkedGames.TryGetValue(user.VoiceChannel.Id, out var code))
            {
                _logger.LogDebug("found linked game");
                try
                {
                    _automuteService.DestroyMuteController(code, user);
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
            // TODO
            await ReplyAsync($":speaker: \"{user.VoiceChannel.Name}\" に対応するゲームが見つかりません");
        }
    }
}
