// Included libraries
using GameModeManager.Core;
using GameModeManager.Menus;
using GameModeManager.Contracts;
using WASDMenuAPI.Shared.Models;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;
using GameModeManager.CrossCutting;
using Microsoft.Extensions.Localization;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Commands;

// Declare namespace
namespace GameModeManager.Features
{
    // Define class
    public class NominateCommands : IPluginDependency<Plugin, Config>
    {
        // Define class dependencies
        private Plugin? _plugin;
        private GameRules _gameRules;
        private VoteManager _voteManager;
        private PluginState _pluginState;
        private StringLocalizer _localizer;
        private Config _config = new Config();
        private ILogger<ModeCommands> _logger;
        private NominateManager _nominateManager;
        private MaxRoundsManager _maxRoundsManager;
        private TimeLimitManager _timeLimitManager;
        private VoteOptionManager _voteOptionManager;

        // Define class instance
        public NominateCommands(PluginState pluginState, IStringLocalizer iLocalizer, ILogger<ModeCommands> logger, NominateManager nominateManager, GameRules gameRules, VoteOptionManager voteOptionManager, MaxRoundsManager maxRoundsManager, TimeLimitManager timeLimitManager, VoteManager voteManager)
        {
            _logger = logger;
            _gameRules = gameRules;
            _voteManager = voteManager;
            _pluginState = pluginState;
            _nominateManager = nominateManager;
            _maxRoundsManager = maxRoundsManager;
            _timeLimitManager = timeLimitManager;
            _voteOptionManager = voteOptionManager;
            _localizer = new StringLocalizer(iLocalizer, "rtv.prefix");
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

            if (_pluginState.RTV.Enabled)
            {
                if (_pluginState.RTV.NominationEnabled)
                {
                    plugin.AddCommand("css_nominate", "Nominates a map or game mode.", OnNominateCommand);
                    plugin.RegisterEventHandler<EventPlayerDisconnect>(PlayerDisconnected, HookMode.Pre);
                }
            }
        }

