using Discord;

namespace AchilliosOmega.Discord.Handlers
{
    public class EmbedHandler
    {
        public static Embed CreateEmbed(string title, string description, Color color, string url = null, string thumbnailUrl = null, EmbedFooterBuilder footer = null)
        {
            var embed = new EmbedBuilder()
            {
                Title = title,
                Description = description,
                Color = color,
                ThumbnailUrl = thumbnailUrl,
                Url = url,
                Footer = footer
            };
            return embed.Build();
        }

        public static EmbedFooterBuilder CreateFooterEmbed(string text, string iconUrl = null)
        {
            var embed = new EmbedFooterBuilder()
            {
                Text = text,
                IconUrl = iconUrl
            };
            return embed;
        }

        public static Embed CreateErrorEmbed(string message)
        {
            var embed = new EmbedBuilder()
            {
                Title = "Error",
                Description = message,
                Color = Color.DarkRed,
            };
            return embed.Build();
        }
    }
}
