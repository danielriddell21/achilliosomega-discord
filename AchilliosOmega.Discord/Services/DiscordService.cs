using AchilliosOmega.Discord.Handlers;
using AchilliosOmega.Discord.Services;
using AchilliosOmega.Discord.Services.TaskScheduler;
using AchilliosOmega.Discord.Services.TaskScheduler.Jobs;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using System;
using System.Threading.Tasks;
using Victoria;

namespace AchilliosOmega.Discord
{
    public class DiscordService
    {
        private readonly ServiceProvider _services;
        private readonly DiscordSocketClient _client;
        private readonly LavaNode _audioClient;
        private readonly CommandHandler _commandHandler;
        private readonly AudioService _audioService;
        private readonly ConfigHandler _config;
        private readonly TaskSchedulerService _taskScheduler;
        private readonly SpotifyService _spotify;

        public DiscordService()
        {
            _services = ConfigureServices();
            _client = _services.GetRequiredService<DiscordSocketClient>();
            _audioClient = _services.GetRequiredService<LavaNode>();
            _commandHandler = _services.GetRequiredService<CommandHandler>();
            _audioService = _services.GetRequiredService<AudioService>();
            _config = _services.GetRequiredService<ConfigHandler>();
            _taskScheduler = _services.GetRequiredService<TaskSchedulerService>();
            _spotify = _services.GetRequiredService<SpotifyService>();
        }

        public async Task MainAsync()
        {
            await _config.InitializeAsync();
            await _taskScheduler.InitializeAsync();
            _spotify.Initialize();

            _audioClient.OnLog += Log;
            _audioClient.OnTrackEnded += _audioService.TrackEnded;
            _audioClient.OnTrackStarted += _audioService.TrackStarted;

            _client.Log += Log;
            _client.Ready += OnReady;

            await _client.LoginAsync(TokenType.Bot, ConfigHandler.Config.DiscordToken);
            await _client.StartAsync();

            await _commandHandler.InitializeAsync();

            await Task.Delay(-1);
        }

        private async Task Log(LogMessage logMessage)
        {
            await LoggingService.LogAsync(logMessage.Source, logMessage.Severity, logMessage.Message, logMessage.Exception);
        }

        private async Task OnReady()
        {
            try
            {
                if (!_audioClient.IsConnected)
                {
                    await _audioClient.ConnectAsync();
                }
            }
            catch (Exception ex)
            {
                await LoggingService.LogInformationAsync(ex.Source, ex.Message);
            }
        }

        private ServiceProvider ConfigureServices()
        {
            return new ServiceCollection()
                .AddSingleton<DiscordSocketClient>()
                .AddSingleton<CommandService>()
                .AddSingleton<CommandHandler>()
                .AddSingleton<AudioService>()
                .AddSingleton<RssService>()
                .AddSingleton<SpotifyService>()
                .AddSingleton<ConfigHandler>()
                .AddSingleton<IJobFactory, SingletonJobFactory>()
                .AddSingleton<ISchedulerFactory, StdSchedulerFactory>()
                .AddSingleton<RssJob>()
                .AddSingleton(new JobSchedule(
                        jobType: typeof(RssJob),
                        cronExpression: "0 0 17 * * ?"))
                .AddSingleton<TaskSchedulerService>()
                .AddLavaNode(x =>
                {
                    x.SelfDeaf = false;
                    x.LogSeverity = LogSeverity.Info;
                    x.Hostname = "192.168.0.20";
                    x.Authorization = "youshallpass";
                })
                .BuildServiceProvider();
        }
    }
}
