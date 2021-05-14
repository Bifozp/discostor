using System;
using System.Linq;
using System.Timers;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Discord.WebSocket;
using Discord.Rest;
using Impostor.Api.Games;
using Impostor.Api.Net;
using Impostor.Api.Innersloth;
using Impostor.Plugins.Discostor.Discord;
using Impostor.Plugins.Discostor.Config;
using Impostor.Plugins.Discostor.Game;

namespace Impostor.Plugins.Discostor.Mute
{
    enum MuteType
    {
        Default,
        TaskPhase,
        MeetingPhase
    }

    internal class MuteController
    {
        private readonly ILogger<Discostor> _logger;
        private readonly IGame _game;
        private Dictionary<ulong, MuteState> _defaultVCProps
            = new Dictionary<ulong, MuteState>();
        private readonly DiscordBotConfig _config;
        private Timer _delayTimer = null;
        private PhaseTypes _phase;
        private Dictionary<int, bool> _isDead
            = new Dictionary<int, bool>();

        // Amongus Player ID(int) <=> Discord ID(ulong)
        private Dictionary<int, ulong> _idConversionDict
            = new Dictionary<int, ulong>();
        // Discord ID(ulong) => Discord User
        private Dictionary<ulong, SocketGuildUser> _guildMembersInVC
            = new Dictionary<ulong, SocketGuildUser>();
        // Player ID(int) => AmongUs Player
        private Dictionary<int, IClientPlayer> _players
            = new Dictionary<int, IClientPlayer>();
        private RestUserMessage _embedMsg;

        internal SocketVoiceChannel VoiceChannel { get; }
        internal RestUserMessage EmbedMsg { get{return _embedMsg;} }

        internal bool IsAbandoned
        {
            get
            {
                return (_guildMembersInVC.Count == 0)
                    && (_players.Count == 0);
            }
        }

        internal MuteController(
                ILogger<Discostor> logger,
                IGame game,
                SocketGuildUser user,
                DiscordBotConfig config)
        {
            _logger = logger;
            _game = game;
            _config = config;
            VoiceChannel = user.VoiceChannel;

            _phase = PhaseTypes.Unknown;
            if(game.GameState != GameStates.Started)
            {
                _phase = PhaseTypes.Lobby;
            }

            // add players
            using(_logger.BeginScope("players"))
            {
                _logger.LogDebug($"Game \"{game.Code.Code}\" players:");
                foreach(var player in _game.Players)
                {
                    _players[player.Client.Id] = player;
                    _isDead[player.Client.Id] = player.Character.PlayerInfo.IsDead;
                    _logger.LogDebug($"  {player.Character.PlayerInfo.PlayerName}");
                }
            }
            // add vc members
            using(_logger.BeginScope("vc-members"))
            {
                _logger.LogDebug($"VC \"{VoiceChannel.Name}\" members:");

                foreach(var vcuser in user.VoiceChannel.Users)
                {
                    _guildMembersInVC[vcuser.Id] = vcuser;
                    _defaultVCProps[vcuser.Id] = CreateCurrentMuteState(vcuser);
                    _logger.LogDebug($"  {vcuser.Username}");
                }
            }
            _logger.LogDebug($"MuteController created.");
        }

        internal void JoinToVC(SocketGuildUser user)
        {
            if(user.VoiceChannel.Id != VoiceChannel?.Id)
            {
                return;
            }
            _guildMembersInVC[user.Id] = user;
            _defaultVCProps[user.Id] = CreateCurrentMuteState(user);
            _logger.LogInformation($"[Discord] \"{user.Username}\" joined to game {_game.Code.Code}.");
            PutInternalInfo();
            UpdateEmbedMonitor();
        }

