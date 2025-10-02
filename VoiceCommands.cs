using AngleSharp.Dom;
using Concentus;
using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.WebSocket;
using LMStudioNET.Objects.Models;
using Microsoft.VisualBasic;
using NAudio.Wave;
using SmartAiCompendium.Common.Python;
using SmartAICompendium.AudioGeneration.Inferences;
using Swan;
using Swan.Parsers;
using Swan.Threading;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Speech.Synthesis;
using System.Threading;
using Vosk;
using static SpotifyAPI.Web.SearchRequest;
using static System.Net.Mime.MediaTypeNames;

namespace bot7
{
    public class VoiceCommands : ModuleBase<SocketCommandContext>
    {

        public static CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        public static CancellationTokenSource _recordingCancellationTokenSource = new CancellationTokenSource();
        private readonly static string defaultSong = "autism.webm";
        static PiperIntegration piperCaller = new();
        LmStudioCaller lmStudioCaller = new();
        public static IAudioClient? audioClient;
        public static Thread? thread;
        public static Thread? recordingThread;
        public static bool _stop = false;
        static SongsQueuee<InQueueSong> queue = new((defaultSong, 0.5f));
        static SocketVoiceState? voiceState;
        static Semaphore PlaySemaphore = new(1, 1);
        static bool cutIn = false;
        AtomicBoolean isPlayingMusic = new(false);



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
                    using (var process = await fileToProcess(currUrl)) { 
                        await PlaySoundFromProcess(process);
                    }
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
            var model = new Vosk.Model("Models/vosk-model-small-pl-0.22"); // e.g. "models/vosk-model-small-pl-0.22"
            Vosk.Vosk.SetLogLevel(0);
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
                        string grammarJson = "[\"daćme\", \"dis\", \"robul\", \"robula\"]";
                        var rec = new VoskRecognizer(model, 16000.0f);
                        
