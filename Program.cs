using bot7;
using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.WebSocket;
using Swan;
using System.ComponentModel;
using System.Diagnostics;

namespace bot7
{
    internal class Program
    {
        static public DiscordSocketClient client;
        static async Task Main(string[] args)
        {
            try
            {
                LibOpusLoader.Init();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            var config = new DiscordSocketConfig()
            {
                GatewayIntents = GatewayIntents.All
            };

            client = new DiscordSocketClient(config);
            CommandService _commands = new CommandService();
            CommandHandler _commandHandler = new CommandHandler(client, _commands);
            await _commandHandler.InstallCommandsAsync();
            var token = "";
            token = Environment.GetEnvironmentVariable("DiscordToken");
            await client.LoginAsync(TokenType.Bot, token);
            client.Log += Client_Log;
            await client.StartAsync();

            //client.Connected += client_Connected;
            client.MessageReceived += Client_MessageReceived;
            client.UserVoiceStateUpdated += Client_UserVoiceStateUpdated;
            
            client.VoiceChannelStatusUpdated += Client_VoiceChannelStatusUpdated;
            client.InteractionCreated += Client_InteractionCreated;
            client.ButtonExecuted += Client_ButtonExecuted;
            //Thread.Sleep(10000000);
            await client.SetCustomStatusAsync("Proszę beton");
            try
            {
                for (; ; )
                {
                    var mess = Console.ReadLine();
                    if (mess != null)
                    {
                        await VoiceCommands.BotSpeak(mess);
                    }
                }
            }
            finally
            {
                LibOpusLoader.Dispose();
            }
        }

        private static async Task Client_ButtonExecuted(SocketMessageComponent arg)
        {
            await VoiceCommands.HandleButtonClick(arg);
           // message.

        }

        private static async Task Client_InteractionCreated(SocketInteraction arg)
        {
        }

        private static Task Client_VoiceChannelStatusUpdated(Cacheable<SocketVoiceChannel, ulong> arg1, string arg2, string arg3)
        {
            Console.WriteLine();
            return Task.CompletedTask;
        }
       
        private async static Task Client_UserVoiceStateUpdated(SocketUser user, SocketVoiceState state1, SocketVoiceState state2)
        {
            
            if (user.IsBot)
            {
                return;
            }
         /*   if (state1.VoiceChannel is object ? state1.VoiceChannel.Name != state2.VoiceChannel.Name : true&& state2.VoiceChannel.Name == botsCannal)
            {
                
                if (user.GlobalName == "MrLeon")
                {
                    await VoiceCommands.BudgetQuickplayCommand("C:/Users/kurek/Documents/lemon.mp3");
                    return;
                }
                if (user.Username == "! Vicky")
                {
                    //await VoiceCommands.BudgetQuickplayCommand("C:/Users/kurek/Documents/viczki.mp3");
                    return;
                }
                if (user.GlobalName == "kaczek")
                {
                    await VoiceCommands.BudgetQuickplayCommand("C:/Users/kurek/Documents/kaczek.mp3");
                    return;
                }
                if (user.GlobalName == "Siyuu")
                {
                    await VoiceCommands.BudgetQuickplayCommand("C:/Users/kurek/Documents/siju.mp3");
                    return;
                }
                if (user.GlobalName == "Krisq")
                {
                    await VoiceCommands.BudgetQuickplayCommand("C:/Users/kurek/Documents/krzys.mp3");
                    return;
                }
                if (user.GlobalName == "!Dawid Bombuszczak") 
                {
                    await VoiceCommands.BudgetQuickplayCommand("C:/Users/kurek/Documents/robul.mp3");
                    return;
                }
            }*/
        }

       
        static IUserMessage userMessage;
        public static async Task SendCurrentPlayingMessage(IMessageChannel chanelawait, string message, bool paused = false)
        {
            var buttonBuilder1 = new ButtonBuilder()
                .WithStyle(ButtonStyle.Secondary)
                .WithEmote(new Emoji("👎"))
                .WithCustomId("Skip");

            var buttonBuilder2 = paused?
                new ButtonBuilder()
                .WithStyle(ButtonStyle.Success)
                .WithEmote(new Emoji("▶️"))
                .WithCustomId("Resume"):
                new ButtonBuilder()
                .WithStyle(ButtonStyle.Danger)
                .WithEmote(new Emoji("⏹️"))
                .WithCustomId("Pause");

            var buttonBuilder4 = new ButtonBuilder()
               .WithStyle(ButtonStyle.Danger)
               .WithEmote(new Emoji("🗑️"))
               .WithCustomId("Clear");

            var componentBuilder = new ComponentBuilder()
                .WithButton(buttonBuilder1)
                .WithButton(buttonBuilder2)
                .WithButton(buttonBuilder4);

            userMessage?.DeleteAsync(); 
            userMessage = await chanelawait.SendMessageAsync($"{message}", components: componentBuilder.Build());
        }


        private static Task Client_Log(LogMessage arg)
        {
            Console.WriteLine("logged " + arg.Message);
            return Task.CompletedTask;
        }

        private static async Task Client_MessageReceived(SocketMessage arg)
        {
            var message = arg as SocketUserMessage;
            if (message.Author.IsBot) return;
            if (message.Content == "!wyłącz mi komputer xd")
            {
                //Process.Start("shutdown", "/s /t 5");
            }
            if (message.Content == "!play")
            {
                await client.SetCustomStatusAsync("Jedzie audicą Leona");
            }
            return;
        }

    }
}
