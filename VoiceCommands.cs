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
using AngleSharp.Dom;
using YoutubeExplode.Playlists;
using AngleSharp.Common;


namespace bot7
{
    public class VoiceCommands : ModuleBase<SocketCommandContext>
    {
        public static CancellationTokenSource cancellationToken = new CancellationTokenSource();
        private static Process _currentProcess = null!;
        public static IAudioClient? audioClient;
        public static Thread thread;
        public static bool imStopping = false;
        private static YoutubeClient youtube = new YoutubeClient();
        static SongsQueuee<string> queue = new("https://www.youtube.com/watch?v=woNw5Dyqhzo");
        static SocketVoiceState? voiceState;

        internal static async Task SongsThread()
        {
            try
            {
                if (audioClient == null)
                {
                    audioClient = voiceState.Value.VoiceChannel.ConnectAsync(true, false, false, false).Result;
                }
                cancellationToken = new();
                while (true)
                {
                    var token = cancellationToken.Token;
                    var currUrl = queue.Dequeue();
                    await Program.MessageInChannel("playing " + currUrl);
                    await SendAsyncYT(audioClient, currUrl, token);
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
        }



        [Command("play")]
        public async Task PlayCommand(string url = "https://www.youtube.com/watch?v=woNw5Dyqhzo")
        {
            if (Context.User is SocketGuildUser guildUser)
            {
                voiceState = guildUser.VoiceState;
                if (voiceState?.VoiceChannel != null)
                {
                    try {

                        cancellationToken.Cancel();
                        try
                        {
                            if (thread != null)
                            {
                                thread.Join();
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("noone cares exception:" + e.Message);
                        }
                        var urls = TryAsPlaylist(url);
                        if (urls.Count() > 0)
                        {
                            queue.AppendFront(urls);
                        }
                        else
                        {
                            queue.insert(0, url);
                        }
                        thread = new Thread(async () => { await SongsThread(); });
                        thread.Start();
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

        [Command("q")]
        public async Task QCommand(string url = "https://www.youtube.com/watch?v=woNw5Dyqhzo")
        {
            if (Context.User is SocketGuildUser guildUser)
            {
                voiceState = guildUser.VoiceState;
                if (voiceState?.VoiceChannel != null)
                {
                    try
                    {
                        var urls = TryAsPlaylist(url);
                        if (urls.Count() > 0)
                        {
                            queue.AppendEnd(urls);
                        }
                        else
                        {
                            queue.enqueue(url);
                        }
                        if (thread == null)
                        {
                            thread = new Thread(async () => { await SongsThread(); });
                            thread.Start();
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("t " + e.Message);
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

        [Command("list")]
        public async Task ListCommand()
        {
            string mess = "queue:\n";
            int count = 0;
            foreach(var s in queue) {
                mess += s + '\n';
                count++;
                if(count % 20 == 0)
                {
                    await Program.MessageInChannel(mess);
                    mess = "";
                }
            }
            await Program.MessageInChannel(mess);

        }

        [Command("skip")]
        public async Task SkipCommand()
        {
            cancellationToken.Cancel();
            thread.Join();
            thread = new Thread(async () => { await SongsThread(); });
            thread.Start();
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

        private async static Task<string> CreateFileFromYt(string url)
        {

            if (_currentProcess != null)
            {
                _currentProcess.Dispose();
            }
            try {
                var streamManifest = await youtube.Videos.Streams.GetManifestAsync(url);
                var streamInfo = streamManifest.GetAudioOnlyStreams().TryGetWithHighestBitrate();
                string file = $"audio.{streamInfo.Container}";
                if (streamInfo != null)
                {
             //       if (!File.Exists($"audio.{streamInfo.Container}"))
                    {
                 //       Console.WriteLine($"audio.{streamInfo.Container}");
                        await youtube.Videos.Streams.DownloadAsync(streamInfo, file);
                    }
               //     Console.WriteLine($"audio.{streamInfo.Container}");

                }
                return file;
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
                using (_currentProcess = CreateStream(await CreateFileFromYt(path)))
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

        public static IEnumerable<string> TryAsPlaylist(string url)
        {
            IEnumerable<PlaylistVideo> vids = null;
            try
            {
                vids = youtube.Playlists.GetVideosAsync(url).ToEnumerable();
            }
            catch (Exception e)
            {
                yield break;
            }
            foreach (var vid in vids)
            {
                yield return vid.Url;
            }
        }

    }
}
