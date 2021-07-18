using AchilliosOmega.Discord.Handlers;
using AchilliosOmega.Discord.Models;
using Discord;
using Discord.WebSocket;
using SpotifyAPI.Web;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Victoria;
using Victoria.Enums;
using Victoria.EventArgs;

using System.IO;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

namespace AchilliosOmega.Discord.Services
{
    public class AudioService
    {
        private readonly LavaNode _lavaNode;
        private readonly DiscordSocketClient _discordClient;
        private readonly ConcurrentDictionary<ulong, CancellationTokenSource> _disconnectTokens;

        private CommandResponse response;
        private IUserMessage nowPlayingMessage;

        public AudioService(LavaNode lavaNode, DiscordSocketClient discordClient)
        {
            _lavaNode = lavaNode;
            _discordClient = discordClient;
            _disconnectTokens = new ConcurrentDictionary<ulong, CancellationTokenSource>();
        }

        public async Task<CommandResponse> Play(SocketGuildUser user, IGuild guild, string query, bool playNext, SocketTextChannel textChannel = null)
        {
            try
            {
                if (!_lavaNode.HasPlayer(guild))
                {
                    await _lavaNode.JoinAsync(user.VoiceChannel, textChannel);
                    await LoggingService.LogInformationAsync("AudioService", $"Bot has joined channel {user.VoiceChannel.Id}.");
                }

                var player = _lavaNode.GetPlayer(guild);
                LavaTrack track;

                var spotifyResponse = await CheckUrlForSpotify(user, guild, query, playNext);

                switch (spotifyResponse.ResponseType)
                {
                    case SpotifyResponseType.Track:
                        query = spotifyResponse.ResponseMessage;
                        break;
                    case SpotifyResponseType.Albumn:
                        return new CommandResponse()
                        {
                            ResponseType = CommandResponseType.Message,
                            ResponseEmbed = EmbedHandler.CreateEmbed("Added Albumn", spotifyResponse.ResponseMessage, Color.Blue)
                        };
                    case SpotifyResponseType.Playlist:
                        return new CommandResponse()
                        {
                            ResponseType = CommandResponseType.Message,
                            ResponseEmbed = EmbedHandler.CreateEmbed("Added Playlist", spotifyResponse.ResponseMessage, Color.Blue)
                        };
                    case SpotifyResponseType.Error:
                        return new CommandResponse()
                        {
                            ResponseType = CommandResponseType.ErrorMessage,
                            ResponseEmbed = EmbedHandler.CreateErrorEmbed(string.IsNullOrEmpty(spotifyResponse.ResponseMessage)
                                ? "I have hit a snag! Please try again."
                                : spotifyResponse.ResponseMessage)
                        };
                    case SpotifyResponseType.Nothing:
                        break;
                }

                var search = Uri.IsWellFormedUriString(query, UriKind.Absolute) ?
                    await _lavaNode.SearchAsync(query) :
                    await _lavaNode.SearchYouTubeAsync(query);

                await LoggingService.LogInformationAsync("AudioService", $"Searching for requested track: {query}.");

                if (search.LoadStatus == LoadStatus.NoMatches)
                {
                    response = new CommandResponse()
                    {
                        ResponseType = CommandResponseType.ErrorMessage,
                        ResponseEmbed = EmbedHandler.CreateErrorEmbed($"I wasn't able to find anything for '{query}'.")
                    };
                    return response;
                }

                track = search.Tracks.FirstOrDefault();

                if (player.Track != null && player.PlayerState is PlayerState.Playing || player.PlayerState is PlayerState.Paused)
                {
                    if (playNext)
                    {
                        var queue = player.Queue.Prepend(track).ToArray();

                        player.Queue.Clear();

                        foreach (var song in queue)
                        {
                            player.Queue.Enqueue(song);
                        }
                        await LoggingService.LogInformationAsync("AudioService", $"Enqueued new song to play next: {track.Id}");
                    }
                    else
                    {
                        player.Queue.Enqueue(track);
                        await LoggingService.LogInformationAsync("AudioService", $"Enqueued new song: {track.Id}");
                    }

                    response = new CommandResponse()
                    {
                        ResponseType = CommandResponseType.Message,
                        ResponseEmbed = EmbedHandler.CreateEmbed(track.Title, "Succesfully added to queue", Color.Blue,
                            url: track.Url,
                            footer: EmbedHandler.CreateFooterEmbed($"Added by {user.Username}", user.GetAvatarUrl()))
                    };
                    return response;
                }

                await player.PlayAsync(track);
                await LoggingService.LogInformationAsync("AudioService", $"Playing new song: {track.Id}");
                await _discordClient.SetGameAsync(track.Title, type: ActivityType.Playing);

                response = new CommandResponse()
                {
                    ResponseType = CommandResponseType.Message,
                    ResponseEmbed = EmbedHandler.CreateEmbed("Now Playing", $"[{track.Title}]({track.Url})", Color.Blue,
                        thumbnailUrl: await track.FetchArtworkAsync(),
                        footer: EmbedHandler.CreateFooterEmbed($"Added by {user.Username}", user.GetAvatarUrl())),
                    IsNowPlayingMessage = true
                };
                return response;

            }
            catch (Exception ex)
            {
                await LoggingService.LogInformationAsync(ex.Source, ex.Message);
                response = new CommandResponse()
                {
                    ResponseType = CommandResponseType.ErrorMessage,
                    ResponseEmbed = EmbedHandler.CreateErrorEmbed("I have hit a snag! Please try again.")
                };
                return response;
            }
        }

