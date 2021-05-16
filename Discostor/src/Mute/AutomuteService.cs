using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Discord;
using Discord.WebSocket;
using Impostor.Api.Games;
using Impostor.Api.Net;
using Impostor.Plugins.Discostor.Config;
using Impostor.Plugins.Discostor.Discord;

namespace Impostor.Plugins.Discostor.Mute
{
    public class AutomuteService
    {
        private readonly ILogger<Discostor> _logger;
        private readonly DiscordBotConfig _config;
        private readonly EmoteManager _emoteManager;
        private Dictionary<string, MuteController> _controllers =
            new Dictionary<string, MuteController>();

        internal Dictionary<ulong, string> VCLinkedGames { get; set; } =
            new Dictionary<ulong, string>();
        internal Dictionary<string, IGame> Games { get; set; } =
            new Dictionary<string, IGame>();

        public AutomuteService(
                ILogger<Discostor> logger,
                ConfigRoot config,
                EmoteManager emoteManager)
        {
            _logger = logger;
            _config = config.Discord;
            _emoteManager = emoteManager;
        }

        internal void AddGuildUser(IGuildUser user, SocketVoiceChannel vc)
        {
            if(null == vc) return;
            if(VCLinkedGames.TryGetValue(vc.Id, out var code))
            {
                if(_controllers.TryGetValue(code, out var muteController))
                {
                    muteController.JoinToVC(user);
                    return;
                }
                _logger.LogError($"There is no mute controller to pair with game `{code}`.");
                return;
            }
            _logger.LogDebug($"It isn't connected to any game. : VC \"{vc.Name}\"");
            return;
        }

        internal void RemoveGuildUser(IGuildUser user, SocketVoiceChannel vc)
        {
            if(null == vc) return;
            if(VCLinkedGames.TryGetValue(vc.Id, out var code))
            {
                if(_controllers.TryGetValue(code, out var muteController))
                {
                    muteController.LeaveFromVC(user);
                    return;
                }
                _logger.LogError($"There is no mute controller to pair with game `{code}`.");
                return;
            }
            _logger.LogDebug($"It isn't connected to any game. : VC \"{vc.Name}\"");
            return;
        }

        internal void AddGamePlayer(IClientPlayer player)
        {
            if(_controllers.Count == 0) return;
            if(_controllers.TryGetValue(player.Game.Code.Code, out var muteController))
            {
                muteController.JoinToGame(player);
                return;
            }
            _logger.LogError($"There is no mute controller to pair with game `{player.Game.Code.Code}`.");
            return;
        }

        internal void RemoveGamePlayer(IClientPlayer player)
        {
            if(_controllers.TryGetValue(player.Game.Code.Code, out var muteController))
            {
                muteController.LeaveFromGame(player);
                return;
            }
            _logger.LogError($"There is no mute controller to pair with game `{player.Game.Code.Code}`.");
            return;
        }

        internal async Task CreateMuteController(SocketMessage from, string code)
        {
            // Find the game
            var upperCode = code.ToUpper();
            IGame game;
            if(!Games.TryGetValue(upperCode, out game))
            {
                await from.Channel.SendMessageAsync($"There is no game with the game code `{upperCode}`.");
                return;
            }

            // Find a mute controller to pair with the game.
            MuteController existsController;
            if(!_controllers.TryGetValue(game.Code.Code, out existsController))
            {   // not found, create the controller.
                var user = from.Author as SocketGuildUser;
                if(VCLinkedGames.TryGetValue(user.VoiceChannel.Id, out var LinkRemainedMC))
                {
                    // If the program is not correct, it will arrive here.
                    _logger.LogWarning($"Discard the remaining links in game `{game.Code.Code}` and create a new mute controller.");
                    _logger.LogWarning($"A memory leak may have occurred.");
                }
                try{
                    var controller = new MuteController(_logger, game, user, _config, _emoteManager);
                    _controllers[game.Code.Code] = controller;
                    VCLinkedGames[user.VoiceChannel.Id] = game.Code.Code;
                    await controller.GenerateEmbedMsg(from.Channel);
                }
                catch(Exception e)
                {
                    _logger.LogError($"{e.GetType()} -- {e.Message}");
                    _logger.LogError($"{e.StackTrace}");
                }
                return;
            }
            await from.Channel.SendMessageAsync($"Game `{upperCode}` is already linked to :speaker: \"{existsController.VoiceChannel}\".");
            return;
        }

