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
using Whisper.net;

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
        private readonly static string defaultSong = "https://www.youtube.com/watch?v=woNw5Dyqhzo";

        public static IAudioClient? audioClient;
        public static Thread? thread;
        public static Thread? recordingThread;
        public static bool _stop = false;
        private static YoutubeClient youtube = new YoutubeClient();
        static SongsQueuee<(string, songType)> queue = new((defaultSong, songType.ytSong));
        static SocketVoiceState? voiceState;
        static Semaphore PlaySemaphore = new(1, 1);
        static bool cutIn = false;

        internal async Task SongsThread()
        {
            try
            {
                if (audioClient == null)
                {
                    //var discordstream = client.CreatePCMStream(AudioApplication.Mixed)
                    audioClient = await voiceState.Value.VoiceChannel.ConnectAsync(false, false, false, false);
                }
                ResetToken();
                while (true)
                {
                    var token = _cancellationTokenSource.Token;
                    var currUrl = queue.Dequeue();
                    await ReplyAsync("playing " + currUrl.Item1);
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
            List<Thread> listeningThreads = new List<Thread>();
            var users = channel.GetUsersAsync();
            await foreach (var userCollection in channel.GetUsersAsync())
            {
                foreach (var user in userCollection)
                {
                    if (user.IsBot || user is not SocketGuildUser guilduser || guilduser.AudioStream == null)
                    {
                        continue;
                    }
                    listeningThreads.Add(new Thread(async () =>
                    {
                        for (int i = 0; true; i++)
                        {
                            var filePath = $"outputAudioFile{user.GlobalName}{i++}.webm";
                            if (File.Exists(filePath))
                            {
                                File.Delete(filePath);
                            }
                            string ffmpegPath = "ffmpeg";
                            string arguments = $"-y -f s16le -ar 48000 -ac 2 -i pipe:0 -c:a libvorbis {filePath}";
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
                                            await ffmpegProcess.StandardInput.WriteLineAsync("q");
                                            ffmpegProcess.WaitForExit();
                                            await stdin.FlushAsync();
                                        }
                                    }
                                }
                            }
                        }
                    }));
                }
                foreach (var thread in listeningThreads)
                {
                    thread.Start();
                }
            }
        }

        [Command("play")]
        [Alias("p")]
        public async Task PlayCommand(string url = "https://www.youtube.com/watch?v=woNw5Dyqhzo")
        {
            _stop = false;
            if (!await JoinChannel())
            {
                return;
            }
            try
            {
                var urls = TryAsPlaylist(url);
                if (urls.Count() > 0)
                {
                    queue.AppendFront(urls);
                }
                else
                {
                    queue.insert(0, (url, songType.ytSong));
                }
                RunOrContinueSongsThread();
                _cancellationTokenSource.Cancel();
                ResetToken();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private static void ResetToken()
        {
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = new();
        }

        [Command("enqueue")]
        [Alias("pushback", "enq", "q", "potem", "queue")]
        public async Task QCommand(string url = "https://www.youtube.com/watch?v=woNw5Dyqhzo")
        {
            if(!await JoinChannel())
            {
                return;
            }
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
                RunOrContinueSongsThread();
            }
            catch (Exception e)
            {
                Console.WriteLine("t " + e.Message);
            }
        }

        private async Task<bool> JoinChannel()
        {
            if (Context.User is not SocketGuildUser guildUser)
            {
                await ReplyAsync("This command can only be used in a server.");
                return false;
            }
            voiceState = guildUser.VoiceState;
            if ((voiceState?.VoiceChannel) == null)
            {
                await ReplyAsync("VC.");
                return false;

            }
            return true;
        }

        [Command("quickplay")]
        [Alias("qp", "slide", "cutin")]
        public async Task QuickplayCommand(string url = "https://www.youtube.com/watch?v=woNw5Dyqhzo")
        {
            _stop = false;
            if (!await JoinChannel())
            {
                return;
            }
            try
            {
                EnqueueYtSongOrPlaylist(url);
                RunOrContinueSongsThread();
                cutIn = true;
                _cancellationTokenSource.Cancel();
            }
            catch (Exception e)
            {
                Console.WriteLine("t " + e.Message);
            }
        }

        private static void EnqueueYtSongOrPlaylist(string url)
        {
            var urls = TryAsPlaylist(url);
            if (urls.Count() > 0)
            {
                queue.AppendFront(urls);
            }
            else
            {
                queue.insert(0, (url, songType.ytSong));
            }
        }

        [Command("set default")]
        [Alias("default", "d", "kółkuj", "loop")]
        public async Task SetDefault(string url = "https://www.youtube.com/watch?v=woNw5Dyqhzo")
        {
            queue.DefaultSong = (url, songType.ytSong);
        }

        [Command("list")]
        public async Task ListCommand()
        {
            string mess = "queue:\n";
            int count = 0;
            foreach (var s in queue)
            {
                mess += s.Item1 + '\n';
                count++;
                if (count % 20 == 0)
                {
                    await ReplyAsync(mess);
                    mess = "";
                }
            }
            await ReplyAsync(mess);
        }

        [Command("skip")]
        [Alias("next", "s")]
        public async Task SkipCommand()
        {
            SkipSong();
        }

        [Command("empty queue")]
        [Alias("refresh", "refresh queue")]
        public async Task RefreshCommand()
        {
            queue = new((defaultSong, songType.ytSong));
        }

        private void RunOrContinueSongsThread()
        {
            if (thread == null)
            {
                createRunThread();
                return;
            }
            if (false)//TODO
            {
                thread.Join();
                createRunThread();
                return;
            }
            return;

            void createRunThread()
            {
                thread = new Thread(async () => { await SongsThread(); });
                thread.Start();
                return;
            }
        }

        private static void SkipSong()
        {
            var currentTokenSource = _cancellationTokenSource;
            //ResetToken();
            _cancellationTokenSource = new CancellationTokenSource();
            currentTokenSource.Cancel();
            currentTokenSource.Dispose();
        }

        [Command("pause")]
        public async Task PauseCommand()
        {
            _cancellationTokenSource.Cancel();
            PlaySemaphore.WaitOne();
        }

        [Command("resume")]
        [Alias("r")]
        public async Task ResumeCommand()
        {
            PlaySemaphore.Release();
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
            recordingThread.Join();
        }

        [Command("reco")]
        [Alias("rec stop")]
        public async Task RecordStopCommand()
        {
            _recordingCancellationTokenSource.Cancel();
        }

        [Command("text")]
        public async Task TextCommand()
        {
            var modelName = "ggml-medium.bin"; // Specify the model name you want to download
                                               //if (!File.Exists(modelName))
                                               //{
                                               //    using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(GgmlType.Base); // Adjust GgmlType.Base to the type of model you need
                                               //    using var fileWriter = File.OpenWrite(modelName);
                                               //    await modelStream.CopyToAsync(fileWriter);
                                               //}
            string inputFile = "outputAudioFilekaczek0.webm"; // Path to the recorded WebM file
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
                using var whisperFactory = WhisperFactory.FromPath("Models/ggml-small.bin");
                using var processor = whisperFactory.CreateBuilder()
                    .WithLanguage("pl")
                    .Build();

                using var fileStream = File.OpenRead("outputFile.wav");

                await foreach (var result in processor.ProcessAsync(fileStream))
                {
                    Console.WriteLine($"{result.Start}->{result.End}: {result.Text}");
                    await ReplyAsync($"{result.Text}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("ebege = " + e);
            }
        }

        async void StopMusicThread()
        {
            try
            {
                PlaySemaphore.Release();
            }
            catch (Exception ex) { }
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

            try
            {
                var streamManifest = await youtube.Videos.Streams.GetManifestAsync(url);
                var streamInfo = streamManifest.GetAudioOnlyStreams().TryGetWithHighestBitrate();
                string file = $"audio{iteration}.{streamInfo.Container}";
                if (streamInfo != null)
                {
                    await youtube.Videos.Streams.DownloadAsync(streamInfo, file);
                }
                return file;
            }
            catch (Exception e)
            {
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
                return "failed creating file";
            }
        }

        static AudioOutStream? discordstream = null;
        public static async Task SendAsyncYT(IAudioClient client, (string, songType) path, int depth = 0)
        {
            try
            {
                using (var currentProcess = CreateStream(IsFromYoutube(path.Item1) ? (await CreateFileFromYt(path.Item1, depth), songType.ytSong) : path))
                using (var output = currentProcess.StandardOutput.BaseStream)
                {
                    if (discordstream == null)
                    {
                        discordstream = client.CreatePCMStream(AudioApplication.Mixed);
                    }
                    try
                    {
                        do
                        {
                            if (cutIn)
                            {
                                cutIn = false;
                                await SendAsyncYT(client, queue.Dequeue(), depth + 1);
                            }
                            ResetToken();
                            if (_stop)
                            {
                                _stop = false;
                                break;
                            }
                            PlaySemaphore.WaitOne();
                            try
                            {
                                await output.CopyToAsync(discordstream, _cancellationTokenSource.Token);
                            }
                            catch (Exception e) { }
                            finally
                            {
                                PlaySemaphore.Release();
                            }
                        } while (_cancellationTokenSource.IsCancellationRequested);
                    }
                    finally
                    {
                        if (depth == 0)
                        {
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