        public async Task<CommandResponse> Queue(IGuild guild)
        {
            try
            {
                var descriptionBuilder = new StringBuilder();

                var player = _lavaNode.GetPlayer(guild);
                if (player == null)
                {
                    response = new CommandResponse()
                    {
                        ResponseType = CommandResponseType.ErrorMessage,
                        ResponseEmbed = EmbedHandler.CreateErrorEmbed("Nothing is being played right now!")
                    };
                    return response;
                }

                if (player.PlayerState is PlayerState.Playing)
                {
                    if (player.Queue.Count < 1 && player.Track != null)
                    {
                        response = new CommandResponse()
                        {
                            ResponseType = CommandResponseType.Message,
                            ResponseEmbed = EmbedHandler.CreateEmbed("Now Playing", $"[{player.Track.Title}]({player.Track.Url})", Color.Blue,
                                thumbnailUrl: await player.Track.FetchArtworkAsync())
                        };
                        return response;
                    }
                    else
                    {
                        var trackNum = 2;
                        foreach (LavaTrack track in player.Queue.Take(10))
                        {
                            descriptionBuilder.Append($"{trackNum}: [{track.Title}]({track.Url})\n");
                            trackNum++;
                        }

                        if (player.Queue.Count > 10)
                        {
                            descriptionBuilder.Append($"\n And {player.Queue.Count - 10} more...");
                        }

                        response = new CommandResponse()
                        {
                            ResponseType = CommandResponseType.Message,
                            ResponseEmbed = EmbedHandler.CreateEmbed("Queue",
                                $"Now Playing: [{player.Track.Title}]({player.Track.Url}) \n{descriptionBuilder}", Color.Blue)
                        };
                        return response;
                    }
                }
                else
                {
                    response = new CommandResponse()
                    {
                        ResponseType = CommandResponseType.ErrorMessage,
                        ResponseEmbed = EmbedHandler.CreateErrorEmbed("Nothing seems to be playing anything right now.")
                    };
                    return response;
                }
            }
            catch (Exception ex)
            {
                await LoggingService.LogInformationAsync(ex.Source, ex.Message);
                response = new CommandResponse()
                {
                    ResponseType = CommandResponseType.ErrorMessage,
                    ResponseEmbed = EmbedHandler.CreateErrorEmbed("I have hit a snag! Please try again.")
                };
                return response;
            }
        }

