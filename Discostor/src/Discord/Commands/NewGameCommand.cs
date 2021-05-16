using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Discord.Commands;
using Discord.WebSocket;
using Impostor.Api.Games.Managers;
using Impostor.Plugins.Discostor.Config;
using Impostor.Plugins.Discostor.Mute;

namespace Impostor.Plugins.Discostor.Discord.Commands
{
    [Name("new")]
    [Remarks(":video_game:")]
    [Summary("Create new game")]
    public class NewGameCommand : ModuleBase<SocketCommandContext>
    {
        private readonly ILogger<Discostor> _logger;
        private readonly DiscordBotConfig _config;
        private readonly IGameManager _gameManager;
        private readonly AutomuteService _automuteService;

        public NewGameCommand(
                ILogger<Discostor> logger,
                ConfigRoot config,
                IGameManager gameManager,
                AutomuteService automuteService)
        {
            _logger = logger;
            _config = config.Discord;
            _gameManager = gameManager;
            _automuteService = automuteService;
        }

        [Name("new")]
        [Command("new")]
        [Alias("n", "ng")]
        [Summary("Create new game")]
        [Remarks("<GameCode>")]
        public async Task NewAsync([Summary("Game-code")] string code=null)
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
            if(user.VoiceChannel.Guild.Id != Context.Guild.Id)
            {
                // Remarks: Usually, the program never reaches this point.
                await ReplyAsync("The servers for voice chat and text chat are different.");
                return;
            }
            if(_automuteService.VCLinkedGames.TryGetValue(user.VoiceChannel.Id, out var gameCode))
            {
                await ReplyAsync($":speaker: \"{user.VoiceChannel.Name}\" is already paired with game `{gameCode}`.");
                return;
            }

            // Create new room
            if(string.IsNullOrEmpty(code))
            {
            // Remarks: Commented out because there is no API to destroy the game.
            // If this code is enabled, the game may continue to remain.
#if false
                game = await _gameManager.CreateAsync(new GameOptionsData());
                game.DisplayName = $"created by {user}";
                await game.SetPrivacyAsync(isPublic:true);
                _automuteService.Games[game.Code.Code] = game;
                var embedTask = ReplyAsync("TODO: embed");
                await Task.WhenAll(
                        embedTask,
                        user.SendMessageAsync($"Game created: {game.Code.Code}")
                );
                embedMsg = await embedTask as RestUserMessage;
#else
                await ReplyAsync("Enter the game code.");
                return;
#endif
            }
            // Create a mute controller to pair with the game.
            await _automuteService.CreateMuteController(Context.Message, code);
        }
    }
}
