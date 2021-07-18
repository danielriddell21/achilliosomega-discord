using AchilliosOmega.Discord;

namespace AchilliosOmega.Main
{
    public class Program
    {
        static void Main(string[] args)
        {
            new DiscordService().MainAsync().GetAwaiter().GetResult();
        }
    }
}