        public async Task<CommandResponse> Skip(IGuild guild)
        {
            try
            {
                var player = _lavaNode.GetPlayer(guild);
                if (player == null)
                {
                    response = new CommandResponse()
                    {
                        ResponseType = CommandResponseType.ErrorMessage,
                        ResponseEmbed = EmbedHandler.CreateErrorEmbed("Nothing is being played right now!")
                    };
                    return response;
                };

                if (player.Queue.Count < 1)
                {
                    response = new CommandResponse()
                    {
                        ResponseType = CommandResponseType.ErrorMessage,
                        ResponseEmbed = EmbedHandler.CreateErrorEmbed("Unable To skip a track as there is nothing in the queue.")
                    };
                    return response;
                }
                else
                {
                    try
                    {
                        await player.SkipAsync();
                        await LoggingService.LogInformationAsync("AudioService", $"Skipping current song: {player.Track.Id}.");

                        var embed = EmbedHandler.CreateEmbed("Now Playing", $"[{player.Track.Title}]({player.Track.Url})", Color.Blue,
                            thumbnailUrl: await player.Track.FetchArtworkAsync());

                        await player.TextChannel.SendMessageAsync(embed: embed);
                        await _discordClient.SetGameAsync(player.Track.Title, type: ActivityType.Playing);

                        response = new CommandResponse()
                        {
                            ResponseType = CommandResponseType.Reaction,
                            ResponseReaction = "\U0001F44C"
                        };
                        return response;
                    }
                    catch (Exception ex)
                    {
                        await LoggingService.LogInformationAsync(ex.Source, ex.Message);
                        response = new CommandResponse()
                        {
                            ResponseType = CommandResponseType.ErrorMessage,
                            ResponseEmbed = EmbedHandler.CreateErrorEmbed("I have hit a snag! Please try again.")
                        };
                        return response;
                    }

                }
            }
            catch (Exception ex)
            {
                await LoggingService.LogInformationAsync(ex.Source, ex.Message);
                response = new CommandResponse()
                {
                    ResponseType = CommandResponseType.ErrorMessage,
                    ResponseEmbed = EmbedHandler.CreateErrorEmbed("I have hit a snag! Please try again.")
                };
                return response;
            }
        }

        public async Task<CommandResponse> Stop(IGuild guild)
        {
            try
            {
                var player = _lavaNode.GetPlayer(guild);

                if (player == null)
                {
                    response = new CommandResponse()
                    {
                        ResponseType = CommandResponseType.ErrorMessage,
                        ResponseEmbed = EmbedHandler.CreateErrorEmbed("Nothing is being played right now!")
                    };
                    return response;
                }

                if (player.PlayerState is PlayerState.Playing)
                {
                    await player.StopAsync();
                    await LoggingService.LogInformationAsync("AudioService", $"Playback has stopped");
                }

                await _lavaNode.LeaveAsync(player.VoiceChannel);
                await _discordClient.SetGameAsync(string.Empty);
                await LoggingService.LogInformationAsync("AudioService", $"Bot has left the voice channel.");

                response = new CommandResponse()
                {
                    ResponseType = CommandResponseType.Reaction,
                    ResponseReaction = "\U0001F6D1"
                };
                return response;
            }
            catch (Exception ex)
            {
                await LoggingService.LogInformationAsync(ex.Source, ex.Message);
                response = new CommandResponse()
                {
                    ResponseType = CommandResponseType.ErrorMessage,
                    ResponseEmbed = EmbedHandler.CreateErrorEmbed("I have hit a snag! Please try again.")
                };
                return response;
            }
        }

        public async Task<CommandResponse> SetVolume(IGuild guild, int volume)
        {
            if (volume > 150 || volume <= 0)
            {
                response = new CommandResponse()
                {
                    ResponseType = CommandResponseType.ErrorMessage,
                    ResponseEmbed = EmbedHandler.CreateErrorEmbed("Volume must be between 1 and 150!")
                };
                return response;
            }

            try
            {
                var player = _lavaNode.GetPlayer(guild);

                await LoggingService.LogInformationAsync("AudioService", $"Player volume has been set to {volume}%.");

                if (player.Volume > volume)
                {
                    response = new CommandResponse()
                    {
                        ResponseType = CommandResponseType.Reaction,
                        ResponseReaction = "\U0001F509"
                    };
                }
                else
                {
                    response = new CommandResponse()
                    {
                        ResponseType = CommandResponseType.Reaction,
                        ResponseReaction = "\U0001F50A"
                    };
                }

                await player.UpdateVolumeAsync((ushort)volume);
                return response;
            }
            catch (InvalidOperationException ex)
            {
                await LoggingService.LogInformationAsync(ex.Source, ex.Message);
                response = new CommandResponse()
                {
                    ResponseType = CommandResponseType.ErrorMessage,
                    ResponseEmbed = EmbedHandler.CreateErrorEmbed("I have hit a snag! Please try again.")
                };
                return response;
            }
        }

