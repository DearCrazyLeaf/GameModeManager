// Included libraries
using GameModeManager.Core;
using CounterStrikeSharp.API;
using GameModeManager.Contracts;
using CounterStrikeSharp.API.Core;
using GameModeManager.CrossCutting;
using Microsoft.Extensions.Localization;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;

// Declare namespace
namespace GameModeManager.Features
{
    // Define class
    public class EndOfMapVote : IPluginDependency<Plugin, Config>
    {
        // Define class dependencies
        private Plugin? _plugin;
        private GameRules _gameRules;
        private Config _config = new();
        private PluginState _pluginState;
        private StringLocalizer _localizer;
        private TimeLimitManager _timeLimitManager;
        private MaxRoundsManager _maxRoundsManager;
        private AsyncVoteManager _asyncVoteManager;

        // Define class instance
        public EndOfMapVote(TimeLimitManager timeLimitManager, IStringLocalizer iLocalizer, MaxRoundsManager maxRoundsManager, PluginState pluginState, GameRules gameRules, AsyncVoteManager asyncVoteManager)
        {
            _gameRules = gameRules;
            _pluginState = pluginState;
            _timeLimitManager = timeLimitManager;
            _maxRoundsManager = maxRoundsManager;
            _asyncVoteManager = asyncVoteManager;
            _localizer = new StringLocalizer(iLocalizer, "rtv.prefix");
        }

        // Define class properties
        private Timer? timer;
        private ConVar? gameType;
        private ConVar? gameMode;
        private bool killsReached = false;
        private bool armsRace => gameMode?.GetPrimitiveValue<int>() == 0 && gameType?.GetPrimitiveValue<int>() == 1;
        private bool deathMatch => gameMode?.GetPrimitiveValue<int>() == 2 && gameType?.GetPrimitiveValue<int>() == 1;

        // Load config
        public void OnConfigParsed(Config config)
        {
            _config = config;
        }

        // Define on load behavior
        public void OnLoad(Plugin plugin)
        {
            _plugin = plugin;

            // Register event handlers
            if(_config.RTV.Enabled)
            {
                plugin.RegisterEventHandler<EventRoundStart>(EventRoundStartHandler);
                plugin.RegisterEventHandler<EventPlayerDeath>(EventPlayerDeathHandler);
                plugin.RegisterEventHandler<EventRoundAnnounceMatchStart>(EventRoundAnnounceMatchStartHandler);
            }
        }

        // Define on map start behavior
        public void OnMapStart(string map)
        {
            KillTimer();
            killsReached = false;
            gameMode = ConVar.Find("game_mode");
            gameType = ConVar.Find("game_type");
        }

        // Define class methods
        void KillTimer()
        {
            timer?.Kill();
            timer = null;
        }

         public void StartVote()
        {
            if (_pluginState.EndOfMapVote && !_pluginState.EofVoteHappened && !_pluginState.EofVoteHappening)
            {
                KillTimer();
                _asyncVoteManager.StartVote(null, null);
            }
        }

        bool CheckTimeLeft()
        {
            return !_timeLimitManager.UnlimitedTime() && _timeLimitManager.TimeRemaining() <= _pluginState.RTVSecondsBeforeEnd;
        }

        bool CheckMaxRounds()
        {
            if (_maxRoundsManager.UnlimitedRounds)
            {
                return false;
            }
            if (_maxRoundsManager.RemainingRounds <= _pluginState.RTVRoundsBeforeEnd)
            {
                return true;
            }
            return _maxRoundsManager.CanClinch && _maxRoundsManager.RemainingWins <= _pluginState.RTVRoundsBeforeEnd;
        }

        public void StartTimer()
        {
            KillTimer();
            if (_pluginState.EndOfMapVote && !_pluginState.EofVoteHappened && !_pluginState.EofVoteHappening)
            {
                if (!_timeLimitManager.UnlimitedTime())
                {
                    timer = _plugin?.AddTimer(1.0F, () =>
                    {
                        if (_gameRules != null && !_gameRules.WarmupRunning && !_pluginState.DisableCommands && _timeLimitManager.TimeRemaining() > 0)
                        {
                            if (CheckTimeLeft())
                            {
                                Server.PrintToChatAll(_localizer.LocalizeWithPrefix("rtv.eofmapvote.message"));
                                StartVote();
                            }
                        }
                    }, TimerFlags.REPEAT);
                }
                else if (!_maxRoundsManager.UnlimitedRounds)
                {
                    timer = _plugin?.AddTimer(1.0F, () =>
                    {
                        if (_gameRules != null && !_gameRules.WarmupRunning && !_pluginState.DisableCommands && _timeLimitManager.TimeRemaining() > 0 && _pluginState.TimeLimitCustom)
                        {
                            if (CheckTimeLeft())
                            {
                                Server.PrintToChatAll(_localizer.LocalizeWithPrefix("rtv.eofmapvote.message"));
                                StartVote();
                            }
                        }
                    }, TimerFlags.REPEAT);
                }
            }
        }

        // Define class event handlers
        public HookResult EventPlayerDeathHandler(EventPlayerDeath @event, GameEventInfo info)
        {
            if (armsRace & !killsReached)
            {
                var player = Extensions.ValidPlayers(true).OrderByDescending(p => p.Score).FirstOrDefault();
                if (player?.ActionTrackingServices?.MatchStats?.Kills >= _pluginState.RTVKillsBeforeEnd)
                {
                    killsReached = true;
                    Server.PrintToChatAll(_localizer.LocalizeWithPrefix("rtv.armsrace.message", player.PlayerName));
                    Server.PrintToChatAll(_localizer.LocalizeWithPrefix("rtv.eofmapvote.message"));
                    StartVote();
                }
            }
            return HookResult.Continue;
        }

        public HookResult EventRoundStartHandler(EventRoundStart @event, GameEventInfo info)
        {
            if (!_pluginState.DisableCommands && !_gameRules.WarmupRunning && CheckMaxRounds())
            {
                StartVote();
            }
            else if (deathMatch || armsRace)
            {
                StartTimer();
            }
            return HookResult.Continue;  
        }
        
        public HookResult EventRoundAnnounceMatchStartHandler(EventRoundAnnounceMatchStart @event, GameEventInfo info)
        {
            StartTimer();
            return HookResult.Continue;
        }
    }
}