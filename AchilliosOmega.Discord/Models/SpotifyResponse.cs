namespace AchilliosOmega.Discord.Models
{
    public class SpotifyResponse
    {
        public SpotifyResponseType ResponseType { get; set; }
        public string ResponseMessage { get; set; }
    }

    public enum SpotifyResponseType
    {
        Track,
        Albumn,
        Playlist,
        Error,
        Nothing
    }
}