        internal void LeaveFromVC(SocketGuildUser user)
        {
            _guildMembersInVC.Remove(user.Id, out var member);

            // TODO: Restore code cleanup
            var tsks = new List<Task>();
            var ms = CreateCurrentMuteState(user);
            var ds = _defaultVCProps[user.Id];
            if(NeedMuteControl(user, ms, ds))
            {
                tsks.Add( user.ModifyAsync(x =>
                {
                    x.Mute = ds.Mute;
                    x.Deaf = ds.Deaf;
                }));
            }
            _defaultVCProps.Remove(user.Id);

            var playerId = _idConversionDict.FirstOrDefault(x => x.Value == user.Id);
            if(playerId.Value == user.Id)
            {
                _idConversionDict.Remove(playerId.Key);
                Task.WhenAll(_embedMsg.Channel.SendMessageAsync($"{user.Mention} has left."));
            }
            _logger.LogInformation($"[Discord] \"{user.Username}\" leaved from game {_game.Code.Code}.");
            PutInternalInfo();
            UpdateEmbedMonitor();
            Task.WhenAll(tsks);
        }

        internal void JoinToGame(IClientPlayer player)
        {
            if(player.Game.Code.Code != _game.Code.Code)
            {
                return;
            }
            _logger.LogDebug($"joined ==> {player}");
            _players[player.Client.Id] = player;
            var pi = player.Character?.PlayerInfo;
            bool isDead = false;
            if(null != pi) isDead = pi.IsDead;
            _isDead[player.Client.Id] = isDead;
        }

        internal void LeaveFromGame(IClientPlayer player)
        {
            var playerId = player.Client.Id;
            _players.Remove(playerId);
            _isDead.Remove(player.Client.Id);
            if(_idConversionDict.ContainsKey(playerId))
            {
                _idConversionDict.Remove(playerId);
            }
            PutInternalInfo();
            UpdateEmbedMonitor();
            _logger.LogInformation($"\"[among us] {player.Character.PlayerInfo.PlayerName}\" leaved from game {_game.Code.Code}.");
        }

        internal void PlayerSpawned()
        {
            PutInternalInfo();
            UpdateEmbedMonitor();
        }

        internal bool LinkUserAndPlayer(SocketGuildUser user, int playerIndex, bool force=false)
        {
            if(_idConversionDict.Values.Contains(user.Id))
            {
                _logger.LogInformation("二重登録になるため失敗");
                return false;
            }
            if(playerIndex >= _players.Count)
            {
                _logger.LogInformation($"player[{playerIndex}] not found");
                return false;
            }
            var vt = _players.ToArray();
            var v = vt[playerIndex];
            if(_idConversionDict.TryGetValue(v.Key, out var uid))
            {
                _logger.LogInformation($"割当あり - {_players[v.Key]} => {_guildMembersInVC[uid]}");
                return false;
            }

            _idConversionDict[v.Key] = user.Id;
            _defaultVCProps[user.Id] = CreateCurrentMuteState(user);

            PutInternalInfo();
            UpdateEmbedMonitor();
            var playerName = _players[v.Key].Client.Name;
            _logger.LogInformation($"Player linked: {playerName} => {user.Username}");
            return true;
        }

        internal bool UnlinkUserAndPlayer(SocketGuildUser user, int playerIndex, bool force=false)
        {
            if(playerIndex >= _players.Count)
            {
                _logger.LogInformation($"player[{playerIndex}] not found");
                return false;
            }
            var pids = _players.Keys.ToArray();
            var pid = pids[playerIndex];
            if(_idConversionDict.TryGetValue(pid, out var uid))
            {
                if(uid != user.Id)
                {
                    _logger.LogInformation($"他人指定 - {_players[pid]} => {_guildMembersInVC[uid]}");
                    if(!force) return false;
                }

                // TODO:Restore code cleanup
                var tsks = new List<Task>();
                var ms = CreateCurrentMuteState(user);
                var ds = _defaultVCProps[user.Id];
                if(NeedMuteControl(user, ms, ds))
                {
                    tsks.Add( user.ModifyAsync(x =>
                    {
                        x.Mute = ds.Mute;
                        x.Deaf = ds.Deaf;
                    }));
                }
                _defaultVCProps.Remove(user.Id);
                _idConversionDict.Remove(pid);
                PutInternalInfo();
                UpdateEmbedMonitor();
                Task.WhenAll(tsks);
                return true;
            }
            _logger.LogInformation($"割当なし - {_players[pid]}");
            return false;
        }

        internal void RestoreMuteControl(int delay)
            => TriggerMuteTimer(delay, MuteType.Default);