        public async Task<CommandResponse> Pause(IGuild guild)
        {
            try
            {
                var player = _lavaNode.GetPlayer(guild);
                if (!(player.PlayerState is PlayerState.Playing))
                {
                    response = new CommandResponse()
                    {
                        ResponseType = CommandResponseType.ErrorMessage,
                        ResponseEmbed = EmbedHandler.CreateErrorEmbed("Nothing is being played right now!")
                    };
                    return response;
                }

                await player.PauseAsync();
                await LoggingService.LogInformationAsync("AudioService", $"Playback has been paused.");

                response = new CommandResponse()
                {
                    ResponseType = CommandResponseType.Reaction,
                    ResponseReaction = "\U0001F44D"
                };
                return response;
            }
            catch (InvalidOperationException ex)
            {
                await LoggingService.LogInformationAsync(ex.Source, ex.Message);
                response = new CommandResponse()
                {
                    ResponseType = CommandResponseType.ErrorMessage,
                    ResponseEmbed = EmbedHandler.CreateErrorEmbed("I have hit a snag! Please try again.")
                };
                return response;
            }
        }

        public async Task<CommandResponse> Resume(IGuild guild)
        {
            try
            {
                var player = _lavaNode.GetPlayer(guild);

                if (!(player.PlayerState is PlayerState.Paused))
                {
                    response = new CommandResponse()
                    {
                        ResponseType = CommandResponseType.ErrorMessage,
                        ResponseEmbed = EmbedHandler.CreateErrorEmbed("Nothing is being played right now!")
                    };
                    return response;
                }

                await player.ResumeAsync();
                await LoggingService.LogInformationAsync("AudioService", $"Playback has been resumed.");

                response = new CommandResponse()
                {
                    ResponseType = CommandResponseType.Reaction,
                    ResponseReaction = "\U0001F44D"
                };
                return response;
            }
            catch (InvalidOperationException ex)
            {
                await LoggingService.LogInformationAsync(ex.Source, ex.Message);
                response = new CommandResponse()
                {
                    ResponseType = CommandResponseType.ErrorMessage,
                    ResponseEmbed = EmbedHandler.CreateErrorEmbed("I have hit a snag! Please try again.")
                };
                return response;
            }
        }

        public async Task<CommandResponse> Shuffle(IGuild guild)
        {
            try
            {
                var player = _lavaNode.GetPlayer(guild);

                if (player == null)
                {
                    response = new CommandResponse()
                    {
                        ResponseType = CommandResponseType.ErrorMessage,
                        ResponseEmbed = EmbedHandler.CreateErrorEmbed("Nothing is being played right now!")
                    };
                    return response;
                }

                if (player.PlayerState is PlayerState.Playing)
                {
                    if (player.Queue.Count < 1 && player.Track != null)
                    {
                        response = new CommandResponse()
                        {
                            ResponseType = CommandResponseType.Message,
                            ResponseEmbed = EmbedHandler.CreateErrorEmbed("Not enough songs in queue to shuffle.")
                        };
                        return response;
                    }
                    else
                    {
                        player.Queue.Shuffle();
                        await LoggingService.LogInformationAsync("AudioService", $"Player queue has been shuffled.");

                        response = new CommandResponse()
                        {
                            ResponseType = CommandResponseType.Reaction,
                            ResponseReaction = "\U0001F4AA"
                        };
                        return response;
                    }
                }
                else
                {
                    response = new CommandResponse()
                    {
                        ResponseType = CommandResponseType.ErrorMessage,
                        ResponseEmbed = EmbedHandler.CreateErrorEmbed("Nothing seems to be playing anything right now.")
                    };
                    return response;
                }
            }
            catch (Exception ex)
            {
                await LoggingService.LogInformationAsync(ex.Source, ex.Message);
                response = new CommandResponse()
                {
                    ResponseType = CommandResponseType.ErrorMessage,
                    ResponseEmbed = EmbedHandler.CreateErrorEmbed("I have hit a snag! Please try again.")
                };
                return response;
            }
        }

