using Discord.Commands;
using Discord;
using Discord.WebSocket;
using Discord.Audio;
using System.Diagnostics;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using AngleSharp.Dom;
using YoutubeExplode.Playlists;
using System.Speech.Synthesis;
using System.Globalization;
using System;
using Whisper.net;
using Whisper.net.Ggml;

namespace bot7
{
    public class VoiceCommands : ModuleBase<SocketCommandContext>
    {
        public enum songType
        {
            ytSong,
            Voice
        }

        public static CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        public static CancellationTokenSource _recordingCancellationTokenSource = new CancellationTokenSource();

        private static Process _currentProcess = null!;
        public static IAudioClient? audioClient;
        public static Thread? thread;
        public static Thread? recordingThread;
        public static bool _stop = false;
        private static YoutubeClient youtube = new YoutubeClient();
        static SongsQueuee<(string, songType)> queue = new(("https://www.youtube.com/watch?v=woNw5Dyqhzo", songType.ytSong));
        static SocketVoiceState? voiceState;
        static long pausedAt = 0;
        static Semaphore semaphore = new(1,1);
        static bool cutIn = false;

        //Whisper.iAudioBuffer? buffer;


        internal async Task SongsThread()
        {
            try
            {
                if (audioClient == null)
                {
                    //var discordstream = client.CreatePCMStream(AudioApplication.Mixed)
                    audioClient =  await voiceState.Value.VoiceChannel.ConnectAsync(false, false, false, false);
                }
                _cancellationTokenSource = new();
                while (true)
                {
                    var token = _cancellationTokenSource.Token;
                    var currUrl = queue.Dequeue();
                    await Program.MessageInChannel("playing " + currUrl.Item1);
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

        internal async Task RecordingThread()
        {
            try
            {
                
                //if (audioClient == null)
                {
                    //audioClient = await voiceState.Value.VoiceChannel.ConnectAsync(false, false, false, false);
                }

                //var audioClient = await channel.ConnectAsync();
                //var audioStream = audioClient.CreatePCMStream(AudioApplication.Mixed);
                //using (var fileStream = File.Create("voices.pcm"))
                //{
                //    await audioStream.CopyToAsync(fileStream);
                //}

                //var channel = channel ?? (Context.User as IGuildUser)?.VoiceChannel;
                //if (channel == null)
                {
                   // await ReplyAsync("User must be in a voice channel, or a voice channel must be passed as an argument.");
                   // return;
                }

                // Transmit silence to comply with Discord's requirements
                //var audioClient = await channel.ConnectAsync();
                //await TransmitSilenceAsync(audioClient);

                // Listen to the audio stream
                _recordingCancellationTokenSource = new();
                await ListenToAudioStream(voiceState.Value.VoiceChannel);

                //WhisperMain.WhisperRun(Array.Empty<string>());


            }
            catch (Exception e)
            {
                Console.WriteLine("it " + e.Message);
            }
        }

        private async Task ListenToAudioStream(IVoiceChannel channel)
        {
            var users =  channel.GetUsersAsync();
            await foreach (var userCollection in channel.GetUsersAsync())
            {
                foreach (var user in userCollection)
                {
                    if (!user.IsBot)
                    {
                        string nm = "kaczek";
                        //if (user.DisplayName == nm)
                        //{
                        //    Console.WriteLine(user.DisplayName);
                        //}
                        //if (user.GlobalName == nm)
                        //{
                        //    Console.WriteLine(user.GlobalName);
                        //}
                       
                        //{
                        //    Console.WriteLine(user.Username);
                        //}
                        //if (user.Nickname == nm)
                        //{
                        //    Console.WriteLine(user.Nickname);
                        //}
                        if (user.Username == nm)
                        if (user is SocketGuildUser guilduser)
                        {
                            if(guilduser.AudioStream != null)
                            {
                                var filePath = "outputAudioFile.webm";
                                if (File.Exists(filePath))
                                {
                                    File.Delete(filePath);
                                }
                                string ffmpegPath = "ffmpeg"; 
                                string arguments = "-f s16le -ar 48000 -ac 2 -i pipe:0 -c:a libvorbis outputAudioFile.webm";
                                using (Process ffmpegProcess = new Process())
                                {
                                    ProcessStartInfo startInfo = new ProcessStartInfo
                                    {
                                        FileName = ffmpegPath,
                                        Arguments = arguments,
                                        UseShellExecute = false,
                                        RedirectStandardInput = true,
                                        RedirectStandardOutput = false,
                                        RedirectStandardError = true,
                                        CreateNoWindow = true,
                                    };

                                    ffmpegProcess.StartInfo = startInfo;

        internal async Task RecordingThread()
        {
            try
            {
                
                //if (audioClient == null)
                {
                    //audioClient = await voiceState.Value.VoiceChannel.ConnectAsync(false, false, false, false);
                }

                //var audioClient = await channel.ConnectAsync();
                //var audioStream = audioClient.CreatePCMStream(AudioApplication.Mixed);
                //using (var fileStream = File.Create("voices.pcm"))
                //{
                //    await audioStream.CopyToAsync(fileStream);
                //}

                //var channel = channel ?? (Context.User as IGuildUser)?.VoiceChannel;
                //if (channel == null)
                {
                   // await ReplyAsync("User must be in a voice channel, or a voice channel must be passed as an argument.");
                   // return;
                }

                // Transmit silence to comply with Discord's requirements
                //var audioClient = await channel.ConnectAsync();
                //await TransmitSilenceAsync(audioClient);

                // Listen to the audio stream
                _recordingCancellationTokenSource = new();
                await ListenToAudioStream(voiceState.Value.VoiceChannel);

                //WhisperMain.WhisperRun(Array.Empty<string>());


            }
            catch (Exception e)
            {
                Console.WriteLine("it " + e.Message);
            }
        }

        private async Task ListenToAudioStream(IVoiceChannel channel)
        {
            var users =  channel.GetUsersAsync();
            await foreach (var userCollection in channel.GetUsersAsync())
            {
                foreach (var user in userCollection)
                {
                    if (!user.IsBot)
                    {
                        string nm = "kaczek";
                        //if (user.DisplayName == nm)
                        //{
                        //    Console.WriteLine(user.DisplayName);
                        //}
                        //if (user.GlobalName == nm)
                        //{
                        //    Console.WriteLine(user.GlobalName);
                        //}
                       
                        //{
                        //    Console.WriteLine(user.Username);
                        //}
                        //if (user.Nickname == nm)
                        //{
                        //    Console.WriteLine(user.Nickname);
                        //}
                        if (user.Username == nm)
                        if (user is SocketGuildUser guilduser)
                        {
                            if(guilduser.AudioStream != null)
                            {
                                var filePath = "outputAudioFile.webm";
                                if (File.Exists(filePath))
                                {
                                    File.Delete(filePath);
                                }
                                string ffmpegPath = "ffmpeg"; 
                                string arguments = $"-f s16le -ar 48000 -ac 2 -i pipe:0 -c:a libvorbis {filePath}";
                                using (Process ffmpegProcess = new Process())
                                {
                                    ProcessStartInfo startInfo = new ProcessStartInfo
                                    {
                                        FileName = ffmpegPath,
                                        Arguments = arguments,
                                        UseShellExecute = false,
                                        RedirectStandardInput = true,
                                        RedirectStandardOutput = true,
                                        RedirectStandardError = true,
                                        CreateNoWindow = true,
                                    };

                                    ffmpegProcess.StartInfo = startInfo;

                                    ffmpegProcess.Start();
                                    using (var audioStream = guilduser.AudioStream)
                                    {
                                        using (var stdin = ffmpegProcess.StandardInput.BaseStream)
                                        {
                                            EndOfStreamWrapper endOfStreamWrapper = new(audioStream);
                                            try
                                            {
                                                await endOfStreamWrapper.CopyToAsync(stdin, _recordingCancellationTokenSource.Token);
                                            }
                                            catch (Exception e)
                                            {
                                                Console.WriteLine(e.Message);
                                            }
                                            finally
                                            {
                                                ffmpegProcess.WaitForExit(40);
                                                await stdin.FlushAsync();
                                            }
                                        }
                                    }

                                    Console.WriteLine("zakonczylem sluchanie");
                                }
                            }
                        }
                    }
                }
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
                            //Console.WriteLine("otuz " + urls.Contains(url) + url.IndexOf(url));
                        }
                        else
                        {
                            queue.insert(0, (url,songType.ytSong));
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
                            queue.enqueue((url, songType.ytSong));
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
                            //Console.WriteLine("otuz " + urls.Contains(url) + url.IndexOf(url));
                        }
                        else
                        {
                            queue.insert(0, (url, songType.ytSong));
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
            queue.DefaultSong = (url, songType.ytSong);
        }

        [Command("list")]
        public async Task ListCommand()
        {
            string mess = "queue:\n";
            int count = 0;
            foreach(var s in queue) {
                mess += s.Item1 + '\n';
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

        [Command("record")]
        [Alias("rec")]
        public async Task RecordCommand()
        {
            recordingThread = new Thread(async () => { await RecordingThread(); });
            recordingThread.Start();
        }
        
        [Command("reco")]
        [Alias("rec stop")]
        public async Task RecordStopCommand()
        {
            _recordingCancellationTokenSource.Cancel();
        }

        [Command("textuj")]
        [Alias("text")]
        public async Task TextCommand()
        {
            //string[] tokeny = { "-m", "Models/ggml-medium.bin" , "outputAudioFile.webm"};
            //WhisperMain.WhisperRun(tokeny);

            var modelName = "ggml-medium.bin"; // Specify the model name you want to download
                                               //if (!File.Exists(modelName))
                                               //{
                                               //    using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(GgmlType.Base); // Adjust GgmlType.Base to the type of model you need
                                               //    using var fileWriter = File.OpenWrite(modelName);
                                               //    await modelStream.CopyToAsync(fileWriter);
                                               //}

            string inputFile = "outputAudioFile.webm"; // Path to the recorded WebM file
            string outputFile = "outputFile.wav"; // Desired output .wav file
            if (File.Exists(outputFile))
            {
                File.Delete(outputFile);
            }
            // FFmpeg command to convert the recorded audio file (WebM) to a .wav file with 16 kHz sample rate
            string arguments = $"-i {inputFile} -ar 16000 {outputFile}";

            using (Process ffmpegProcess = new Process())
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg", // Assuming ffmpeg is in the system PATH
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                ffmpegProcess.StartInfo = startInfo;
                ffmpegProcess.Start();

                // You may want to wait for the process to finish before continuing
                ffmpegProcess.WaitForExit();
            }







            Console.WriteLine("sciongled");
            try
            {
                using var whisperFactory = WhisperFactory.FromPath("Models/ggml-medium.bin");
                using var processor = whisperFactory.CreateBuilder()
                    .WithLanguage("pl")
                    .Build();





                using var fileStream = File.OpenRead("outputFile.wav");

                await foreach (var result in processor.ProcessAsync(fileStream))
                {
                    Console.WriteLine($"{result.Start}->{result.End}: {result.Text}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("ebege = " + e);
            }
        }

        async void StopMusicThread()
        {
            try {
                semaphore.Release();
            } catch(Exception ex) { }
            _cancellationTokenSource.Cancel();
            _stop = true;
            thread.Join();
            thread = null;
        }

        public static async Task BotSpeak(string text)
        {
            await BudgetQuickplayCommand((await CreateFileText(text, 0), songType.Voice));
        }

        public async static Task BudgetQuickplayCommand((string, songType) path)
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
        private static Process CreateStream((string, songType) path)
        {
            if (_currentProcess != null)
            {
                _currentProcess.Dispose();
            }
            float volume = 1f;
            if (path.Item2 == songType.ytSong)
            {
                volume = 0.7f;
            }

#pragma warning disable CS8603 // Possible null reference return.
            return Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel panic -i \"{path.Item1}\" -af \"volume={volume.ToString("0.0")}\" -ac 2 -f s16le -ar 48000 pipe:1",
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
                
                using (_currentProcess = CreateStream((path, songType.ytSong)))
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

        public static async Task SendAsyncYT(IAudioClient client, (string , songType) path, int depth = 0)
        {
            try
            {
                using (var currentProcess = CreateStream(IsFromYoutube(path.Item1) ? (await CreateFileFromYt(path.Item1, depth),songType.ytSong) : path))
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

        public static IEnumerable<(string, songType)> TryAsPlaylist(string url)
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
                yield return (vid.Url, songType.ytSong);
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
