using System;
using Impostor.Api.Events;
using Impostor.Api.Events.Player;
using Microsoft.Extensions.Logging;
using Impostor.Plugins.Discostor.Mute;

namespace Impostor.Plugins.Discostor.Handlers
{
    public class PlayerEventListener : IEventListener
    {
        private static readonly Random Random = new Random();

        private readonly ILogger<PlayerEventListener> _logger;
        private readonly AutomuteService _automuteService;

        public PlayerEventListener(
                ILogger<PlayerEventListener> logger,
                AutomuteService automuteService)
        {
            _logger = logger;
            _automuteService = automuteService;
        }

        [EventListener]
        public void OnPlayerSpawned(IPlayerSpawnedEvent e)
        {
            _automuteService.OnPlayerSpawned(e.ClientPlayer);
        }
    }
}
