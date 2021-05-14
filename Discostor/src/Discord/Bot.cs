using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Impostor.Plugins.Discostor.Config;
using Impostor.Plugins.Discostor.Mute;

namespace Impostor.Plugins.Discostor.Discord
{
    public class Bot : IHostedService, IDisposable
    {
        private const char SplitChar = ' ';
        private readonly DiscordSocketClient _mainClient;
        private readonly CommandService _comms;
        private readonly IServiceProvider _services;
        private readonly DiscordBotConfig _config;
        private readonly ILogger<Discostor> _logger;
        private readonly AutomuteService _automuteService;

        public Bot(DiscordSocketClient mainClient,
                CommandService comms,
                IServiceProvider services,
                ConfigRoot config,
                ILogger<Discostor> logger,
                AutomuteService automuteService)
        {
            _mainClient = mainClient;
            _comms = comms;
            _services = services;
            _config = config.Discord;
            _logger = logger;
            _automuteService = automuteService;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Discostor bot service is starting...");
            _mainClient.Ready += onReady;
            _mainClient.MessageReceived += onMessageReceived;
            _mainClient.UserVoiceStateUpdated += onUserVoiceStateUpdated;
            _mainClient.Log += onLogging;
            _comms.Log += onLogging;
            _comms.CommandExecuted += onCommandExecuted;
            await _comms.AddModulesAsync(Assembly.GetExecutingAssembly(), _services);
            await _mainClient.LoginAsync(TokenType.Bot, _config.Token);
            await _mainClient.StartAsync();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Discostor bot service is stopping...");
            await _mainClient.SetGameAsync(null);
            await _mainClient.SetStatusAsync(UserStatus.Offline);
            await _mainClient.StopAsync();
            _comms.CommandExecuted -= onCommandExecuted;
            _comms.Log -= onLogging;
            _mainClient.Log -= onLogging;
            _mainClient.UserVoiceStateUpdated -= onUserVoiceStateUpdated;
            _mainClient.MessageReceived -= onMessageReceived;
            _mainClient.Ready -= onReady;
        }

        public void Dispose()
        {
            _mainClient?.Dispose();
        }

        private async Task onReady()
        {
            var cmdHelp = _config.CommandPrefix + "help";
            await _mainClient.SetStatusAsync(UserStatus.Online);
            await _mainClient.SetGameAsync($"type {cmdHelp} for help");
        }

        private async Task onLogging(LogMessage log)
        {
            _logger.LogInformation(log.Message);
            await Task.CompletedTask;
        }

        private async Task onMessageReceived(SocketMessage msgParam)
        {
            var msg = msgParam as SocketUserMessage;
            // Don't process system msg
            if(null == msg) return;
            // Don't process bot msg
            if (msg.Author.IsBot || msg.Author.IsWebhook) return;

            int argPos = 0;

            if( !(
                        msg.HasStringPrefix(_config.CommandPrefix, ref argPos) ||
                        msg.HasMentionPrefix(_mainClient.CurrentUser, ref argPos)
                 )) return;
 
            // Skip whitespaces
            while(argPos < msg.Content.Length)
            {
                if(msg.Content.ToCharArray()[argPos] != SplitChar)
                {
                    break;
                }
                argPos++;
            }

            // non-args
            _logger.LogDebug($"len:{msg.Content.Length} / argPos:{argPos}");
            if(msg.Content.Length <= argPos)
            {
                var tasks = new List<Task>();
                var builder = _services.GetService(typeof(HelpEmbedBuilder)) as HelpEmbedBuilder;
                var embed = builder.Build();
                tasks.Add(msg.Channel.SendMessageAsync(embed:embed));
                if(_config.RemoveCommand && msg.Channel is not IPrivateChannel)
                {
                    tasks.Add(msg.Channel.DeleteMessageAsync(msg));
                }
                await Task.WhenAll(tasks);
                return;
            }

            // Create a WebSocket-based command
            var context = new SocketCommandContext(_mainClient, msg);
            var resultTask = _comms.ExecuteAsync(
                    context: context,
                    argPos: argPos,
                    services: _services);
            if(_config.RemoveCommand && msg.Channel is not IPrivateChannel)
            {
                await msg.Channel.DeleteMessageAsync(msg);
            }
            var result = await resultTask;
            if(!result.IsSuccess)
            {
                var cmd = getCmd(msg.Content, argPos, SplitChar);
                await msg.Channel.SendMessageAsync($"failed to execute `{cmd}` command. ({result.ErrorReason})");
            }
        }

        private async Task onUserVoiceStateUpdated(SocketUser userParam, SocketVoiceState vsPrev, SocketVoiceState vsNew)
        {
            var user = userParam as SocketGuildUser;
            if(user == null)
            {
                _logger.LogInformation($"user \"{user.Username}\" isn't guild user.");
                return;
            }

            // Leave or Join
            if(vsPrev.VoiceChannel != vsNew.VoiceChannel)
            {
                _automuteService.RemoveGuildUser(user, vsPrev.VoiceChannel);
                _automuteService.AddGuildUser(user, vsNew.VoiceChannel);
            }
            await Task.CompletedTask;
        }

        private async Task onCommandExecuted(Optional<CommandInfo> cmd, ICommandContext ctx, IResult result)
        {
            var user = ctx.Message.Author;
            _logger.LogInformation($"{user.Username}#{user.Discriminator} > executed {ctx.Message.Content} ({result})");
            await Task.CompletedTask;
        }

        private string getCmd(string s, int argPos, char splitter)
        {
            var inx = s.IndexOf(splitter, argPos);
            if(inx == -1)
            {
                inx = s.Length;
            }
            _logger.LogDebug($"\"{s}\" -- ({argPos})-({inx})");
            return s.Substring(argPos, inx-argPos);
        }
    }
}
