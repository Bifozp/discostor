using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Discord;
using Discord.Commands;
using Impostor.Plugins.Discostor.Config;

namespace Impostor.Plugins.Discostor.Discord
{
    public class HelpEmbedBuilder
    {
        private readonly CommandService _comms;
        private readonly ILogger<Discostor> _logger;
        private readonly DiscordBotConfig _config;

        public HelpEmbedBuilder(
                CommandService comms,
                ILogger<Discostor> logger,
                ConfigRoot config
                )
        {
            _comms = comms;
            _logger = logger;
            _config = config.Discord;
        }

        public Embed Build(string input=null)
        {
            var builder = new EmbedBuilder();
            builder.WithColor(Color.Gold);
            builder.WithTitle("Help");
            if(string.IsNullOrEmpty(input))
            {
                DefaultHelp(builder);
            }
            else
            {
                SpecificHelp(builder, input);
            }
            return builder.Build();
        }

        private void DefaultHelp(EmbedBuilder builder)
        {
            var modules = _comms.Modules.ToList();
            builder.WithTitle("Welcome to Discostor");
            builder.WithDescription($"You can type `{_config.CommandPrefix} help <command>` to get more information.");
            foreach(var mod in _comms.Modules.ToList())
            {
                var fieldName = mod.Remarks;
                fieldName += mod.Name;
                var fieldBody = mod.Summary;
                builder.AddField(fieldName, fieldBody, true);
            }
        }

        private void SpecificHelp(EmbedBuilder builder, string input)
        {
            var cmdSearchResult = _comms.Search(input);
            if(cmdSearchResult.IsSuccess)
            {
                if(cmdSearchResult.Commands.Count != 1)
                {
                    return;
                }
                var cmdInfo = cmdSearchResult.Commands.FirstOrDefault().Command;
                builder.WithTitle(cmdInfo.Name ?? cmdInfo.ToString());
                builder.WithDescription(cmdInfo.Summary ?? cmdInfo.ToString());
                builder.AddField("Remarks", cmdInfo.Remarks ?? cmdInfo.ToString());
                if(cmdInfo.Aliases.Count > 0)
                {
                    builder.AddField("Aliases", string.Join(", ", cmdInfo.Aliases));
                }
                return;
            }

            var modules = _comms.Modules.Where(mod => mod.Commands.Count > 0);
            var moduleMatch = modules?.FirstOrDefault(m => m.Name == input || m.Aliases.Contains(input));
            if(moduleMatch != null)
            {
                var cmdNames = new List<string>();
                foreach(var c in moduleMatch.Commands)
                {
                    cmdNames.Add(c.Name);
                }
                builder.WithTitle(moduleMatch.Name ?? moduleMatch.ToString());
                builder.WithDescription(moduleMatch.Summary ?? moduleMatch.ToString());
                builder.AddField("Commands", string.Join(", ", cmdNames));
                return;
            }

            // command not found
            builder.WithTitle("Command not found");
            builder.WithDescription($"I don't know `{input}` command.");
        }
    }
}