        public async Task<CommandResponse> Listen(SocketGuildUser user, IGuild guild, SocketUserMessage message)
        {
            if (!_lavaNode.HasPlayer(guild))
            {
                await _lavaNode.JoinAsync(user.VoiceChannel, message.Channel as SocketTextChannel);
                await LoggingService.LogInformationAsync("AudioService", $"Bot has joined channel {user.VoiceChannel.Id}.");
            }

            await message.AddReactionAsync(new Emoji("\U0001F442"));
            await LoggingService.LogInformationAsync("AudioService", $"Bot is now listening to channel {user.VoiceChannel.Id}.");

            var speechConfig = SpeechConfig.FromSubscription("<paste-your-subscription-key>", "<paste-your-region>");
            var voiceChannelUsers = await _lavaNode.GetPlayer(guild).VoiceChannel.GetUsersAsync().ToListAsync();

            string recognisedMessage = string.Empty;

            foreach (SocketGuildUser voiceChannelUser in voiceChannelUsers[0] )
            {
                if (voiceChannelUser.IsBot)
                    continue;

                using (var ffmpeg = CreateFfmpegOut())
                using (var ffmpegOutStdinStream = ffmpeg.StandardInput.BaseStream)
                {
                    try
                    {
                        var buffer = new byte[3840];
                        do
                        {
                            await ffmpegOutStdinStream.WriteAsync(buffer, 0, buffer.Length);
                            recognisedMessage = await FromStream(speechConfig, buffer);
                            await ffmpegOutStdinStream.FlushAsync();
                        }
                        while (await voiceChannelUser.AudioStream.ReadAsync(buffer, 0, buffer.Length) > 0);
                    }
                    catch (Exception ex)
                    {
                        await LoggingService.LogInformationAsync(ex.Source, ex.Message);
                        response = new CommandResponse()
                        {
                            ResponseType = CommandResponseType.ErrorMessage,
                            ResponseEmbed = EmbedHandler.CreateErrorEmbed("I have hit a snag! Please try again.")
                        };
                        return response;
                    }
                    finally
                    {
                        await ffmpegOutStdinStream.FlushAsync();
                        ffmpegOutStdinStream.Close();
                        ffmpeg.Close();
                    }
                }
            }
            response = new CommandResponse()
            {
                ResponseType = CommandResponseType.Message,
                ResponseEmbed = EmbedHandler.CreateEmbed("Did you say", recognisedMessage, Color.Blue)
            };
            return response;
        }

