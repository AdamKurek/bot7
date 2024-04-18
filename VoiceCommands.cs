using Discord.Commands;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;
using Discord.Audio;
using System.Diagnostics;

namespace bot7
{
    public class VoiceCommands : ModuleBase<SocketCommandContext>
    {
        [Command("play")]
        public async Task PlayCommand()
        {
            if (Context.User is SocketGuildUser guildUser)
            {
                var voiceState = guildUser.VoiceState;

                if (voiceState?.VoiceChannel != null)
                {
                    try {
                        Thread thread = new(async ()  => {
                            Thread.Sleep(100);
                            try
                            {
                               var xd = voiceState.Value.VoiceChannel.ConnectAsync(true, false, false, true).Result;
                               while(true)
                               await SendAsync(xd, "C:/Users/kurek/Documents/melon.mp3");
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("it " + e.Message);
                            }
                });
                        thread.Start();
                        //await voiceState.Value.VoiceChannel.ConnectAsync(true, false, true, true);
                        //await SendAsync(voiceState.Value.VoiceChannel, "C:/Users/kurek/Documents/melon.mp3");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("t "+e.Message);
                    }
                }
                else
                {
                    await ReplyAsync("VC.");
                }
            }
            else
            {
                await ReplyAsync("This command can only be used in a server.");
            }
        }

        private Process CreateStream(string path)
        {
            return Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel panic -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
            });
        }

        private async Task SendAsync(SocketVoiceChannel voiceChannel, string path)
        {
            await voiceChannel.ConnectAsync(true, false, false, true);
            using (var ffmpeg = CreateStream(path))
            using (var output = ffmpeg.StandardOutput.BaseStream)
            using (var discord = voiceChannel.Guild.AudioClient.CreatePCMStream(AudioApplication.Mixed))
            {
                try { await output.CopyToAsync(discord); }
                finally { await discord.FlushAsync(); }
            }
        }
        private async Task SendAsync(IAudioClient client, string path)
        {
            using (var ffmpeg = CreateStream(path))
            using (var output = ffmpeg.StandardOutput.BaseStream)
            using (var discord = client.CreatePCMStream(AudioApplication.Mixed))
            {
                try { await output.CopyToAsync(discord); }
                finally { await discord.FlushAsync(); }
            }
        }

    }
}
