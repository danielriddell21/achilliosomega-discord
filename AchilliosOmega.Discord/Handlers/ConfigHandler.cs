using AchilliosOmega.Discord.Models;
using AchilliosOmega.Discord.Services;
using Discord;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace AchilliosOmega.Discord.Handlers
{
    public class ConfigHandler
    {
        public static string ConfigPath { get; set; } = "appsettings.json";
        public static Config Config { get; set; }

        public async Task InitializeAsync()
        {
            string json;
            if (!File.Exists(ConfigPath))
            {
                json = JsonConvert.SerializeObject(GenerateNewConfig(), Formatting.Indented);
                File.WriteAllText("appsettings.json", json, new UTF8Encoding(false));
                await LoggingService.LogAsync("Bot", LogSeverity.Error, "No config file was found. Generating a new one. Please close the & fill in the required section.");
                await Task.Delay(-1);
            }

            json = File.ReadAllText(ConfigPath, new UTF8Encoding(false));
            Config = JsonConvert.DeserializeObject<Config>(json);
        }

        private static Config GenerateNewConfig() => new Config
        {
            DiscordToken = "",
            DefaultPrefix = "!",
            RssLinks = new List<string>(),
            DiscordMusicChannelId = 123456,
            SpotifyApiClientId = "",
            SpotifyApiClientSecret = ""
        };
    }
}