        // Define command handler
        [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void OnNominateCommand(CCSPlayerController? player, CommandInfo command)
        {
            MenuFactory menuFactory = new MenuFactory(_plugin);
            NominateMenus nominateMenus = new NominateMenus(_plugin, _pluginState, _localizer, _voteOptionManager, _nominateManager, _config);

            if (player != null)
            {
                // Check if RTV vote happened already
                if (_pluginState.RTV.EofVoteHappened)
                {
                    if (!_timeLimitManager.UnlimitedTime())
                    {
                        string timeleft = _voteManager.GetTimeLeft();
                        player.PrintToChat(_localizer.LocalizeWithPrefix("rtv.schedule-change", timeleft));
                    }
                    else if (!_maxRoundsManager.UnlimitedRounds)
                    {
                        string roundsleft = _voteManager.GetRoundsLeft();
                        player.PrintToChat(_localizer.LocalizeWithPrefix("rtv.schedule-change", roundsleft));
                    }
                    else
                    {
                        _logger.LogWarning("RTV: No timelimit or max rounds is set for the current map/mode");
                    }
                    return;
                }

                // Check if disabled
                if (_pluginState.RTV.DisableCommands || !_pluginState.RTV.NominationEnabled || _pluginState.RTV.EofVoteHappening)
                {
                    player.PrintToChat(_localizer.LocalizeWithPrefix("general.validation.disabled"));
                    return;
                }

                // Check if warmup
                if (_gameRules.WarmupRunning)
                {
                    if (!_config.RTV.EnabledInWarmup)
                    {
                        player.PrintToChat(_localizer.LocalizeWithPrefix("general.validation.warmup"));
                        return;
                    }
                }
                else if (_timeLimitManager.UnlimitedTime() && !_maxRoundsManager.UnlimitedRounds && _config.RTV.MinRounds > 0 && _config.RTV.MinRounds > _gameRules.TotalRoundsPlayed)
                {
                    player!.PrintToChat(_localizer.LocalizeWithPrefix("general.validation.minimum-rounds", _config.RTV.MinRounds));
                    return;
                }

                // Check if meets minimum players
                if (PlayerExtensions.ValidPlayerCount() < _config!.RTV.MinPlayers)
                {
                    player.PrintToChat(_localizer.LocalizeWithPrefix("general.validation.minimum-players", _config!.RTV.MinPlayers));
                    return;
                }

                // If no option provided, display menu
                string option = command.GetArg(1);

                if (string.IsNullOrEmpty(option))
                {
                    if (_config.RTV.IncludeModes)
                    {
                        if (_config.RTV.Style.Equals("wasd", StringComparison.OrdinalIgnoreCase))
                        {
                            nominateMenus.WasdMenus.Load();
                            IWasdMenu? menu = nominateMenus.WasdMenus.MainMenu;

                            if (menu != null)
                            {
                                menuFactory.WasdMenus.OpenMenu(player, menu);
                            }
                        }
                        else
                        {
                            nominateMenus.BaseMenus.Load();
                            BaseMenu menu = nominateMenus.BaseMenus.MainMenu;
                            menuFactory.BaseMenus.OpenMenu(menu, player);
                        }
                    }
                    else
                    {
                        if (_config.RTV.Style.Equals("wasd", StringComparison.OrdinalIgnoreCase))
                        {
                            nominateMenus.WasdMenus.Load();
                            IWasdMenu? menu = nominateMenus.WasdMenus.MapMenu;

                            if (menu != null)
                            {
                                menuFactory.WasdMenus.OpenMenu(player, menu);
                            }
                        }
                        else
                        {
                            nominateMenus.BaseMenus.Load();
                            BaseMenu menu = nominateMenus.BaseMenus.MapMenu;
                            menuFactory.BaseMenus.OpenMenu(menu, player);
                        }
                    }
                }
                else
                {
                    if (_voteOptionManager.OptionExists(option))
                    {
                        _nominateManager.Nominate(player, option);
                    }
                    else
                    {
                        command.ReplyToCommand($"Cannot find {option}");
                    }
                }
            }
            return;
        }

         // Define command handlers
        [CommandHelper(minArgs: 1, usage: "<true|false>", whoCanExecute: CommandUsage.SERVER_ONLY)]
        public void OnNominateEnabledCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null)
            {
                if (command.ArgByIndex(1).Equals("true", StringComparison.OrdinalIgnoreCase) && !_pluginState.RTV.NominationEnabled)
                {
                    _pluginState.RTV.NominationEnabled = true;
                }
                else if (command.ArgByIndex(1).Equals("false", StringComparison.OrdinalIgnoreCase) && _pluginState.RTV.NominationEnabled)
                {
                    _pluginState.RTV.NominationEnabled = false;
                }
                else
                {
                    command.ReplyToCommand("Valid options are true or false.");
                }

            }
        }

        [CommandHelper(minArgs: 1, usage: "<number>", whoCanExecute: CommandUsage.SERVER_ONLY)]
        public void OnMaxNominationCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (player == null)
            {
                if (int.TryParse(command.ArgByIndex(1), out var max))
                {
                    _pluginState.RTV.MaxNominationWinners = max;
                }
                else
                {
                    command.ReplyToCommand("Please specify a number.");
                }
            }
        }

        public HookResult PlayerDisconnected(EventPlayerDisconnect @event, GameEventInfo @eventInfo)
        {
            var player = @event.Userid;

            if (player != null)
            {
                int userId = player.UserId!.Value;
                if (_nominateManager.ModeNominations.ContainsKey(userId))
                {
                    _nominateManager.ModeNominations.Remove(userId);
                }

                if (_nominateManager.MapNominations.ContainsKey(userId))
                {
                    _nominateManager.MapNominations.Remove(userId);
                }
            }
            return HookResult.Continue;
        }
    }
}