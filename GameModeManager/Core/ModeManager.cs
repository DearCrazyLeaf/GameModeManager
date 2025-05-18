// Included libraries
using CounterStrikeSharp.API;
using GameModeManager.Models;
using GameModeManager.Contracts;
using GameModeManager.CrossCutting;
using Microsoft.Extensions.Logging;
using GameModeManager.Shared.Models;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

// Declare namespace
namespace GameModeManager.Core
{
    // Define class
    public class ModeManager : IPluginDependency<Plugin, Config>
    {
        // Define class dependencies
        private PluginState _pluginState;
        private ILogger<ModeManager> _logger;
        private Config _config = new Config();

        // Define class instance
        public ModeManager(PluginState pluginState, ILogger<ModeManager> logger)
        {
            _logger = logger;
            _pluginState = pluginState;
        }

        // Define class methods
        public void OnConfigParsed(Config config)
        {
            _config = config;
        }

        public void OnMapStart(string map)
        {
            string _modeConfig = PluginExtensions.RemoveCfgExtension(_pluginState.Game.CurrentMode.Config);
            string _settingsConfig = $"{_modeConfig}_settings.cfg";

            new Timer(.5f, () =>
            {
                Server.ExecuteCommand($"exec {_settingsConfig}");
                Server.ExecuteCommand("mp_restartgame 1");
            });
        }

        public void OnLoad(Plugin plugin)
        {
            foreach (ModeEntry _mode in _config.GameModes.List)
            {
                List<IMapGroup> mapGroups = new List<IMapGroup>();

                foreach (string _mapGroup in _mode.MapGroups)
                {
                    IMapGroup? mapGroup = _pluginState.Game.MapGroups.FirstOrDefault(m => m.Name.Equals(_mapGroup, StringComparison.OrdinalIgnoreCase));

                    if (mapGroup != null)
                    {
                        mapGroups.Add(mapGroup);
                    }
                    else
                    {
                        _logger.LogError($"Cannot find {_mapGroup} in map group list.");
                    }
                }

                IMode? gameMode;

                if (mapGroups.Count > 0)
                {
                    if (_mode.DefaultMap != null)
                    {
                        gameMode = new Mode(_mode.Name, _mode.Config, _mode.DefaultMap, mapGroups);
                    }
                    else
                    {
                        gameMode = new Mode(_mode.Name, _mode.Config, mapGroups);
                    }

                    _pluginState.Game.Modes.Add(gameMode);
                }
                else
                {
                    _logger.LogError($"Cannot create map group list.");
                }
            }

            IMode? currentMode = _pluginState.Game.Modes.FirstOrDefault(m => m.Name.Equals(_config.GameModes.Default.Name, StringComparison.OrdinalIgnoreCase));

            if (currentMode != null)
            {
                _pluginState.Game.CurrentMode = currentMode;
            }
            else
            {
                _logger.LogError($"Cannot find mode {_config.GameModes.Default.Name} in modes list.");
            }
        }
    }
}