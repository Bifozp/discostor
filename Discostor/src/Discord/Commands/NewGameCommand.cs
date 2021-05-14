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
                await ReplyAsync("プライベートメッセージでは機能しません");
                return;
            }

            var user = Context.Message.Author as SocketGuildUser;
            if(user?.VoiceChannel == null)
            {
                // TODO
                await ReplyAsync($"{Context.User.Mention} このサーバーのボイスチャンネルに参加してください");
                return;
            }
            if(user.VoiceChannel.Guild.Id != Context.Guild.Id)
            {
                // TODO
                // Remarks: 通常このルートに来ることはない
                await ReplyAsync("参加中のボイスチャットとテキストチャットのサーバーが異なります");
                return;
            }
            if(_automuteService.VCLinkedGames.TryGetValue(user.VoiceChannel.Id, out var c))
            {
                // TODO
                await ReplyAsync($":speaker: \"{user.VoiceChannel.Name}\" は、既にGame `{c}` とリンクしています");
                return;
            }

            // Create new room
            if(string.IsNullOrEmpty(code))
            {
            // Remarks: Commented out because there is no API to destroy the game.
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
                // TODO
                await ReplyAsync("ゲームコードを入力してください");
                return;
#endif
            }
            // Create
            await _automuteService.CreateMuteController(Context.Message, code);
        }
    }
}
