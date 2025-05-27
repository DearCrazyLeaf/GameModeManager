// Included libraries
using GameModeManager.Core;
using GameModeManager.Menus;
using CounterStrikeSharp.API;
using GameModeManager.Contracts;
using WASDMenuAPI.Shared.Models;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;
using GameModeManager.CrossCutting;
using GameModeManager.Shared.Models;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;

// Declare namespace
namespace GameModeManager.Features
{
    // Define class
    public class ModeCommands : IPluginDependency<Plugin, Config>
    {
        // Define class dependencies
        private Plugin? _plugin;
        private PluginState _pluginState;
        private StringLocalizer _localizer;
        private ServerManager _serverManager;
        private Config _config = new Config();
        private ILogger<ModeCommands> _logger;
        private CustomVoteManager _customVoteManager;

        // Define class instance
        public ModeCommands(PluginState pluginState, StringLocalizer localizer, CustomVoteManager customVoteManager, ILogger<ModeCommands> logger, ServerManager serverManager)
        {
            _logger = logger;
            _localizer = localizer;
            _pluginState = pluginState;
            _serverManager = serverManager;
            _customVoteManager = customVoteManager;
        }

        // Load config
        public void OnConfigParsed(Config config)
        {
            _config = config;
        }

        // Define on load behavior
        public void OnLoad(Plugin plugin)
        {
            _plugin = plugin;

            if (_config.Commands.Mode)
            {
                plugin.AddCommand("css_mode", "Changes the game mode.", OnModeCommand);
            }
            if (_config.Commands.Modes)
            {
                plugin.AddCommand("css_modes", "Shows a list of game modes.", OnModesCommand);
            }
            plugin.AddCommand("css_gamemode", "Sets the current game mode.", OnGameModeCommand);
        }

        // Define server game mode command handler
        [CommandHelper(minArgs: 1, usage: "<mode>", whoCanExecute: CommandUsage.SERVER_ONLY)]
        public void OnGameModeCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null) 
            {
                IMode? _mode = _pluginState.Game.Modes.FirstOrDefault(m => m.Name.Equals($"{command.ArgByIndex(1)}", StringComparison.OrdinalIgnoreCase) || m.Config.Equals($"{command.ArgByIndex(1)}.cfg", StringComparison.OrdinalIgnoreCase));
                
                if(_mode != null && _pluginState.Game.CurrentMode != _mode)
                {   
                    if (_config.Votes.Enabled && _config.Votes.Maps)
                    {                        
                        // Deregister map votes from old mode
                        _customVoteManager.DeregisterMapVotes();

                        // Set mode
                        _pluginState.Game.CurrentMode = _mode;
        
                        // Register map votes for new mode
                        _customVoteManager.RegisterMapVotes();
                    }
                    else
                    {
                        _pluginState.Game.CurrentMode = _mode;
                    }
                }
                else if (_pluginState.Game.CurrentMode == _mode)
                {
                    _logger.LogWarning($"The mode is already set to {_mode.Name}.");
                }
                else if (_mode == null)
                {
                    _logger.LogWarning($"Cannot find game mode {command.ArgByIndex(1)}.");
                }
            }
        }

        // Define admin change mode command handler
        [RequiresPermissions("@css/changemap")]
        [CommandHelper(minArgs: 1, usage: "<mode>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
        public void OnModeCommand(CCSPlayerController? player, CommandInfo command)
        {
            IMode? _mode = _pluginState.Game.Modes.FirstOrDefault(m => m.Name.Equals($"{command.ArgByIndex(1)}", StringComparison.OrdinalIgnoreCase) || m.Config.Equals($"{command.ArgByIndex(1)}.cfg", StringComparison.OrdinalIgnoreCase));

            if (_mode != null)
            {
                if(player != null)
                {
                    Server.PrintToChatAll(_localizer.LocalizeWithPrefix("changemode.message", player.PlayerName, _mode.Name));
                }
                _serverManager.ChangeMode(_mode);
            }
            else
            {
                command.ReplyToCommand($"Can't find mode: {command.ArgByIndex(1)}");
            }
        }

        // Define admin mode menu command handler
        [RequiresPermissions("@css/changemap")]
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void OnModesCommand(CCSPlayerController? player, CommandInfo command)
        {
            MenuFactory menuFactory = new MenuFactory(_plugin);
            ModeMenus modeMenus = new ModeMenus(_plugin, _pluginState, _localizer, _serverManager, _config);

            if (player != null)
            {
                if (_config.GameModes.Style.Equals("wasd"))
                {
                    IWasdMenu? menu = modeMenus.WasdMenus.MainMenu;

                    if (menu != null)
                    {
                        menuFactory.WasdMenus.OpenMenu(player, menu);
                    }
                }
                else
                {
                    BaseMenu menu = modeMenus.BaseMenus.MainMenu;
                    menu.Title = _localizer.Localize("modes.menu-title");
                    menuFactory.BaseMenus.OpenMenu(menu, player);
                }
            }
        }
    }
}