        internal void TaskMuteControl(int delay)
            => TriggerMuteTimer(delay, MuteType.TaskPhase);

        internal void MeetingMuteControl(int delay)
            => TriggerMuteTimer(delay, MuteType.MeetingPhase);

        private void TriggerMuteTimer(int delay, MuteType MuteType)
        {
            _delayTimer?.Dispose();
            if(delay == 0)
            {
                Task.Run(() => CommonMuteControl(MuteType));
                _logger.LogInformation($"Mute delay = 0, mute start - type: {MuteType}");
                return;
            }

            _delayTimer = new Timer();
            _delayTimer.Interval = delay;
            _delayTimer.Elapsed += (s,e) => {
                CommonMuteControl(MuteType);
            };
            _delayTimer.AutoReset = false;
            _delayTimer.Start();
            _logger.LogInformation($"Mute delay timer started - type: {MuteType}, delay: {delay}ms");
        }

        private void CommonMuteControl(MuteType muteType)
        {
            _logger.LogDebug($"Game({_game.Code.Code}) mute task start.");
            var mtasks = new List<Task>();
            foreach(var user in _guildMembersInVC.Values)
            {
                var curState = CreateCurrentMuteState(user);
                MuteState newState = CreateNextMuteState(user, muteType);
                if(NeedMuteControl(user, curState, newState))
                {
                    mtasks.Add(user.ModifyAsync(x =>
                    {
                        x.Mute = newState.Mute;
                        x.Deaf = newState.Deaf;
                    }));
                }
            }
            try
            {
                Task.WhenAll(mtasks);
            }
            catch(Exception e)
            {
                _logger.LogError($"{e.GetType().Name} -- {e.Message}");
                _logger.LogError($"{e.StackTrace}");
            }
            _delayTimer?.Stop();
            _delayTimer?.Dispose();
            _delayTimer = null;

            // Change current phase
            switch(muteType)
            {
                case MuteType.TaskPhase:
                    _phase = PhaseTypes.Task;
                    break;
                case MuteType.MeetingPhase:
                    _phase = PhaseTypes.Meeting;
                    break;
                case MuteType.Default:
                    _phase = PhaseTypes.Lobby;
                    break;
            }

            PutInternalInfo();
            UpdateEmbedMonitor();
        }

        internal void SyncDeadStatus()
        {
            foreach(var p in _game.Players)
            {//
                _isDead[p.Client.Id] = p.Character.PlayerInfo.IsDead;
            }
        }

        // Get current mute state
        private MuteState CreateCurrentMuteState(SocketGuildUser user)
        {
            var state = new MuteState();
            state.Deaf = user.IsDeafened;
            state.Mute = user.IsMuted;
            return state;
        }

        private MuteState CreateNextMuteState(SocketGuildUser user, MuteType muteType)
        {
            // set default state
            _logger.LogDebug($"{user.Username} mute conf");
            var ms = new MuteState(_defaultVCProps[user.Id]);
            if(muteType == MuteType.Default) return ms;

            _logger.LogDebug($"{user.Username} dead state set");
            // Set dead state
            bool isDead = true;
            var cnvInfo = _idConversionDict.FirstOrDefault( u => u.Value == user.Id);
            if(cnvInfo.Value == user.Id)
            {
                try
                {
                    _logger.LogDebug($"{user.Username} found in conversion list");
                    isDead = _isDead[cnvInfo.Key];
                    //var name = player.Character.PlayerInfo.PlayerName;
                    //_logger.LogDebug($"プレイヤー => {name}({user.Username}) is dead: {isDead}");
                }
                catch(Exception e)
                {
                    _logger.LogError($"{e.GetType()} -- {e.Message}");
                    _logger.LogError($"{e.StackTrace}");
                }
            }
            else
            {
                _logger.LogInformation($"観客 => {user.Username}");
            }

            // Apply mute rule
            MuteConfig m = _config.TaskPhaseMuteRules;
            if(muteType == MuteType.MeetingPhase)
            {
                m = _config.MeetingPhaseMuteRules;
            }
            ms.Mute = isDead ? m.Mute.Dead : m.Mute.Alive;
            ms.Deaf = isDead ? m.Deaf.Dead : m.Deaf.Alive;
            return ms;
        }

