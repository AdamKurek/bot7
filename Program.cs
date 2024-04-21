using bot7;
using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.WebSocket;

namespace bot7
{
    internal class Program
    {
        static IAudioClient audioClient;
        static DiscordSocketClient client;
        static string botsCannal = "";
        static async Task Main(string[] args)
        {
            try { 
                LibOpusLoader.Init();
            }catch (Exception ex)
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


            var token = "MTIyODc1ODI5NTA2MjM4NDY0MA.GkFdOy.Ap_ELkH_8ZOCyMHrCLZ8vNSalDvqJjIuBvB22U";


            await client.LoginAsync(TokenType.Bot, token);
            client.Log += Client_Log;
            await client.StartAsync();

            //client.Connected += client_Connected;
            client.MessageReceived += Client_MessageReceived;
            client.UserVoiceStateUpdated += Client_UserVoiceStateUpdated;
            client.VoiceChannelStatusUpdated += Client_VoiceChannelStatusUpdated;
            //Thread.Sleep(10000000);
            await client.SetCustomStatusAsync("sziluje bombe");
            await Task.Delay(TimeSpan.FromMinutes(10000));
            LibOpusLoader.Dispose();
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
                botsCannal = state2.VoiceChannel?.Name;
                return;
            }
            if (state1.VoiceChannel is object ? state1.VoiceChannel.Name != state2.VoiceChannel.Name : true&& state2.VoiceChannel.Name == botsCannal)
            {
                if (user.GlobalName == "MrLeon")
                {
                    VoiceCommands.cancellationToken.Cancel();
                    await VoiceCommands.PlayOnce("C:/Users/kurek/Documents/lemon.mp3");
                    //await VoiceCommands.SendAsync(VoiceCommands.audioClient, "C:/Users/kurek/Documents/lemon.mp3", VoiceCommands.cancellationToken.Token);
                    return;
                }
                if (user.Username == "! Vicky")
                {
                    await VoiceCommands.PlayOnce("C:/Users/kurek/Documents/viczki.mp3");
                    return;
                }
                if (user.GlobalName == "kaczek")
                {
                    await VoiceCommands.PlayOnce("C:/Users/kurek/Documents/kaczek.mp3");
                    return;
                }
                if (user.GlobalName == "Siyuu")
                {
                    await VoiceCommands.PlayOnce("C:/Users/kurek/Documents/siju.mp3");
                    return;
                }
                if (user.GlobalName == "Krisq")
                {
                    await VoiceCommands.PlayOnce("C:/Users/kurek/Documents/krzys.mp3");
                    return;
                }
                if (user.GlobalName == "!Dawid Bombuszczak") 
                {
                    await VoiceCommands.PlayOnce("C:/Users/kurek/Documents/robul.mp3");
                    return;
                }
            }
        }

        private static Task Client_Log(LogMessage arg)
        {
            Console.WriteLine("logged " + arg.Message);
            return null!;
        }

        private static async Task Client_MessageReceived(SocketMessage arg)
        {
            var message = arg as SocketUserMessage;
            if (message.Content == "!play")
                await client.SetCustomStatusAsync("Jedzie audicą Leona");

            return;
            if (message == null) return;
            if (message.Author.IsBot) return;
            if (message.Content != "jd") return;
            var voiceChannel = (message.Author as IGuildUser)?.VoiceChannel;
            if (voiceChannel == null)
            {
                await message.Channel.SendMessageAsync("na kanał wejdz");
                return;
            }
            try
            {
                //Discord.Audio.IAudioClient audio;
                //audio.
                audioClient = await voiceChannel.ConnectAsync(true, false, false, false);
                Console.WriteLine(" przeszed");

            }
            catch (Exception e)
            {
                Console.WriteLine(" xd " + e);
                await message.Channel.SendMessageAsync(e.ToString());
            }
            string response = "hihi";
            await message.Channel.SendMessageAsync(response);
        }

    }
}
