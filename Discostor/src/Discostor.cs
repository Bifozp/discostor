using System.Threading.Tasks;
using Impostor.Api.Games.Managers;
using Impostor.Api.Innersloth;
using Impostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Impostor.Plugins.Discostor
{
    [ImpostorPlugin("com.bifozp.discostor")]
    public class Discostor : PluginBase
    {
        private readonly ILogger<Discostor> _logger;
        private readonly IGameManager _gameManager;

        public Discostor(ILogger<Discostor> logger, IGameManager gameManager)
        {
            _logger = logger;
            _gameManager = gameManager;
        }

        public override async ValueTask EnableAsync()
        {
            _logger.LogInformation("Discostor is being enabled.");

            var game = await _gameManager.CreateAsync(new GameOptionsData());
            game.DisplayName = "Example game";
            await game.SetPrivacyAsync(true);

            _logger.LogInformation("Created game {0}.", game.Code.Code);
        }

        public override ValueTask DisableAsync()
        {
            _logger.LogInformation("Discostor is being disabled.");
            return default;
        }
    }
}
