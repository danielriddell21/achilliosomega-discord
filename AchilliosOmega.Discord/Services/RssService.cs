using AchilliosOmega.Discord.Handlers;
using CodeHollow.FeedReader;
using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AchilliosOmega.Discord.Services
{
    public class RssService
    {
        private async Task<Feed> FetchFeedFromUrl(string url) => await FeedReader.ReadAsync(url);

        private List<FeedItem> CheckForNewResults(Feed feed) => feed.Items.Where(i => i.PublishingDate.Value.Date == DateTime.Now.Date).ToList();

        private Embed RssItemToDiscordEmbed(FeedItem feedItem)
        {
            var title = feedItem.Title;
            var url = feedItem.Link.Replace("![CDATA[", "").Replace("]]", "");
            var description = Regex.Replace(feedItem.Description, "<.*?>", string.Empty).Substring(0, 300) + "...";
            var thumbnailUrl = feedItem.SpecificItem.Element.Descendants().First(x => x.Name.LocalName == "enclosure").Attribute("url").Value;

            return EmbedHandler.CreateEmbed(title, description, Color.Teal, url, thumbnailUrl);
        }

        public async Task Init(DiscordSocketClient discordClient, List<string> rssLinks)
        {
            await LoggingService.LogInformationAsync("Program", "Running Daily Rss Check");
            foreach (var rssFeed in rssLinks)
            {
                var feed = await FetchFeedFromUrl(rssFeed);
                var latestFeedItems = CheckForNewResults(feed);

                foreach (var feedItem in latestFeedItems)
                {
                    var embed = RssItemToDiscordEmbed(feedItem);
                    await discordClient.GetGuild(452787311537815553).GetTextChannel(709131665259364443).SendMessageAsync(embed: embed);
                }
            }
        }
    }
}