        internal bool DestroyMuteController(string code, SocketGuildUser user=null)
        {
            if(_controllers.TryGetValue(code, out var controller))
            {
                controller.RestoreMuteControl(0);
                var ch = controller.Channel;
                var destroyer = (user == null) ? "Server" : user.Mention;
                var content = $"The link to game `{code}` was destroyed by {destroyer}.";
                Task.WaitAll(ch.SendMessageAsync(content), controller.DeleteEmbedAsync());

                // Delete mute-controller object
                VCLinkedGames.Remove(controller.VoiceChannel.Id);
                _controllers.Remove(code);
                return true;
            }
            return false;

        }

        internal bool LinkUserAndPlayer(string code, SocketGuildUser user, int index)
        {
            if(_controllers.TryGetValue(code, out var controller))
            {
                return controller.LinkUserAndPlayer(user, index);
            }
            return false;
        }

        internal bool UnlinkUserAndPlayer(string code, SocketGuildUser user, int index)
        {
            if(_controllers.TryGetValue(code, out var controller))
            {
                return controller.UnlinkUserAndPlayer(user, index);
            }
            return false;
        }

        internal async Task OnReactionAdded(IUserMessage msg, IGuildUser user, IReaction reaction, string code)
        {
            if(_controllers.TryGetValue(code, out var controller))
            {
                if(controller.ToggleLink(user, msg, reaction.Emote))
                {
                    // succeeded.
                    await msg.RemoveReactionAsync(reaction.Emote, user);
                }
            }
            return;
        }

        internal bool RefreshEmbed(string code)
        {
            if(_controllers.TryGetValue(code, out var controller))
            {
                Task.WhenAll(controller.RefreshEmbedAsync());
                return true;
            }
            return false;
        }

        internal void OnPlayerSpawned(IClientPlayer player)
        {
            if(_controllers.TryGetValue(player.Game.Code.Code, out var muteController))
            {
                // muteController.SyncDeadStatus(); // After the game is over, "player.Character" will be null, so it is not called here.
                muteController.PlayerSpawned();
                return;
            }
            return;
        }

        internal void OnGameStarted(IGame game)
        {
            var controller = _controllers[game.Code.Code];
            controller.SyncDeadStatus();
            controller.TaskMuteControl(_config.MuteDelays.FromLobby.ToTask);
        }

        internal void OnMeetingStarted(IGame game)
        {
            var controller = _controllers[game.Code.Code];
            controller.SyncDeadStatus();
            controller.MeetingMuteControl(_config.MuteDelays.FromTask.ToMeeting);
        }

        internal void OnMeetingEnded(IGame game)
        {
            var controller = _controllers[game.Code.Code];
            controller.SyncDeadStatus();
            controller.TaskMuteControl(_config.MuteDelays.FromMeeting.ToTask);
        }

        internal void OnGameEnded(IGame game)
        {
            var controller = _controllers[game.Code.Code];
            controller.SyncDeadStatus();
            controller.RestoreMuteControl(_config.MuteDelays.FromTask.ToLobby);
        }

        internal string GetVCLinkedGameCode(IVoiceChannel vc)
        {
            if(vc is null) return null;
            if(VCLinkedGames.TryGetValue(vc.Id, out var code))
            {
                _logger.LogDebug($"Game found. vc({vc.Name}) => game[{code}]");
                return code;
            }
            return null;
        }
    }
}
