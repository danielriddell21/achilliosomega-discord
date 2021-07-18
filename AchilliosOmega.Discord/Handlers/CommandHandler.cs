using AchilliosOmega.Discord.Services;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace AchilliosOmega.Discord.Handlers
{
    public class CommandHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly IServiceProvider _services;

        public CommandHandler(IServiceProvider services)
        {
            _commands = services.GetRequiredService<CommandService>();
            _client = services.GetRequiredService<DiscordSocketClient>();
            _services = services;

            _commands.CommandExecuted += CommandExecuted;
            _commands.Log += Log;
            _client.MessageReceived += HandleCommand;
        }

        public async Task InitializeAsync()
        {
            await _commands.AddModulesAsync(
                assembly: Assembly.GetExecutingAssembly(),
                services: _services);
        }

        private Task HandleCommand(SocketMessage socketMessage)
        {
            var argPos = 0;
            if (!(socketMessage is SocketUserMessage message) || message.Author.IsBot || message.Author.IsWebhook || message.Channel is IPrivateChannel)
                return Task.CompletedTask;

            if (message.HasMentionPrefix(_client.CurrentUser, ref argPos))
            {
                var embed = new EmbedBuilder()
                {
                    ImageUrl = "https://cdn.discordapp.com/attachments/138522037181349888/438774275546152960/Ping_Discordapp_GIF-downsized_large.gif"
                };

                message.Channel.SendMessageAsync(embed: embed.Build());
                return Task.CompletedTask;
            }

            if (!message.HasStringPrefix(ConfigHandler.Config.DefaultPrefix, ref argPos))
                return Task.CompletedTask;

            var context = new SocketCommandContext(_client, socketMessage as SocketUserMessage);

            var result = _commands.ExecuteAsync(context, argPos, _services, MultiMatchHandling.Best);
            return result;
        }

        public async Task CommandExecuted(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            if (!command.IsSpecified)
                return;

            if (result.IsSuccess)
                return;

            await LoggingService.LogInformationAsync("CommandHandler", result.ErrorReason);
        }

        private Task Log(LogMessage log)
        {
            Console.WriteLine(log.ToString());

            return Task.CompletedTask;
        }
    }
}
