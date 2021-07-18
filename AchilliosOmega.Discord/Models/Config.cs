using System.Collections.Generic;

namespace AchilliosOmega.Discord.Models
{
    public class Config
    {
        public string DiscordToken { get; set; }
        public string DefaultPrefix { get; set; }
        public List<string> RssLinks { get; set; }
        public ulong DiscordMusicChannelId { get; set; }
        public string SpotifyApiClientId { get; set; }
        public string SpotifyApiClientSecret { get; set; }
    }
}
