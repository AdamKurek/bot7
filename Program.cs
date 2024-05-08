﻿using bot7;
using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.WebSocket;
using System.Diagnostics;

namespace bot7
{
    internal class Program
    {
        static IAudioClient audioClient;
        static public DiscordSocketClient client;
        static string botsCannal = "";
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
<<<<<<< HEAD
            var token = "";
=======

            var token = "MTIyODc1ODI5NTA2MjM4NDY0MA.GkFdOy.Ap_ELkH_8ZOCyMHrCLZ8vNSalDvqJjIuBvB22U";// recruiters please

>>>>>>> 25cbafd (added volumes and started working on speach to text)
            await client.LoginAsync(TokenType.Bot, token);
            client.Log += Client_Log;
            await client.StartAsync();

            //client.Connected += client_Connected;
            client.MessageReceived += Client_MessageReceived;
            client.UserVoiceStateUpdated += Client_UserVoiceStateUpdated;
            
            client.VoiceChannelStatusUpdated += Client_VoiceChannelStatusUpdated;
            client.InteractionCreated += Client_InteractionCreated;
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

        private static Task Client_InteractionCreated(SocketInteraction arg)
        {
            if(arg.Type == InteractionType.MessageComponent)
            {

                arg.RespondAsync("xd");
            }
            return Task.CompletedTask;
        }

        private static Task Client_VoiceChannelStatusUpdated(Cacheable<SocketVoiceChannel, ulong> arg1, string arg2, string arg3)
        {
            Console.WriteLine();
            return Task.CompletedTask;
        }
       
        private async static Task Client_UserVoiceStateUpdated(SocketUser user, SocketVoiceState state1, SocketVoiceState state2)
        {
<<<<<<< HEAD
            
=======
>>>>>>> 25cbafd (added volumes and started working on speach to text)
            if (user.IsBot)
            {
                botsCannal = state2.VoiceChannel?.Name;
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
        public static async Task MessageInChannel(string message)
        {
            ulong chan = 1229272739303526501;
            var chanelawait = (await client.GetChannelAsync(chan)) as IMessageChannel;
            await chanelawait.SendMessageAsync(message);
            return;
        }

        public static async Task MessageInChannel(string message, string id)
        {
            
            //degenerate = 866745065020063747
            // botkanal  = 1229272739303526501
            ulong chan = 1229272739303526501;
            var chanelawait = (await client.GetChannelAsync(chan)) as IMessageChannel;
            await chanelawait.SendMessageAsync(message);
            var buttonBuilder = new ButtonBuilder()
            .WithLabel("<3")
            .WithStyle(ButtonStyle.Primary)
            .WithCustomId("id");

            // Create a component builder and add the button to it
            var componentBuilder = new ComponentBuilder()
                .WithButton(buttonBuilder);

            // Send the message with the component (button)
            chanelawait.SendMessageAsync("message", components: componentBuilder.Build());
            return;
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
