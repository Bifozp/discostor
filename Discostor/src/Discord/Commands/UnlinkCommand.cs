using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Discord.Commands;
using Discord.WebSocket;
using Impostor.Plugins.Discostor.Config;
using Impostor.Plugins.Discostor.Mute;

namespace Impostor.Plugins.Discostor.Discord.Commands
{
    [Name("unlink")]
    [Remarks(":no_pedestrians:")]
    [Summary("unlink user")]
    public class UnlinkCommand : ModuleBase<SocketCommandContext>
    {
        private readonly ILogger<Discostor> _logger;
        private readonly DiscordBotConfig _config;
        private readonly AutomuteService _automuteService;

        public UnlinkCommand(
                ILogger<Discostor> logger,
                ConfigRoot config,
                AutomuteService automuteService)
        {
            _logger = logger;
            _config = config.Discord;
            _automuteService = automuteService;
        }

        [Name("unlink")]
        [Command("unlink")]
        [Alias("u", "ul")]
        [Summary("unlink user")]
        [Remarks("<number>")]
        public async Task UnlinkAsync([Summary("User index")]int index)
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
            if(index <= 0)
            {
                await ReplyAsync("1以上の数値を入れてください");
                return;
            }

            // Search game
            var code = _automuteService.GetVCLinkedGameCode(user.VoiceChannel);
            if(code != null)
            {
                try
                {
                    if(!_automuteService.UnlinkUserAndPlayer(code, user, index-1))
                    {
                        _logger.LogInformation("unlink failed.");
                    }
                }
                catch(Exception e)
                {
                    _logger.LogError($"{e.GetType()} -- {e.Message}");
                    using(_logger.BeginScope("err"))
                    {
                        _logger.LogError(e.StackTrace);
                    }
                }
                return;
            }
            // TODO
            await ReplyAsync($":speaker: \"{user.VoiceChannel.Name}\" に対応するゲームが見つかりません");
        }
    }
}
