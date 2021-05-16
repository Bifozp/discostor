using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Discord.Commands;
using Discord.WebSocket;
using Impostor.Plugins.Discostor.Config;
using Impostor.Plugins.Discostor.Mute;

namespace Impostor.Plugins.Discostor.Discord.Commands
{
    [Name("link")]
    [Remarks(":link:")]
    [Summary("link user")]
    public class LinkCommand : ModuleBase<SocketCommandContext>
    {
        private readonly ILogger<Discostor> _logger;
        private readonly DiscordBotConfig _config;
        private readonly AutomuteService _automuteService;

        public LinkCommand(
                ILogger<Discostor> logger,
                ConfigRoot config,
                AutomuteService automuteService)
        {
            _logger = logger;
            _config = config.Discord;
            _automuteService = automuteService;
        }

        [Name("link")]
        [Command("link")]
        [Alias("l")]
        [Summary("link user")]
        [Remarks("<number>")]
        public async Task LinkAsync([Summary("User index")]int index)
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
            if(index <= 0)
            {
                await ReplyAsync("Enter a value greater than or equal to `1`.");
                return;
            }

            // Search game
            var code = _automuteService.GetVCLinkedGameCode(user.VoiceChannel);
            if(code != null)
            {
                try
                {
                    if(!_automuteService.LinkUserAndPlayer(code, user, index-1))
                    {
                        _logger.LogInformation("link failed.");
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
            await ReplyAsync($":speaker: \"{user.VoiceChannel.Name}\" > There are no games to pair with.");
        }
    }
}
