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
using System.Threading;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using System.IO;


namespace bot7
{
    public class VoiceCommands : ModuleBase<SocketCommandContext>
    {
        public static CancellationTokenSource cancellationToken = new CancellationTokenSource();
        private static Process _currentProcess = null!;
        public static IAudioClient audioClient;
        public static Thread thread;
        public static bool imStopping = false;
        private static YoutubeClient youtube = new YoutubeClient();

        [Command("play")]
        public async Task PlayCommand(string url = "https://www.youtube.com/watch?v=woNw5Dyqhzo")
        {
            if (Context.User is SocketGuildUser guildUser)
            {
                var voiceState = guildUser.VoiceState;
                if (voiceState?.VoiceChannel != null)
                {
                    try {
                         thread = new(async ()  => {
                            try
                            {
                               audioClient = voiceState.Value.VoiceChannel.ConnectAsync(true, false, false, false).Result;
                                 cancellationToken = new();

                                 while (true)
                                 {
                                     Console.WriteLine("proboje");
                                     var token = cancellationToken.Token;
                                     await SendAsyncYT(audioClient, "https://www.youtube.com/watch?v=woNw5Dyqhzo", token);
                                     if (token.IsCancellationRequested)
                                     {
                                         return;
                                     }
                                 }
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

        public static async Task PlayOnce(string pathto)
        {
            try
            {
                imStopping = true;
                VoiceCommands.cancellationToken.Cancel();
                thread.Join();
                cancellationToken = new();
                thread = new(async () => {
                    try
                    {
                        cancellationToken = new();
                        var tkn = cancellationToken.Token;
                        await SendAsync(audioClient, pathto, tkn);
                        if (tkn.IsCancellationRequested)
                        {
                            return;
                        }
                    while (true)
                        {
                            var token = cancellationToken.Token;
                            //await SendAsync(audioClient, "C:/Users/kurek/Documents/melon.mp3", token);
                            
                            await SendAsyncYT(audioClient, "https://www.youtube.com/watch?v=woNw5Dyqhzo", token);

                            if (token.IsCancellationRequested)
                            {
                                return;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("it " + e.Message);
                    }
                });
                thread.Start() ;
            }
            catch (Exception e)
            {
                imStopping = false;
                Console.WriteLine("wontkowy Error: " + e.Message);
            }
        }

            private static Task Client_Log(LogMessage arg)
            {
                Console.WriteLine("logged " + arg.Message);
                return null!;
            }
            private static Process CreateStream(string path)
            {
                //Console.WriteLine($"trying to get the process");

                if (_currentProcess != null)
                {
                    _currentProcess.Dispose();
                }

#pragma warning disable CS8603 // Possible null reference return.
                return Process.Start(new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-hide_banner -loglevel panic -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
                    UseShellExecute = false,
                    RedirectStandardOutput = true, 
                });
#pragma warning restore CS8603 // Possible null reference return.
        }

        private async static Task<string> CreateStreamYT(string url)
        {

            if (_currentProcess != null)
            {
                _currentProcess.Dispose();
            }
            try {

                var streamManifest = await youtube.Videos.Streams.GetManifestAsync(url);

                // Get the highest bitrate audio-only stream
                var streamInfo = streamManifest.GetAudioOnlyStreams().TryGetWithHighestBitrate();

                // Download the stream to a file
                await youtube.Videos.Streams.DownloadAsync(streamInfo, $"audio.{streamInfo.Container}");
                if (File.Exists($"audio.{streamInfo.Container}"))
                {
                    Console.WriteLine($"File {$"audio.{streamInfo.Container}"} downloaded successfully.");
                }
                else
                {
                    Console.WriteLine($"File {$"audio.{streamInfo.Container}"} was not downloaded.");
                }
                return $"audio.{streamInfo.Container}";
                //return await youtube.Videos.Streams.GetAsync(streamInfo);


            }
            catch (Exception e) {
                Console.WriteLine(e.ToString());
                return "";
            }
        }

        static AudioOutStream? discordstream = null;
        public static async Task SendAsync(IAudioClient client, string path, CancellationToken cancellationToken)
        {
            try {
                using (_currentProcess = CreateStream(path))
                using (var output = _currentProcess.StandardOutput.BaseStream)
                using (discordstream = client.CreatePCMStream(AudioApplication.Mixed))
                {
                    try { await output.CopyToAsync(discordstream, cancellationToken); }
                    finally { await discordstream.FlushAsync(cancellationToken); }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("flushbug:" + e.Message);
            }
        }

        public static async Task SendAsyncYT(IAudioClient client, string path, CancellationToken cancellationToken)
        {
            try
            {
                using (_currentProcess = CreateStream(await CreateStreamYT(path)))
                using (var output = _currentProcess.StandardOutput.BaseStream)
                using (discordstream = client.CreatePCMStream(AudioApplication.Mixed))
                {
                    try { await output.CopyToAsync(discordstream, cancellationToken); }
                    finally { await discordstream.FlushAsync(cancellationToken); }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("flushbug:" + e.Message);
            }
        }

    }
}