        private async Task<string> FromStream(SpeechConfig speechConfig, byte[] buffer)
        {
            using var audioInputStream = AudioInputStream.CreatePushStream();
            using var audioConfig = AudioConfig.FromStreamInput(audioInputStream);
            using var recognizer = new SpeechRecognizer(speechConfig, audioConfig);

            do
            {
                audioInputStream.Write(buffer, buffer.Length);
            } while (buffer.Length > 0);

            var result = await recognizer.RecognizeOnceAsync();
            switch (result.Reason)
            {
                case ResultReason.RecognizedSpeech:
                    return result.Text;
                case ResultReason.Canceled:
                    var cancellation = CancellationDetails.FromResult(result);
                    return $"Sorry I was unable to recognise what you said: {cancellation.Reason}";
                default:
                    return "Sorry I did not understand"; ;
            }
        }
        public Process CreateFfmpegOut()
        {
            return Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel panic -ac 2 -f s16le -ar 48000 -i pipe:0 -acodec pcm_u8 -ar 22050 -f wav -",
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true
            });
        }

        public async Task TrackEnded(TrackEndedEventArgs args)
        {
            if (!args.Reason.ShouldPlayNext())
            {
                return;
            }

            if (!args.Player.Queue.TryDequeue(out var queueable))
            {
                await InitiateDisconnectAsync(args.Player, TimeSpan.FromSeconds(60));
                return;
            }

            if (!(queueable is LavaTrack track))
            {
                return;
            }

            await args.Player.PlayAsync(track);
            await _discordClient.SetGameAsync(args.Player.Track.Title, type: ActivityType.Playing);

            var embed = EmbedHandler.CreateEmbed("Now Playing", $"[{args.Player.Track.Title}]({args.Player.Track.Url})", Color.Blue,
                thumbnailUrl: await args.Player.Track.FetchArtworkAsync());

            var message = await args.Player.TextChannel.SendMessageAsync(embed: embed);
            await RemovePreviousNowPlayingMessage(message, textChannel: args.Player.TextChannel);

        }

        public async Task TrackStarted(TrackStartEventArgs args)
        {
            if (!_disconnectTokens.TryGetValue(args.Player.VoiceChannel.Id, out var value))
            {
                return;
            }

            if (value.IsCancellationRequested)
            {
                return;
            }

            value.Cancel(true);
            await LoggingService.LogInformationAsync("AudioService", "Auto disconnect has been cancelled!");
        }

        private async Task InitiateDisconnectAsync(LavaPlayer player, TimeSpan timeSpan)
        {
            if (!_disconnectTokens.TryGetValue(player.VoiceChannel.Id, out var value))
            {
                value = new CancellationTokenSource();
                _disconnectTokens.TryAdd(player.VoiceChannel.Id, value);
            }
            else if (value.IsCancellationRequested)
            {
                _disconnectTokens.TryUpdate(player.VoiceChannel.Id, new CancellationTokenSource(), value);
                value = _disconnectTokens[player.VoiceChannel.Id];
            }

            await LoggingService.LogInformationAsync("AudioService", $"Auto disconnect initiated! Disconnecting in {timeSpan}...");
            var isCancelled = SpinWait.SpinUntil(() => value.IsCancellationRequested, timeSpan);
            if (isCancelled)
            {
                return;
            }

            await _lavaNode.LeaveAsync(player.VoiceChannel);
            await _discordClient.SetGameAsync(string.Empty);
            await LoggingService.LogInformationAsync("AudioService", $"Bot has left the voice channel.");
        }

        private async Task<SpotifyResponse> CheckUrlForSpotify(SocketGuildUser user, IGuild guild, string url, bool playNext)
        {
            if (url.Contains("open.spotify.com"))
            {
                var spotifyUrl = "spotify:" + Regex.Replace(url, @"(http[s]?:\/\/)?(open.spotify.com)\/", string.Empty).Replace("/", ":");
                url = Regex.Replace(spotifyUrl, @"\?.*", string.Empty);
            }

            if (url.StartsWith("spotify:"))
            {
                var parts = url.Split(":");
                try
                {
                    if (parts.Any(p => p == "track"))
                    {
                        var track = await SpotifyService.GetTrack(parts.Last());
                        return new SpotifyResponse()
                        {
                            ResponseType = SpotifyResponseType.Track,
                            ResponseMessage = string.Join(" ", track.Artists.First().Name, track.Name)
                        };
                    }
                    else if (parts.Any(p => p == "album"))
                    {
                        if (playNext)
                        {
                            return new SpotifyResponse()
                            {
                                ResponseType = SpotifyResponseType.Error,
                                ResponseMessage = "You Cannot queue a playlist or album next. Please use !play instead"
                            };
                        }

                        var albumn = await SpotifyService.GetAlbumn(parts.Last());
                        await foreach (var track in albumn)
                        {
                            var trackRequest = string.Join(" ", track.Artists.First().Name, track.Name);
                            await LoggingService.LogInformationAsync("AudioService", $"Processing song from albumn: {trackRequest}");
                            await Play(user, guild, trackRequest, false);
                        }
                        return new SpotifyResponse()
                        {
                            ResponseType = SpotifyResponseType.Albumn,
                            ResponseMessage = "Succesfully all songs in the albumn to queue"
                        };
                    }
                    else if (parts.Any(p => p == "playlist"))
                    {
                        if (playNext)
                        {
                            return new SpotifyResponse()
                            {
                                ResponseType = SpotifyResponseType.Error,
                                ResponseMessage = "You Cannot queue a playlist or album next. Please use !play instead"
                            };
                        }

                        var playlist = await SpotifyService.GetPlaylist(parts.Last());
                        await foreach (var playlistTrack in playlist)
                        {
                            if (playlistTrack.Track is FullTrack track)
                            {
                                var trackRequest = string.Join(" ", track.Artists.First().Name, track.Name);
                                await LoggingService.LogInformationAsync("AudioService", $"Processing song from albumn: {trackRequest}");
                                await Play(user, guild, trackRequest, false);
                            }
                        }

                        return new SpotifyResponse()
                        {
                            ResponseType = SpotifyResponseType.Playlist,
                            ResponseMessage = "Succesfully all songs in the playlist to queue"
                        };
                    }
                    else
                    {
                        return new SpotifyResponse()
                        {
                            ResponseType = SpotifyResponseType.Error
                        };
                    }
                }
                catch (Exception ex)
                {
                    await LoggingService.LogInformationAsync(ex.Source, ex.Message);
                    return new SpotifyResponse()
                    {
                        ResponseType = SpotifyResponseType.Error
                    };
                }
            }

            return new SpotifyResponse()
            {
                ResponseType = SpotifyResponseType.Nothing
            };
        }

        public async Task RemovePreviousNowPlayingMessage(IUserMessage message, IGuild guild = null, ITextChannel textChannel = null)
        {
            if (nowPlayingMessage != null)
            {
                if (textChannel == null)
                {
                    textChannel = _lavaNode.GetPlayer(guild).TextChannel;
                }

                await textChannel.DeleteMessageAsync(nowPlayingMessage);
            }

            nowPlayingMessage = message;
        }
    }
}
