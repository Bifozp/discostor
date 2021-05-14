using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Discord.WebSocket;
using Impostor.Api.Games;
using Impostor.Api.Net;
using Impostor.Plugins.Discostor.Config;

namespace Impostor.Plugins.Discostor.Mute
{
    public class AutomuteService
    {
        private readonly ILogger<Discostor> _logger;
        private readonly DiscordBotConfig _config;
        private Dictionary<string, MuteController> _controllers =
            new Dictionary<string, MuteController>();

        internal Dictionary<ulong, string> VCLinkedGames { get; set; } =
            new Dictionary<ulong, string>();
        internal Dictionary<string, IGame> Games { get; set; } =
            new Dictionary<string, IGame>();

        public AutomuteService(
                ILogger<Discostor> logger,
                ConfigRoot config)
        {
            _logger = logger;
            _config = config.Discord;
        }

        internal void AddGuildUser(SocketGuildUser user, SocketVoiceChannel vc)
        {
            if(null == vc) return;
            if(VCLinkedGames.TryGetValue(vc.Id, out var code))
            {
                if(_controllers.TryGetValue(code, out var muteController))
                {
                    muteController.JoinToVC(user);
                    return;
                }
                // TODO
                _logger.LogWarning($"Discordユーザー \"{user.Username}#{user.Discriminator}\" に対応するゲームのステータス情報が見つかりません.");
                return;
            }
            // TODO
            _logger.LogDebug($"どこのゲームとも接続されていません \"{vc.Name}\".");
            return;
        }

        internal void RemoveGuildUser(SocketGuildUser user, SocketVoiceChannel vc)
        {
            if(null == vc) return;
            if(VCLinkedGames.TryGetValue(vc.Id, out var code))
            {
                if(_controllers.TryGetValue(code, out var muteController))
                {
                    muteController.LeaveFromVC(user);
                    return;
                }
                // TODO
                _logger.LogWarning($"Discordユーザー \"{user.Username}#{user.Discriminator}\" に対応するゲームのステータス情報が見つかりません.");
                return;
            }
            // TODO
            _logger.LogDebug($"どこのゲームとも接続されていません \"{vc.Name}\".");
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
            // TODO
            _logger.LogWarning($"プレイヤー \"{player.Character.PlayerInfo.PlayerName}\" に対応するゲームのステータス情報が見つかりません.");
            return;
        }

        internal void RemoveGamePlayer(IClientPlayer player)
        {
            if(_controllers.TryGetValue(player.Game.Code.Code, out var muteController))
            {
                muteController.LeaveFromGame(player);
                return;
            }
            // TODO
            _logger.LogWarning($"プレイヤー \"{player.Character.PlayerInfo.PlayerName}\" に対応するゲームのステータス情報が見つかりません.");
            return;
        }

        internal async Task CreateMuteController(SocketMessage from, string code)
        {
            var upperCode = code.ToUpper();
            IGame game;
            if(!Games.TryGetValue(upperCode, out game))
            {
                // TODO:
                await from.Channel.SendMessageAsync($"code {code} not found.");
                return;
            }
            MuteController existsController;
            if(!_controllers.TryGetValue(game.Code.Code, out existsController))
            {
                var user = from.Author as SocketGuildUser;
                if(VCLinkedGames.TryGetValue(user.VoiceChannel.Id, out var st))
                {
                    // TODO
                    _logger.LogWarning($"game {st} の設定が {game.Code.Code} で上書きされます");
                }
                try{
                var controller = new MuteController(_logger, game, user, _config);
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
            await from.Channel.SendMessageAsync($"指定したゲーム `{upperCode}` は、既に :speaker: \"{existsController.VoiceChannel}\" にリンクしています");
            return;
        }

        internal bool DestroyMuteController(string code, SocketGuildUser user=null)
        {
            if(_controllers.TryGetValue(code, out var controller))
            {
                controller.RestoreMuteControl(0);
                var embed = controller.EmbedMsg;
                var ch = embed.Channel;
                var destroyer = (user == null) ? "Server" : user.Mention;
                var content = $"Game `{code}` destroyed by {destroyer}.";
                Task.WaitAll(ch.SendMessageAsync(content), embed.DeleteAsync());

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

        internal bool RefreshEmbed(string code)
        {
            if(_controllers.TryGetValue(code, out var controller))
            {
                Task.WhenAll(controller.RefreshEmbed());
                return true;
            }
            return false;
        }

        internal void OnPlayerSpawned(IClientPlayer player)
        {
            if(_controllers.TryGetValue(player.Game.Code.Code, out var muteController))
            {
                muteController.SyncDeadStatus();
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

        internal string GetVCLinkedGameCode(SocketVoiceChannel vc)
        {
            if(VCLinkedGames.TryGetValue(vc.Id, out var code))
            {
                _logger.LogDebug($"Game found. vc({vc.Name}) => game[{code}]");
                return code;
            }
            return null;
        }
    }
}