        private bool NeedMuteControl(SocketGuildUser user, MuteState before, MuteState after)
        {
            if(_config.MuteSpectator || _idConversionDict.Values.Contains(user.Id))
            {
                if(!before.Equals(after))
                {
                    return true;
                }
            }
            return false;
        }

        internal async Task<RestUserMessage> GenerateEmbedMsg(ISocketMessageChannel ch)
        {
            var embed = CreateBuilder()?.Build();
            var emsg = await ch.SendMessageAsync(embed:embed);
            _embedMsg = emsg;
            return emsg;
        }

        internal async Task RefreshEmbed()
        {
            var ch = _embedMsg.Channel as ISocketMessageChannel;
            var embedTask = GenerateEmbedMsg(ch);
            await Task.WhenAll(
                embedTask,
                _embedMsg.DeleteAsync()
            );
            _embedMsg = await embedTask;
        }

        private void UpdateEmbedMonitor()
        {
            // debug
            PutInternalInfo();

            var embed = CreateBuilder()?.Build();
            Task.Run(()=>_embedMsg.ModifyAsync(x=>{x.Embed=embed;}));
        }

        private MonitorEmbedBuilder CreateBuilder()
        {
            var builder = new MonitorEmbedBuilder(_logger);
            try
            {
                builder.GameCode = _game.Code.Code;
                builder.Phase = _phase;
                builder.VoiceChatName = VoiceChannel.Name;
                builder.OwnerPlayerName = _game.Host?.Character?.PlayerInfo?.PlayerName;
                builder.GameOption = _game.Options;
                if(_game.Host != null && _idConversionDict.TryGetValue(_game.Host.Client.Id, out var vcid))
                {
                    if(_guildMembersInVC.TryGetValue(vcid, out var vcuser))
                    {
                        builder.OwnerDiscordUserName = vcuser.Mention;
                    }
                }
                var playerFields = new List<PlayerFileldInfo>();
                _logger.LogDebug($"== players: {_players.Count}");
                foreach(var player in _players.Values)
                {
                    var fieldInfo = new PlayerFileldInfo();
                    var pi = player.Character?.PlayerInfo;
                    if(string.IsNullOrEmpty(pi?.PlayerName))
                    {
                        fieldInfo.PlayerName = player.Client.Name;
                    }
                    else
                    {
                        fieldInfo.PlayerName = pi.PlayerName;
                    }
                    if(_game.GameState != GameStates.Started)
                    {
                        fieldInfo.IsDead = false;
                    }
                    else
                    {
                        fieldInfo.IsDead = _isDead[player.Client.Id];
                    }
                    if(_idConversionDict.TryGetValue(player.Client.Id, out var uid))
                    {
                        if(_guildMembersInVC.TryGetValue(uid, out var vcuser))
                        {
                            fieldInfo.DiscordUserName = vcuser.Mention;
                        }
                    }
                    playerFields.Add(fieldInfo);
                }
                builder.Players = playerFields;
            }
            catch(Exception e)
            {
                _logger.LogError($"{e.GetType()} - {e.Message}");
                _logger.LogError($"{e.StackTrace}");
            }
            return builder;
        }

        private void PutInternalInfo()
        {
            try
            {
                _logger.LogDebug("--- players ---");
                foreach(var p in _players)
                {
                    _logger.LogDebug($"{p.Value.Client.Name} ({p.Key})");
                }
                _logger.LogDebug("--- isDead ---");
                foreach(var d in _isDead)
                {
                    _logger.LogDebug($"isdead:{d.Value} ({d.Key})");
                }
                _logger.LogDebug("--- vc users ---");
                foreach(var u in _guildMembersInVC)
                {
                    _logger.LogDebug($"{u.Value.Username} ({u.Key})");
                }
                _logger.LogDebug("--- conv table ---");
                foreach(var ids in _idConversionDict)
                {
                    _logger.LogDebug($"discord({ids.Value}) -> ({ids.Key})");
                }
            }
            catch(Exception){}
        }
    }
}
