using Discord.Commands;
using Discord;
using Discord.WebSocket;
using Discord.Audio;
using System.Diagnostics;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode.Playlists;
using System.Speech.Synthesis;
using System.Globalization;
using Whisper.net;
using AngleSharp.Dom;
using System.IO;

namespace bot7
{
    public class VoiceCommands : ModuleBase<SocketCommandContext>
    {

        public static CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        public static CancellationTokenSource _recordingCancellationTokenSource = new CancellationTokenSource();
        private readonly static string defaultSong = "https://www.youtube.com/watch?v=woNw5Dyqhzo";

        public static IAudioClient? audioClient;
        public static Thread? thread;
        public static Thread? recordingThread;
        public static bool _stop = false;
        private static YoutubeClient youtube = new YoutubeClient();
        static SongsQueuee<InQueueSong> queue = new((defaultSong, 0.5f));
        static SocketVoiceState? voiceState;
        static Semaphore PlaySemaphore = new(1, 1);
        static bool cutIn = false;



        internal async Task SongsThread()
        {
            try
            {
                if (audioClient == null)
                {
                    audioClient = await voiceState.Value.VoiceChannel.ConnectAsync(false, false, false, false);
                }
                ResetToken();
                while (true)
                {
                    var token = _cancellationTokenSource.Token;
                    if (queue.empty())
                    {
                        AddSongsFromPlaylistToQueue(queue.DefaultSong.Url, true);
                    }
                    var currUrl = queue.Dequeue();
                    //await ReplyAsync("playing " + currUrl.Url);
                    await Program.SendCurrentPlayingMessage(Context.Channel, $"Playing {currUrl.Url}");
                    await SendAsyncYT(audioClient, currUrl);
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("songs thread: " + e.Message);
            }
        }


        internal async Task RecordingThread()
        {
            try
            {
                _recordingCancellationTokenSource = new();
                await ListenToAudioStream(voiceState.Value.VoiceChannel);
            }
            catch (Exception e)
            {
                Console.WriteLine("recording thread: " + e.Message);
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
                if (!AddSongsFromPlaylistToQueue(url, true))
                {
                    queue.insert(0, (url, 0.5f));
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

        [Command("playdefault")]
        [Alias("pd", "playanddefault")]
        public async Task PlayDefaultCommand(string url = "https://www.youtube.com/watch?v=woNw5Dyqhzo")
        {
            await PlayCommand(url);
            await SetDefaultCommand(url);
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
                if(!AddSongsFromPlaylistToQueue(url))
                {
                    queue.enqueue((url, 0.5f));
                }
                RunOrContinueSongsThread();
            }
            catch (Exception e)
            {
                Console.WriteLine("q command: " + e.Message);
            }
        }

        private bool AddSongsFromPlaylistToQueue(string url, bool front = false)
        {
            var urls = TryAsPlaylist(url);
            if (urls.Count() > 0)
            {
                if (front)
                {
                    queue.AppendFront(urls);
                    return true;
                }
                queue.AppendEnd(urls);
                return true;
            }
            return false;
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
                queue.insert(0, (url, 0.5f));
                RunOrContinueSongsThread();
                cutIn = true;
                _cancellationTokenSource.Cancel();
            }
            catch (Exception e)
            {
                Console.WriteLine("q command: " + e.Message);
            }
        }

        private static void EnqueueFrontYtSongOrPlaylist(string url)
        {
            var urls = TryAsPlaylist(url);
            if (urls.Count() > 0)
            {
                queue.AppendFront(urls);
            }
            else
            {
                queue.insert(0, (url, 0.5f));
            }
        }

        [Command("set default")]
        [Alias("default", "d", "kółkuj", "loop")]
        public async Task SetDefaultCommand(string url = "https://www.youtube.com/watch?v=woNw5Dyqhzo")
        {
            queue.DefaultSong = (url, 0.5f);
        }

        [Command("list")]
        public async Task ListCommand()
        {
            string mess = "queue:\n";
            int count = 0;
            foreach (var s in queue)
            {
                mess += s.Url + '\n';
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
        [Alias("next", "s", "end")]
        public async Task SkipCommand(int count = 1)
        {
            if (count > 0)
            {
                SkipSong();
                queue.RemoveFirst(count);
            }
        }

        [Command("empty queue")]
        [Alias("refresh", "refresh queue")]
        public async Task RefreshCommand()
        {
            queue.Clear();
        }

        private void RunOrContinueSongsThread()
        {
            if (thread == null)
            {
                createRunThread();
                return;
            }
            if(false)//if some bug
            {
                thread.Join(500);
                thread = null;
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
            PausePlaying();
        }

        private static void PausePlaying()
        {
            _cancellationTokenSource.Cancel();
            PlaySemaphore.WaitOne();
        }

        [Command("resume")]
        [Alias("r")]
        public async Task ResumeCommand()
        {
            ResumePlaying();
        }

        private static void ResumePlaying()
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
                Console.WriteLine("textcommand: " + e);
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
            thread?.Join();
            thread = null;
        }

        public static async Task BotSpeak(string text)
        {
            BudgetQuickplayCommand((await CreateFileText(text, 0), 0.5f));
        }

        public static void BudgetQuickplayCommand(InQueueSong path)
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
        private static Process CreateStream((string, float) path)
        {
#pragma warning disable CS8603 // Possible null reference return.
            return Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel panic -i \"{path.Item1}\" -af \"volume={path.Item2.ToString("0.0")}\" -ac 2 -f s16le -ar 48000 pipe:1",
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
                Console.WriteLine("CreatingFileError: " + e.ToString());
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
        public static async Task SendAsyncYT(IAudioClient client, InQueueSong path, int depth = 0)
        {
            try
            {
                using (var currentProcess = CreateStream(IsFromYoutube(path.Url) ? (await CreateFileFromYt(path.Url, depth), path.Volume) : path))
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
                Console.WriteLine("flushbug: " + e.Message);
            }
        }

        private static bool IsFromYoutube(string path)
        {
            return path.Substring(0, 5).ToLower() == "https";
        }

        public static IEnumerable<InQueueSong> TryAsPlaylist(string url)
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
                yield return (vid.Url, 0.5f);
            }
        }

        [Command("ButtonClicked")]

        public async Task YourCommand()
        {
            Console.WriteLine("xd3");
        }
        public static async Task HandleButtonClick(SocketMessageComponent interaction)
        {
            switch(interaction.Data.CustomId)
            
            {
                case "Skip":
                    SkipSong();
                break;
                case "Pause":
                    PausePlaying();
                    await Program.SendCurrentPlayingMessage(interaction.Message.Channel, interaction.Message.Content, true);
                break;
                case "Resume":
                    ResumePlaying();
                    await Program.SendCurrentPlayingMessage(interaction.Message.Channel, interaction.Message.Content, false);
                break;
                //case "Stop":
                //    StopMusicThread();
                //break;
                case "List":
                    //Modal 
                  //  ModalBuilder modalBuilder = new ModalBuilder()
                  //  .WithTitle("urls")
                  //  .Components 

                break;
                case "Clear":
                    queue.Clear();
                break;
            }
        }

    }

    public record struct InQueueSong(string Url, float Volume)
    {
        public static implicit operator (string, float)(InQueueSong value)
        {
            return (value.Url, value.Volume);
        }

        public static implicit operator InQueueSong((string, float) value)
        {
            return new InQueueSong(value.Item1, value.Item2);
        }
    }
}
