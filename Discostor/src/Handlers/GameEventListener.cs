using Microsoft.Extensions.Logging;
using Impostor.Api.Events;
using Impostor.Plugins.Discostor.Mute;

namespace Impostor.Plugins.Discostor.Handlers
{
    public class GameEventListener : IEventListener
    {
        private readonly ILogger<Discostor> _logger;
        private readonly AutomuteService _automuteService;

        public GameEventListener(
                ILogger<Discostor> logger,
                AutomuteService automuteService)
        {
            _logger = logger;
            _automuteService = automuteService;
        }

        [EventListener]
        public void OnGameCreated(IGameCreatedEvent e)
        {
            _automuteService.Games.TryAdd(e.Game.Code.Code, e.Game);
        }

        [EventListener]
        public void OnGameDestroyed(IGameDestroyedEvent e)
        {
            _automuteService.DestroyMuteController(e.Game.Code.Code);
            _automuteService.Games.Remove(e.Game.Code.Code);
        }

        [EventListener]
        public void OnGameStarted(IGameStartedEvent e)
        {
            _automuteService.OnGameStarted(e.Game);
        }

        [EventListener]
        public void OnGameEnded(IGameEndedEvent e)
        {
            _automuteService.OnGameEnded(e.Game);
        }

        [EventListener]
        public void OnPlayerJoined(IGamePlayerJoinedEvent e)
        {
            _automuteService.AddGamePlayer(e.Player);
        }

        [EventListener]
        public void OnPlayerLeftGame(IGamePlayerLeftEvent e)
        {
            _automuteService.RemoveGamePlayer(e.Player);
        }
    }
}
