using System;
using System.Linq;
using System.Timers;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Discord;
using Discord.WebSocket;
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
        private const int DiscordFieldsCapacity = 25;

        private readonly ILogger<Discostor> _logger;
        private readonly IGame _game;
        private readonly EmoteManager _emoteManager;
        private Dictionary<ulong, MuteState> _defaultVCProps
            = new Dictionary<ulong, MuteState>();
        private readonly DiscordBotConfig _config;
        private Timer _delayTimer = null;
        private PhaseTypes _phase;
        private Dictionary<int, bool> _isDead
            = new Dictionary<int, bool>(DiscordFieldsCapacity);

        // Amongus Player ID(int) <=> Discord ID(ulong)
        private Dictionary<int, ulong> _idConversionDict
            = new Dictionary<int, ulong>(DiscordFieldsCapacity);
        // Discord ID(ulong) => Discord User
        private Dictionary<ulong, IGuildUser> _guildMembersInVC
            = new Dictionary<ulong, IGuildUser>();
        // Player ID(int) => AmongUs Player
        private Dictionary<int, IClientPlayer> _players
            = new Dictionary<int, IClientPlayer>(DiscordFieldsCapacity);
        private IUserMessage _embedMsg;

        private int _currentStamps = 0;

        internal SocketVoiceChannel VoiceChannel { get; }
        internal IMessageChannel Channel { get{ return _embedMsg.Channel; } }

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
                DiscordBotConfig config,
                EmoteManager emoteManager)
        {
            _logger = logger;
            _game = game;
            _config = config;
            _emoteManager = emoteManager;
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

        internal void JoinToVC(IGuildUser user)
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

        internal void LeaveFromVC(IGuildUser user)
        {
            _guildMembersInVC.Remove(user.Id, out var member);

            var nextMuteState = _defaultVCProps[user.Id];
            if(NeedMuteControl(user, nextMuteState))
            {
                Task.Run(() => user.ModifyAsync(x =>
                {
                    x.Mute = nextMuteState.Mute;
                    x.Deaf = nextMuteState.Deaf;
                }));
            }
            _defaultVCProps.Remove(user.Id);

            var playerId = _idConversionDict.FirstOrDefault(x => x.Value == user.Id);
            if(playerId.Value == user.Id)
            {
                _idConversionDict.Remove(playerId.Key);
                Task.WhenAll(_embedMsg.Channel.SendMessageAsync($"{user.Mention} has left."));
            }
            _logger.LogInformation($"[Discord] \"{user.Username}#{user.Discriminator}\" leaved from game {_game.Code.Code}.");
            PutInternalInfo();
            UpdateEmbedMonitor();
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
            _logger.LogInformation($"\"[among us] {player.Client.Name}\" joined to game {_game.Code.Code}.");
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
            _logger.LogInformation($"\"[among us] {player.Client.Name}\" leaved from game {_game.Code.Code}.");
        }

        internal void PlayerSpawned()
        {
            PutInternalInfo();
            UpdateEmbedMonitor();
        }

        internal bool LinkUserAndPlayer(IGuildUser user, int monitorIndex, bool force=false)
        {
            if(monitorIndex >= _players.Count)
            {
                _logger.LogInformation($"player[{monitorIndex}] not found");
                return false;
            }
            if(_idConversionDict.Values.Contains(user.Id))
            {
                _logger.LogInformation($"\"{user.Username}#{user.Discriminator}\" is already linked.");
                return false;
            }

            var playerId = _players.Keys.ToArray()[monitorIndex];
            if(_idConversionDict.TryGetValue(playerId, out var uid))
            {
                if(!force) return false;
                UnlinkCommon(user, uid, playerId, true);
            }

            LinkCommon(user, playerId);
            PutInternalInfo();
            UpdateEmbedMonitor();
            return true;
        }

        internal bool UnlinkUserAndPlayer(IGuildUser user, int monitorIndex, bool force=false)
        {
            if(monitorIndex >= _players.Count)
            {
                _logger.LogInformation($"player[{monitorIndex}] not found");
                return false;
            }
            var playerId = _players.Keys.ToArray()[monitorIndex];
            if(_idConversionDict.TryGetValue(playerId, out var uid))
            {
                if(UnlinkCommon(user, uid, playerId))
                {
                    PutInternalInfo();
                    UpdateEmbedMonitor();
                    return true;
                }
                return false;
            }
            _logger.LogInformation($"Not linked. - {user.Username}#{user.Discriminator} =x= {_players[playerId].Client.Name}");
            return false;
        }

        internal bool ToggleLink(IGuildUser user, IUserMessage msg, IEmote emote)
        {
            // Whether the message is for a game or not
            if(msg.Id != _embedMsg.Id) return false;

            // Whether the index of the reaction is in range or not
            var monitorIndex = _emoteManager.GetIndex(emote.Name);
            _logger.LogDebug($"inx ({monitorIndex})");
            if(monitorIndex == -1) return false; // normal reaction
            if(monitorIndex >= _players.Count)
            {
                _logger.LogInformation($"player[{monitorIndex}] not found");
                return false;
            }

            // Check OK, attempt to operate the user.

            // Check link or unlink
            var playerId = _players.Keys.ToArray()[monitorIndex];
            if(_idConversionDict.TryGetValue(playerId, out var uid))
            {  // Link found, unlink
                if(UnlinkCommon(user, uid, playerId))
                {
                    PutInternalInfo();
                    UpdateEmbedMonitor();
                    return true;
                }
                return false;
            }

            // Link not found, create new link.

            if(_idConversionDict.Values.Contains(user.Id))
            {
                _logger.LogInformation($"\"{user.Username}#{user.Discriminator}\" is already linked.");
                return false;
            }

            LinkCommon(user, playerId);
            PutInternalInfo();
            UpdateEmbedMonitor();
            return true;
        }

        private void LinkCommon(IGuildUser user, int playerId)
        {
            _idConversionDict[playerId] = user.Id;
            _defaultVCProps[user.Id] = CreateCurrentMuteState(user);

            var playerName = _players[playerId].Client.Name;
            _logger.LogInformation($"O Player linked: {playerName} <=o=> {user.Username}");
        }

        private bool UnlinkCommon(IGuildUser user, ulong foundId, int playerId, bool force=false)
        {
            if(user.Id != foundId)
            {
                _logger.LogInformation($"There is already a link to the other user. - {_players[playerId]} => {_guildMembersInVC[foundId]}");
                if(!force) return false;
                _logger.LogWarning($"Forcibly disconnect.");
            }

            // Unmute and unlink
            var nextMuteState = _defaultVCProps[user.Id];
            if(NeedMuteControl(user, nextMuteState))
            {
                Task.Run(() => user.ModifyAsync(x =>
                {
                    x.Mute = nextMuteState.Mute;
                    x.Deaf = nextMuteState.Deaf;
                }));
            }
            _defaultVCProps.Remove(user.Id);
            _idConversionDict.Remove(playerId);
            var playerName = _players[playerId].Client.Name;
            _logger.LogInformation($"X Player unlinked: {playerName} <=x=> {user.Username}");
            return true;
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
                var nextMuteState = CreateNextMuteState(user, muteType);
                if(NeedMuteControl(user, nextMuteState))
                {
                    mtasks.Add(user.ModifyAsync(x =>
                    {
                        x.Mute = nextMuteState.Mute;
                        x.Deaf = nextMuteState.Deaf;
                    }));
                }
            }
            try
            {
                Task.WhenAll(mtasks);
            }
            catch(Exception e)
            {
                LogStackTrace(e, "CommonMuteControl");
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
            if(_game.GameState == GameStates.Started)
            {
                foreach(var p in _game.Players)
                {
                    _isDead[p.Client.Id] = p.Character.PlayerInfo.IsDead;
                }
            }
        }

        // Get current mute state
        private MuteState CreateCurrentMuteState(IGuildUser user)
        {
            var state = new MuteState();
            state.Deaf = user.IsDeafened;
            state.Mute = user.IsMuted;
            return state;
        }

        private MuteState CreateNextMuteState(IGuildUser user, MuteType muteType)
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
                    var name = _players[cnvInfo.Key].Client.Name;
                    _logger.LogDebug($"Player => {name}({user.Username}) is dead: {isDead}");
                }
                catch(Exception e)
                {
                    LogStackTrace(e, "CreateNextMuteState");
                }
            }
            else
            {
                _logger.LogInformation($"Spectator => {user.Username}");
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

        private bool NeedMuteControl(IGuildUser user, MuteState before, MuteState after)
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

        private bool NeedMuteControl(IGuildUser user, MuteState after)
            => NeedMuteControl(user, CreateCurrentMuteState(user), after);

        internal async Task<IUserMessage> GenerateEmbedMsg(ISocketMessageChannel ch)
        {
            var embed = CreateBuilder()?.Build();
            var emsg = await ch.SendMessageAsync(embed:embed);
            _embedMsg = emsg;
            await Task.Run(AddMuteStamps);
            return emsg;
        }

        internal async Task RefreshEmbedAsync()
        {
            var ch = _embedMsg.Channel as ISocketMessageChannel;
            var embedTask = GenerateEmbedMsg(ch);
            await Task.WhenAll(
                embedTask,
                _embedMsg.DeleteAsync()
            );
            _embedMsg = await embedTask;
            _currentStamps = 0;
            await Task.Run(AddMuteStamps);
        }

        internal async Task DeleteEmbedAsync()
            => await _embedMsg.DeleteAsync();

        private void UpdateEmbedMonitor()
        {
            // debug
            PutInternalInfo();

            var embed = CreateBuilder()?.Build();
            Task.Run(()=>_embedMsg.ModifyAsync(x=>{x.Embed=embed;}));
            Task.Run(AddMuteStamps);
        }

        private MonitorEmbedBuilder CreateBuilder()
        {
            var builder = new MonitorEmbedBuilder(_logger, _emoteManager);
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
                LogStackTrace(e, "CreateBuilder");
            }
            return builder;
        }

        private async Task AddMuteStamps()
        {
            var emotes = _emoteManager.Emotes;
            var eml = new List<IEmote>();
            while(_currentStamps < _players.Count)
            {
                _logger.LogDebug($"add emoji({_currentStamps})");
                //eml.Add(new Emoji(emotes[_currentStamps]));
                var e = _emoteManager.GetEmote(_currentStamps);
                eml.Add(e);
                _currentStamps++;
            }
            await _embedMsg.AddReactionsAsync(eml.ToArray());
        }

        private void LogStackTrace(Exception e, string scope)
        {
            _logger.LogError($"{e.GetType()} -- {e.Message}");
            _currentStamps = 0;
            using(_logger.BeginScope(scope))
            {
                _logger.LogError(e.StackTrace);
            }
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
            catch(Exception e)
            {
                _logger.LogDebug($"{e.GetType()} -- {e.Message}");
                using(_logger.BeginScope("PutInternalInfo"))
                {
                    _logger.LogDebug(e.StackTrace);
                }
            }
        }
    }
}
