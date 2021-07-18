using AchilliosOmega.Discord.Handlers;
using SpotifyAPI.Web;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AchilliosOmega.Discord.Services
{
    public class SpotifyService
    {
        private static SpotifyClient Client;

        public void Initialize()
        {
            RefreshAccessToken();
        }

        public async static Task<FullTrack> GetTrack(string id)
        {
            return await Client.Tracks.Get(id);
        }

        public async static Task<IAsyncEnumerable<SimpleTrack>> GetAlbumn(string id)
        {
            var album = await Client.Albums.Get(id);
            return Client.Paginate(album.Tracks);
        }

        public async static Task<IAsyncEnumerable<PlaylistTrack<IPlayableItem>>> GetPlaylist(string id)
        {
            var playlist = await Client.Playlists.Get(id);
            return Client.Paginate(playlist.Tracks);
        }

        private static void RefreshAccessToken()
        {
            var config = SpotifyClientConfig
                .CreateDefault()
                .WithAuthenticator(new ClientCredentialsAuthenticator(ConfigHandler.Config.SpotifyApiClientId, ConfigHandler.Config.SpotifyApiClientSecret));

            Client = new SpotifyClient(config);
        }
    }
}
