// Included libraries
using GameModeManager.Core;
using GameModeManager.Models;
using GameModeManager.Contracts;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;

// Declare namespace
namespace GameModeManager.Features
{
    // Define class
    public class WarmupCommand : IPluginDependency<Plugin, Config>
    {
        // Define class dependencies
        private PluginState _pluginState;
        private WarmupManager _warmupManager;
        private Config _config = new Config();
        private ILogger<WarmupCommand> _logger;

        // Define class instance
        public WarmupCommand(WarmupManager warmupManager, PluginState pluginState, ILogger<WarmupCommand> logger)
        {
            _logger = logger;
            _pluginState = pluginState;
            _warmupManager = warmupManager;
        }

        // Load config
        public void OnConfigParsed(Config config)
        {
            _config = config;
        }

        // Define on load behavior
        public void OnLoad(Plugin plugin)
        {
            if(_config.Warmup.Enabled)
            {
                plugin.AddCommand("css_endwarmup", "Ends warmup.", OnEndWarmupCommand);
                plugin.AddCommand("css_startwarmup", "Starts warmup.", OnStartWarmupCommand);
                plugin.AddCommand("css_warmupmode", "Sets current warmup mode.", OnWarmupModeCommand);
            }
        }

        // Define command handlers
        [CommandHelper(minArgs: 1, usage: "<mode>",whoCanExecute: CommandUsage.SERVER_ONLY)]
        public void OnWarmupModeCommand(CCSPlayerController? player, CommandInfo command)
        {
            if(player == null)
            {
                if (!_pluginState.WarmupScheduled)
                {
                    if(_warmupManager.ScheduleWarmup(command.ArgByIndex(1)))
                    {
                        _logger.LogInformation($"Warmup Mode: Warmup scheduled.");   
                    } 
                    else
                    {
                        _logger.LogError($"Warmup Mode: {command.ArgByIndex(1)} cannot be found."); 
                    }  
                }
                else
                {
                    _logger.LogWarning("Warmup Mode: Warmup already scheduled.");
                }       
            }
        }

        [RequiresPermissions("@css/cvar")]
        [CommandHelper(minArgs: 0, usage: "*optional <mode>",whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void OnStartWarmupCommand(CCSPlayerController? player, CommandInfo command)
        {
            if(player != null)
            {
                if(command.ArgCount > 1)
                {
                    Mode? _mode = _pluginState.WarmupModes.FirstOrDefault(m => m.Name.Equals(command.ArgByIndex(1), StringComparison.OrdinalIgnoreCase) ||  m.Config.Contains(command.ArgByIndex(1), StringComparison.OrdinalIgnoreCase));
                    if(_mode != null)
                    {
                        _pluginState.WarmupScheduled = true;
                        _warmupManager.StartWarmup(_mode);
                    }
                    else
                    {
                        command.ReplyToCommand($"Unable to find mode: {command.ArgByIndex(1)}");
                    }
                }
                else
                {
                    _pluginState.WarmupScheduled = true;
                    _warmupManager.StartWarmup(_pluginState.WarmupMode);
                }
            }
        }

        [RequiresPermissions("@css/cvar")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void OnEndWarmupCommand(CCSPlayerController? player, CommandInfo command)
        {
            if(player != null)
            {
                if (_pluginState.WarmupRunning)
                {
                    _warmupManager.EndWarmup();
                }
                else
                {
                    _pluginState.WarmupScheduled = false;
                }
            }
        }
    }
}