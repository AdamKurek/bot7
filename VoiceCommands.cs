using Discord.Commands;
using Discord;
using Discord.WebSocket;
using Discord.Audio;
using System.Diagnostics;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using AngleSharp.Dom;
using YoutubeExplode.Playlists;
using System.Runtime.CompilerServices;
using System.Speech.Synthesis;
using System;
using System.Globalization;


namespace bot7
{
    public class VoiceCommands : ModuleBase<SocketCommandContext>
    {
        public static CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private static Process _currentProcess = null!;
        public static IAudioClient? audioClient;
        public static Thread? thread;
        public static bool _stop = false;
        private static YoutubeClient youtube = new YoutubeClient();
        static SongsQueuee<string> queue = new("https://www.youtube.com/watch?v=woNw5Dyqhzo");
        static SocketVoiceState? voiceState;
        static long pausedAt = 0;
        static Semaphore semaphore = new(1,1);
        static bool cutIn = false;

        internal async Task SongsThread()
        {
            try
            {
                if (audioClient == null)
                {
                    //var discordstream = client.CreatePCMStream(AudioApplication.Mixed)
                    audioClient =  await voiceState.Value.VoiceChannel.ConnectAsync(true, false, false, false);
                }
                _cancellationTokenSource = new();
                while (true)
                {
                    var token = _cancellationTokenSource.Token;
                    var currUrl = queue.Dequeue();
                    await Program.MessageInChannel("playing " + currUrl);
                    await SendAsyncYT(audioClient, currUrl);
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
        [Alias("p")]
        public async Task PlayCommand(string url = "https://www.youtube.com/watch?v=woNw5Dyqhzo")
        {
            _stop = false;
            if (Context.User is SocketGuildUser guildUser)
            {
                voiceState = guildUser.VoiceState;
                if (voiceState?.VoiceChannel != null)
                {
                    try { 
                        var urls = TryAsPlaylist(url);
                        if (urls.Count() > 0)
                        {
                            queue.AppendFront(urls);
                            Console.WriteLine("otuz " + urls.Contains(url) + url.IndexOf(url));
                        }
                        else
                        {
                            queue.insert(0, url);
                        }
                        if (thread == null)
                        {
                            thread = new Thread(async () => { await SongsThread(); });
                            thread.Start();
                            return;
                        }
                        _cancellationTokenSource.Cancel();
                        _cancellationTokenSource = new();


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

        [Command("enqueue")]
        [Alias("pushback", "enq", "q", "potem", "queue")]
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
                            _stop = false;
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


        [Command("quickplay")]
        [Alias("qp", "slide", "zaprezentuj", "cutin")]
        public async Task QuickplayCommand(string url = "https://www.youtube.com/watch?v=woNw5Dyqhzo")
        {
            _stop = false;
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
                            queue.AppendFront(urls);
                            Console.WriteLine("otuz " + urls.Contains(url) + url.IndexOf(url));
                        }
                        else
                        {
                            queue.insert(0, url);
                        }
                        if (thread == null)
                        {
                            thread = new Thread(async () => { await SongsThread(); });
                            thread.Start();
                            return;
                        }
                        cutIn = true;
                        _cancellationTokenSource.Cancel();

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


        [Command("set default")]
        [Alias("default", "domyślna piosenka", "kółkuj", "loop")]
        public async Task SetDefault(string url = "https://www.youtube.com/watch?v=woNw5Dyqhzo")
        {
            queue.DefaultSong = url;
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
        [Alias("next", "s", "ale gówno", "co to za gówno")]
        public async Task SkipCommand()
        {
            SkipSong();
        }
        private static void SkipSong()
        {
            var currentTokenSource = _cancellationTokenSource;
            _cancellationTokenSource = new();
            currentTokenSource.Cancel();
        }

        [Command("pause")]
        public async Task PauseCommand()
        {
            _cancellationTokenSource.Cancel();
            semaphore.WaitOne();
        }

        [Command("resume")]
        [Alias("r")]
        public async Task ResumeCommand()
        {
            semaphore.Release();
        }

        [Command("stop")]
        public async Task StopCommand()
        {
            StopMusicThread();
        }

        async void StopMusicThread()
        {
            semaphore.Release();
            _cancellationTokenSource.Cancel();
            _stop = true;
            thread.Join();
            thread = null;
        }

        public async Task PlayOnce(string pathto)
        {
            _stop = false;
            try
            {
                VoiceCommands._cancellationTokenSource.Cancel();
                thread.Join();
                _cancellationTokenSource = new();
                thread = new(async () => {
                    try
                    {
                        var tkn = _cancellationTokenSource.Token;
                        await SendAsync(audioClient, pathto, tkn);
                        if (tkn.IsCancellationRequested)
                        {
                            return;
                        }
                    while (true)
                        {
                            
                            //await SendAsyncYT(audioClient, "https://www.youtube.com/watch?v=woNw5Dyqhzo", token);

                            //if (token.IsCancellationRequested)
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
                Console.WriteLine("wontkowy Error: " + e.Message);
            }
        }

        public static async Task BotSpeak(string text)
        {
            await BudgetQuickplayCommand(await CreateFileText(text, 0));

        }

        private async static Task BudgetQuickplayCommand(string path)
        {
            _stop = false;
            if (voiceState?.VoiceChannel != null)
            {
                queue.insert(0, path);
                cutIn = true;
                _cancellationTokenSource.Cancel();
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

        private async static Task<string> CreateFileFromYt(string url, int iteration)
        {

            if (_currentProcess != null)
            {
                _currentProcess.Dispose();
            }
            try {
                var streamManifest = await youtube.Videos.Streams.GetManifestAsync(url);
                var streamInfo = streamManifest.GetAudioOnlyStreams().TryGetWithHighestBitrate();
                string file = $"audio{iteration}.{streamInfo.Container}";
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

        private async static Task<string> CreateFileText(string Text, int iteration)
        {
            try
            {

                SpeechSynthesizer synthesizer = new SpeechSynthesizer();
                string file = $"output{iteration}.wav";
                synthesizer.SetOutputToWaveFile(file);
                foreach (InstalledVoice voice in synthesizer.GetInstalledVoices())
                {
                    VoiceInfo voiceInfo = voice.VoiceInfo;
                    if (voiceInfo.Culture.Equals(new CultureInfo("pl-PL")))
                    {
                        synthesizer.SelectVoice(voiceInfo.Name);
                        break;
                    }
                }
                synthesizer.Speak(Text);
                synthesizer.SetOutputToDefaultAudioDevice();

                return file;
            }
            catch (Exception e)
            {
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
                using (var discordstream = client.CreatePCMStream(AudioApplication.Mixed))
                {
                    discordstream.Position = pausedAt;
                    Console.WriteLine(pausedAt);
                    try { await output.CopyToAsync(discordstream, cancellationToken); }
                    finally { 
                        Console.WriteLine("drugi+ "+pausedAt);
                        await discordstream.FlushAsync(cancellationToken); }
                }

            }
            catch (Exception e)
            {
                Console.WriteLine("flushbug:" + e.Message);
            }
        }

        public static async Task SendAsyncYT(IAudioClient client, string path, int depth = 0)
        {
            try
            {
                using (var currentProcess = CreateStream(IsFromYoutube(path) ? await CreateFileFromYt(path, depth) : path))
                using (var output = currentProcess.StandardOutput.BaseStream)
                {
                    if (discordstream == null)
                    {
                        discordstream = client.CreatePCMStream(AudioApplication.Mixed);
                    }
                    {
                        try
                        {
                            do
                            {
                                if (cutIn)
                                {
                                    cutIn = false;
                                    await SendAsyncYT(client, queue.Dequeue(), depth + 1);
                                }
                                _cancellationTokenSource = new();
                                if (_stop)
                                {
                                    _stop = false;
                                    break;
                                }
                                semaphore.WaitOne();
                                try
                                {
                                    await output.CopyToAsync(discordstream, _cancellationTokenSource.Token);
                                }
                                catch (Exception e){}
                                finally
                                {
                                    semaphore.Release();
                                }
                            } while (_cancellationTokenSource.IsCancellationRequested);
                        }
                        finally
                        {
                            if (depth == 0)
                                await discordstream.FlushAsync();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("flushbug:" + e.Message);
            }
        }

        private static bool IsFromYoutube(string path)
        {
            return path.Substring(0, 5).ToLower() == "https";
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



        [Command("ButtonClicked")]
        public async Task YourCommand()
        {
            Console.WriteLine("xd");
            // Create and send the button as shown above
            // ...
        }

        public async Task HandleButtonClick(SocketMessageComponent component)
        {
            Console.WriteLine("xd2");

            if (component.Data.CustomId == "button_guzik")
            {
                await ReplyAsync("<3");
            }
        }

    }
}
