using AchilliosOmega.Discord.Handlers;
using Discord.WebSocket;
using Quartz;
using System.Threading.Tasks;

namespace AchilliosOmega.Discord.Services.TaskScheduler.Jobs
{
    public class RssJob : IJob
    {
        private readonly RssService _rssService;
        private readonly DiscordSocketClient _client;

        public RssJob(DiscordSocketClient client, RssService rssService)
        {
            _client = client;
            _rssService = rssService;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            await _rssService.Init(_client, ConfigHandler.Config.RssLinks);
        }
    }
}
