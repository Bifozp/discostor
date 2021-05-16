using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Discord;
using Impostor.Api.Innersloth;
using Impostor.Plugins.Discostor.Game;
using Impostor.Plugins.Discostor.Exceptions;

namespace Impostor.Plugins.Discostor.Discord
{
    internal class MonitorEmbedBuilder
    {
        private readonly ILogger<Discostor> _logger;
        private readonly EmoteManager _emoteManager;

        internal string GameCode { get; set; }
        internal PhaseTypes Phase { get; set; }
        internal string VoiceChatName { get; set; }
        internal string OwnerPlayerName { get; set; }
        internal string OwnerDiscordUserName { get; set; }
        internal List<PlayerFileldInfo> Players { get; set; }
        internal GameOptionsData GameOption { get; set; }

        public MonitorEmbedBuilder(
                ILogger<Discostor> logger,
                EmoteManager emoteManager
                )
        {
            _logger = logger;
            _emoteManager = emoteManager;
        }

        internal Embed Build()
        {
            CheckRequiredProperties();

            var embed = new EmbedBuilder();
            embed.WithAuthor($"{GameCode} : {Players.Count}/{GameOption.MaxPlayers} ({Phase})");
            embed.WithTitle($":speaker: {VoiceChatName}");
            var owner = string.IsNullOrEmpty(OwnerPlayerName) ? "-" : OwnerPlayerName;
            var discordUsername = 
                string.IsNullOrEmpty(OwnerDiscordUserName) ? "" : $" ({OwnerDiscordUserName})";
            embed.WithDescription($"**Owner: **{owner}{discordUsername}");
            switch(Phase)
            {
                case PhaseTypes.Unknown:
                    embed.WithColor(Color.LightGrey);
                    break;
                case PhaseTypes.Lobby:
                    embed.WithColor(Color.Green);
                    break;
                case PhaseTypes.Task:
                    embed.WithColor(Color.Purple);
                    break;
                case PhaseTypes.Meeting:
                    embed.WithColor(Color.Magenta);
                    break;
            }

            // create footer
            if(GameOption != null) AddGameruleFooter(embed);

            // create player info
            if(Players.Count == 0)
            {
                embed.WithColor(Color.Red);
                embed.AddField("There are no players", $"Let's join and play the game `{GameCode}`.");
            }
            else
            {
                var fid = 0;
                foreach(var p in Players)
                {
                    var emo = _emoteManager.GetEmote(fid);
                    var title = $"{emo} ";
                    title += p.IsDead ? $"~~{p.PlayerName}~~" : p.PlayerName;
                    var body = string.IsNullOrEmpty(p.DiscordUserName) ? "-" : p.DiscordUserName;
                    embed.AddField(title, body, true);
                    fid++;
                }
                if(Phase==PhaseTypes.Lobby)
                
                {
                    if(Players.Count < 4) embed.WithColor(Color.Red);
                    else if(Players.Count < 6) embed.WithColor(Color.Orange);
                }
            }
            return embed.Build();
        }

        private void CheckRequiredProperties()
        {
            var missingProperties = new List<string>();
            if(string.IsNullOrEmpty(GameCode)) missingProperties.Add(nameof(GameCode));
            if(string.IsNullOrEmpty(VoiceChatName)) missingProperties.Add(nameof(VoiceChatName));

            if(missingProperties.Count != 0)
            {
                var ex = new RequiredPropertyException("Missing required parameters.");
                ex.MissingProperties = missingProperties;
            }
        }

        private void AddGameruleFooter(EmbedBuilder embed)
        {
            var ctasks = GameOption.NumCommonTasks;
            var ltasks = GameOption.NumLongTasks;
            var stasks = GameOption.NumShortTasks;
            var footer = string.Join(" / ",
                    $"{GameOption.Map}",
                    $"Impos:{GameOption.NumImpostors}",
                    $"Meets:{GameOption.NumEmergencyMeetings}",
                    $"ECD:{GameOption.EmergencyCooldown}s",
                    $"DT:{GameOption.DiscussionTime}s",
                    $"VT:{GameOption.VotingTime}s",
                    $"PS:x{GameOption.PlayerSpeedMod}",
                    $"CV:x{GameOption.CrewLightMod}",
                    $"IV:x{GameOption.ImpostorLightMod}",
                    $"KCT:{GameOption.KillCooldown}s",
                    $"KD:{GameOption.KillDistance}",
                    $"TBU:{GameOption.TaskBarUpdate}",
                    $"Tasks:(C{ctasks};L{ltasks};S{stasks})"
                    );
            if(GameOption.ConfirmImpostor) footer += " / ConfirmEjects";
            if(GameOption.AnonymousVotes) footer += " / AnonymousVotes";
            if(GameOption.VisualTasks) footer += " / VisulaTask";
            if(!GameOption.GhostsDoTasks) footer += " / NoGhostsTask";
            if(GameOption.IsDefaults) footer += " (default)";
            embed.WithFooter(footer);
        }
    }
}
