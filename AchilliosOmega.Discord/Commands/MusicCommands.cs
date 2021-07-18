using AchilliosOmega.Discord.Handlers;
using AchilliosOmega.Discord.Models;
using AchilliosOmega.Discord.Services;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.Threading.Tasks;

namespace AchilliosOmega.Discord.Commands
{
    public class MusicCommands : ModuleBase<SocketCommandContext>
    {
        public AudioService AudioService { get; set; }

        [Command("Play", RunMode=RunMode.Async)]
        [Alias("p")]
        [Summary("Allows user to play a song")]
        public async Task Play([Remainder] string search)
        {
            if (await Validate())
            {
                var commandResponse = await AudioService.Play(Context.User as SocketGuildUser, Context.Guild, search, false, Context.Message.Channel as SocketTextChannel);

                if (commandResponse.IsNowPlayingMessage)
                {
                    await AudioService.RemovePreviousNowPlayingMessage(await RespondAndFetchMessageId(commandResponse), Context.Guild);
                }
                else
                {
                    await Respond(commandResponse);
                }
            }
        }

        [Command("PlayNext", RunMode = RunMode.Async)]
        [Alias("pn")]
        [Summary("Allows user to play a song and queue it next")]
        public async Task PlayNext([Remainder] string search)
        {
            if (await Validate())
            {
                var commandResponse = await AudioService.Play(Context.User as SocketGuildUser, Context.Guild, search, true, Context.Message.Channel as SocketTextChannel);

                if (commandResponse.IsNowPlayingMessage)
                {
                    await AudioService.RemovePreviousNowPlayingMessage(await RespondAndFetchMessageId(commandResponse), Context.Guild);
                }
                else
                {
                    await Respond(commandResponse);
                }
            }
        }

        [Command("Stop")]
        [Summary("Allows user to stop playing")]
        public async Task Stop()
        {
            if (await Validate())
            {
                await Respond(await AudioService.Stop(Context.Guild));
            }
        }

        [Command("Queue")]
        [Alias("q")]
        [Summary("Allows user to view the queue")]
        public async Task List()
        {
            if (await Validate())
            {
                await Respond(await AudioService.Queue(Context.Guild));
            }
        }

        [Command("Skip")]
        [Alias("s")]
        [Summary("Allows user to skip the current song playing")]
        public async Task Skip()
        {
            if (await Validate())
            {
                await Respond(await AudioService.Skip(Context.Guild));
            }
        }

        [Command("Volume")]
        [Summary("Allows user to adjust the volume")]
        public async Task Volume(int volume)
        {
            if (await Validate())
            {
                await Respond(await AudioService.SetVolume(Context.Guild, volume));
            }
        }

        [Command("Pause")]
        [Summary("Allows user to pause the current song")]
        public async Task Pause()
        {
            if (await Validate())
            {
                await Respond(await AudioService.Pause(Context.Guild));
            }
        }

        [Command("Resume")]
        [Summary("Allows user to resume the current song")]
        public async Task Resume()
        {
            if (await Validate())
            {
                await Respond(await AudioService.Resume(Context.Guild));
            }

        }

        [Command("Shuffle")]
        [Summary("Allows user to shuffle the current queue")]
        public async Task Shuffle()
        {
            if (await Validate())
            {
                await Respond(await AudioService.Shuffle(Context.Guild));
            }
        }

        //[Command("Listen")]
        //[Summary("Allows bot to listen to current voicechannel")]
        //public async Task Listen()
        //{
        //    if (await Validate())
        //    {
        //        await Respond(await AudioService.Listen(Context.User as SocketGuildUser, Context.Guild, Context.Message));
        //    }
        //}

        private async Task Respond(CommandResponse response)
        {
            if (response.ResponseType == CommandResponseType.Reaction)
            {
                await Context.Message.AddReactionAsync(new Emoji(response.ResponseReaction));
            }

            if (response.ResponseType == CommandResponseType.Message || response.ResponseType == CommandResponseType.ErrorMessage)
            {
                await ReplyAsync(embed: response.ResponseEmbed);
            }
        }

        private async Task<IUserMessage> RespondAndFetchMessageId(CommandResponse response)
        {
            return await ReplyAsync(embed: response.ResponseEmbed);
        }

        private async Task<bool> Validate()
        {
            if (ConfigHandler.Config.DiscordMusicChannelId != Context.Channel.Id)
            {
                return false;
            }

            if ((Context.User as SocketGuildUser).VoiceChannel == null)
            {
                await ReplyAsync(embed: EmbedHandler.CreateErrorEmbed("You must be connected to a voice channel!"));
                return false;
            }

            return true;
        }
    }
}