                        var inputFormat = new WaveFormat(48000, 16, 2);
                        var outputFormat = new WaveFormat(16000, 16, 1);
                        _recordingCancellationTokenSource = new();
                        var tokenSource = new CancellationTokenSource();
                        var audioWithReadMethod = new AudioInStreamAdapter(guilduser.AudioStream, tokenSource);//TODO use token that is used by canceling stream?
                        var inputStream = new RawSourceWaveStream(audioWithReadMethod, inputFormat);
                        var monoProvider = new StereoToMonoProvider16(inputStream);
                        monoProvider.LeftVolume = 0.5f;
                        monoProvider.RightVolume = 0.5f;
                        var resampler = new MediaFoundationResampler(monoProvider, outputFormat)
                        {
                            ResamplerQuality = 60 
                        };
                        string sentence = "";
                        try{
                            while (true){
                                var gotToken = audioWithReadMethod.CreateNewToken(); 
                                byte[] buffer = new byte[3200]; // gpt says 100ms of 16kHz mono 16-bit = 16000 * 0.1 * 2 = 3200 bytes
                                resampler.Read(buffer, 0, buffer.Length);//always reads 3200???
                                if (gotToken.IsCancellationRequested)  {
                                    sentence += VoiceRecognition.ExtractText(rec.FinalResult());
                                    //await BotSpeak(sentence);
                                    if (sentence.Length > 0)
                                    {
                                        var response = await lmStudioCaller.call(sentence);
                                        
                                        Console.WriteLine(sentence);
                                        BotSpeak(response);
                                        Console.WriteLine(response);
                                    }
                                    sentence = "";
                                    gotToken = audioWithReadMethod.CreateNewToken();
                                    if (!gotToken.IsCancellationRequested) 
                                    {
                                        Console.WriteLine("Resetting token");
                                    }
                                    else
                                    {
                                        Console.WriteLine("Failed to reset token");
                                    }
                                }
                                if (rec.AcceptWaveform(buffer, 3200)){
                                    var result = VoiceRecognition.ExtractText(rec.Result());
                                    Console.WriteLine(result);
                                    sentence += result;
                                }
                                else{
                                    Console.WriteLine(VoiceRecognition.ExtractPartialText(rec.PartialResult()));
                                }

                                
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            Console.WriteLine("[ReadTask] Cancelled");
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("[ReadTask] Error: " + e.Message);
                        }
                      
                        return;
                        }
                    ));
                }
                foreach (var thread in listeningThreads)
                {
                    thread.Start();
                }
            }
        }

        [Command("play")]
        [Alias("p")]
        public async Task PlayCommand(string url = "autism.webm")
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
        public async Task PlayDefaultCommand(string url = "autism.webm")
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
        public async Task QCommand(string url = "autism.webm")
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
            var urls = YoutubeIntegration.TryGetPlaylistItems(url, 0.5f).ToList();
            if (urls.Count > 0)
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

        [Command("join")]
        [Alias("come", "chodź", "chodz")]
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
        public async Task QuickplayCommand(string url = "autism.webm")
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
            var urls = YoutubeIntegration.TryGetPlaylistItems(url, 0.5f).ToList();
            if (urls.Count > 0)
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
        public async Task SetDefaultCommand(string url = "autism.webm")
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
            Thread recordingThread = new Thread(() =>
            {
                Thread.CurrentThread.Name = "Recording Thread";
                RecordingThread().GetAwaiter().GetResult(); // Wait synchronously
            });

            recordingThread.Start();

            // Don't call Join immediately — let it run a bit
            Thread.Sleep(1000);
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
        }

        [Command("say")]
        [Alias("powiedzMiDaćme", "czemu")]
        public async Task SayCommand([Remainder] string v)
        {
            var response = await lmStudioCaller.call(v);
            await ReplyAsync(response);
            //await SayCommand(response);
        }

        [Command("ok")]
        public async Task SayCommand()
        {

            _recordingCancellationTokenSource.Cancel();
            _recordingCancellationTokenSource.Dispose();
            _recordingCancellationTokenSource = new CancellationTokenSource();
        }

        [Command("tt")]
        public async Task ttCommand()
        {
            Console.WriteLine("xd");
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

        public void BotSpeak(string text)
        {
            piperCaller.TextToStream(text, "piper.exe", "gosia.onnx");
            RunOrContinueSongsThread();
            cutIn = true;
            _cancellationTokenSource.Cancel();
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
        private static Process? CreateStream((string, float) path)
        {
            var ffmpegPath = @"C:\globals\ffmpeg-6.1.1-full_build\bin\ffmpeg.exe";

            var pathWithPre = $@"{path.Item1}";
            if (!File.Exists(pathWithPre))
            {
                Console.WriteLine("File not found: " + path);
                return null!; 
            }
            return Process.Start(new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-hide_banner -loglevel panic -i \"{pathWithPre}\" -ac 2 -f s16le -ar 48000 pipe:1",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }


        //private static Process CreateStream((string, float) path)
        //{
        //    try
        //    {
        //        var processStartInfo = new ProcessStartInfo
        //        {
        //            FileName = "ffmpeg",
        //            Arguments = $"-hide_banner -loglevel panic -i \"{path.Item1}\" -af \"volume={path.Item2.ToString("0.0")}\" -ac 2 -f s16le -ar 48000 pipe:1",
        //            UseShellExecute = false,
        //            RedirectStandardOutput = true,
        //            RedirectStandardError = true, // Redirect error output
        //        };

        //        var process = new Process
        //        {
        //            StartInfo = processStartInfo,
        //            EnableRaisingEvents = true // Enable events
        //        };

        //        process.OutputDataReceived += (sender, e) =>
        //        {
        //            if (!string.IsNullOrEmpty(e.Data))
        //            {
        //                Console.WriteLine($"Output: {e.Data}");
        //            }
        //        };

        //        process.ErrorDataReceived += (sender, e) =>
        //        {
        //            if (!string.IsNullOrEmpty(e.Data))
        //            {
        //                Console.WriteLine($"Error: {e.Data}");
        //            }
        //        };

        //        process.Start();
        //        process.BeginOutputReadLine(); // Start reading output
        //        process.BeginErrorReadLine(); // Start reading errors

        //        return process;
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Error starting FFmpeg process: {ex.Message}");
        //        return null; // Or handle as needed
        //    }
        //}




        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
        private static string TextToAudioFile(string Text, int iteration)
        {
            string file = $"output{iteration}.wav";

            return PiperIntegration.TextToWav(Text, "piper.exe", "gosia.onnx", file);
        }

        static AudioOutStream? discordstream = null;

        static async Task<Process> fileToProcess(InQueueSong path, int depth = 0)
        {
            return CreateStream(
                YoutubeIntegration.IsYoutubeUrl(path.Url)
                    ? (await YoutubeIntegration.DownloadAudioAsync(path.Url, depth), path.Volume)
                    : path)!;
        }

        public async Task PlaySoundFromProcess(Process currentProcess, int depth = 0)
        {
            try
            {
                if(currentProcess == null)
                {
                    //PlaySemaphore.WaitOne();
                    return;
                }
                using (var output = currentProcess.StandardOutput.BaseStream)
                {
                    if (discordstream == null)
                    {
                        int bitrate = 131_072;//131_072; // XD
                        try
                        {
                            discordstream = audioClient.CreatePCMStream(AudioApplication.Music, bitrate);
                        }
                        catch
                        {
                            Console.WriteLine($"Error creating PCM stream, trying with bitrate {bitrate}");
                            bitrate--;
                        }
                        Console.BackgroundColor = ConsoleColor.Magenta;
                        Console.WriteLine($"bitrate {bitrate}");

                    }
                    try
                    {
                        do
                        {
                            if (cutIn)
                            {
                                cutIn = false;
                                var speach = piperCaller.ReadySpeach;
                                if (speach is object)
                                {
                                    //await speach.StandardOutput.BaseStream.CopyToAsync(discordstream);
                                    int modelHz = piperCaller.modelHz;
                                    var srcFormat = new WaveFormat(modelHz, 16, 1);           
                                    var targetFormat = new WaveFormat(48000, 16, 2);

                                    using var raw = new RawSourceWaveStream(speach.StandardOutput.BaseStream, srcFormat);
                                    using var resampler = new MediaFoundationResampler(raw, targetFormat)
                                    { ResamplerQuality = 60 };

                                    byte[] buf = new byte[targetFormat.AverageBytesPerSecond / 50]; // ~20 ms @ 48k stereo 16-bit (3840 bytes)
                                    int n;
                                    while ((n = resampler.Read(buf, 0, buf.Length)) > 0) { 
                                        await discordstream.WriteAsync(buf.AsMemory(0, n), CancellationToken.None);
                                    }

                                }
                                else
                                {
                                    var process = await fileToProcess(queue.Dequeue(), depth + 1);
                                    await PlaySoundFromProcess(process, depth + 1);
                                }
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
                            catch (Exception e) {
                                Console.WriteLine($"Error Copying to the Discord Stream {e.Message}");
                            }
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
