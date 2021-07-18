using Discord;

namespace AchilliosOmega.Discord.Models
{
    public class CommandResponse
    {
        public CommandResponseType ResponseType { get; set; }
        public Embed ResponseEmbed { get; set; }
        public string ResponseReaction { get; set; }
        public bool IsNowPlayingMessage { get; set; }
    }

    public enum CommandResponseType
    {
        Message,
        ErrorMessage,
        Reaction,
        Nothing
    }
}
