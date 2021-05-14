using Microsoft.Extensions.Logging;
using Impostor.Api.Events;
using Impostor.Api.Events.Meeting;
using Impostor.Plugins.Discostor.Mute;
using Impostor.Plugins.Discostor.Config;

namespace Impostor.Plugins.Discostor.Handlers
{
    public class MeetingEventListener : IEventListener
    {
        private readonly ILogger<Discostor> _logger;
        private readonly AutomuteService _automuteService;
        private readonly DiscordBotConfig _config;

        public MeetingEventListener(
                ILogger<Discostor> logger,
                AutomuteService automuteService,
                ConfigRoot config)
        {
            _logger = logger;
            _automuteService = automuteService;
            _config = config.Discord;
        }

        [EventListener]
        public void OnMeetingStarted(IMeetingStartedEvent e)
        {
            _automuteService.OnMeetingStarted(e.Game);
        }

        [EventListener]
        public void OnMeetingEnded(IMeetingEndedEvent e)
        {
            _automuteService.OnMeetingEnded(e.Game);
        }
    }
}
