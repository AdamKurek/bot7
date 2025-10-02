using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos.Streams;

namespace bot7
{
    public static class YoutubeIntegration
    {
        private static readonly string? CookiesFilePath = ResolveYoutubeCookiesPath();
        private static readonly string YtDlpExecutable = Environment.GetEnvironmentVariable("YTDLP_PATH") ?? "yt-dlp";
        private static readonly YoutubeClient Youtube = CreateYoutubeClient();

        public static bool IsYoutubeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            return url.StartsWith("https", StringComparison.OrdinalIgnoreCase);
        }

        public static IEnumerable<InQueueSong> TryGetPlaylistItems(string url, float defaultVolume)
        {
            IEnumerable<PlaylistVideo> videos;
            try
            {
                videos = Youtube.Playlists.GetVideosAsync(url).ToEnumerable();
            }
            catch
            {
                yield break;
            }

            foreach (var video in videos)
            {
                yield return (video.Url, defaultVolume);
            }
        }

        public static async Task<string> DownloadAudioAsync(string url, int iteration)
        {
            var destinationFile = $"audio{iteration}.webm";

            try
            {
                return await DownloadWithYoutubeExplodeAsync(url, destinationFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine("CreatingFileError (YoutubeExplode): " + ex.Message);
            }

            try
            {
                return await DownloadWithYtDlpAsync(url, destinationFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Alternative YouTube download failed: " + ex.Message);
                return string.Empty;
            }
        }

        private static string? ResolveYoutubeCookiesPath()
        {
            var envPath = Environment.GetEnvironmentVariable("YOUTUBE_COOKIES_PATH");
            if (!string.IsNullOrWhiteSpace(envPath))
            {
                return envPath;
            }

            try
            {
                return Path.Combine(AppContext.BaseDirectory, "youtube_cookies.txt");
            }
            catch
            {
                return "youtube_cookies.txt";
            }
        }

        private static YoutubeClient CreateYoutubeClient()
        {
            try
            {
                var handler = new HttpClientHandler();
                if (TryLoadCookies(handler.CookieContainer))
                {
                    Console.WriteLine($"Using YouTube cookies from {CookiesFilePath}.");
                    var httpClient = new HttpClient(handler, disposeHandler: true);
                    return new YoutubeClient(httpClient);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialise YouTube client with cookies: {ex.Message}");
            }

            return new YoutubeClient();
        }

        private static bool TryLoadCookies(CookieContainer container)
        {
            if (string.IsNullOrWhiteSpace(CookiesFilePath) || !File.Exists(CookiesFilePath))
            {
                return false;
            }

            try
            {
                foreach (var rawLine in File.ReadLines(CookiesFilePath))
                {
                    if (string.IsNullOrWhiteSpace(rawLine))
                    {
                        continue;
                    }

                    var line = rawLine.Trim();
                    if (line.StartsWith("#") && !line.StartsWith("#HttpOnly_", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var parts = line.Split('\t');
                    if (parts.Length < 7)
                    {
                        continue;
                    }

                    var domainPart = parts[0];
                    var isHttpOnly = false;
                    if (domainPart.StartsWith("#HttpOnly_", StringComparison.OrdinalIgnoreCase))
                    {
                        isHttpOnly = true;
                        domainPart = domainPart.Substring("#HttpOnly_".Length);
                    }

                    var trimmedDomain = domainPart.Trim();
                    if (string.IsNullOrWhiteSpace(trimmedDomain))
                    {
                        continue;
                    }

                    var uriDomain = trimmedDomain.TrimStart('.');
                    if (string.IsNullOrWhiteSpace(uriDomain))
                    {
                        continue;
                    }

                    var cookiePath = string.IsNullOrEmpty(parts[2]) ? "/" : parts[2];
                    var secure = string.Equals(parts[3], "TRUE", StringComparison.OrdinalIgnoreCase);
                    var expiresRaw = parts[4];
                    var name = parts[5];
                    var value = parts[6];

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    var cookieDomainValue = trimmedDomain.StartsWith(".") ? $".{uriDomain}" : uriDomain;
                    var cookie = new Cookie(name, value, cookiePath, cookieDomainValue)
                    {
                        Secure = secure,
                        HttpOnly = isHttpOnly
                    };

                    if (long.TryParse(expiresRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var expirySeconds) && expirySeconds > 0)
                    {
                        cookie.Expires = DateTimeOffset.FromUnixTimeSeconds(expirySeconds).UtcDateTime;
                    }

                    try
                    {
                        var uri = new Uri($"https://{uriDomain}/");
                        container.Add(uri, cookie);
                    }
                    catch (Exception cookieException)
                    {
                        Console.WriteLine($"Failed to add YouTube cookie '{name}': {cookieException.Message}");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to read YouTube cookies: {ex.Message}");
                return false;
            }
        }

        private static async Task<string> DownloadWithYoutubeExplodeAsync(string url, string destinationFile)
        {
            var streamManifest = await Youtube.Videos.Streams.GetManifestAsync(url);
            var streamInfo = streamManifest.GetAudioOnlyStreams().TryGetWithHighestBitrate();
            if (streamInfo == null)
            {
                throw new InvalidOperationException("No audio-only streams found for the provided YouTube URL.");
            }

            if (File.Exists(destinationFile))
            {
                File.Delete(destinationFile);
            }

            await Youtube.Videos.Streams.DownloadAsync(streamInfo, destinationFile);
            return destinationFile;
        }

        private static async Task<string> DownloadWithYtDlpAsync(string url, string destinationFile)
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), $"bot7_ytdlp_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);
            var outputTemplate = Path.Combine(tempDirectory, "download.%(ext)s");

            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = YtDlpExecutable,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.StartInfo.ArgumentList.Add("--no-playlist");
            process.StartInfo.ArgumentList.Add("--extract-audio");
            process.StartInfo.ArgumentList.Add("--audio-format");
            process.StartInfo.ArgumentList.Add("webm");
            process.StartInfo.ArgumentList.Add("--audio-quality");
            process.StartInfo.ArgumentList.Add("0");
            process.StartInfo.ArgumentList.Add("--no-progress");

            if (!string.IsNullOrWhiteSpace(CookiesFilePath) && File.Exists(CookiesFilePath))
            {
                process.StartInfo.ArgumentList.Add("--cookies");
                process.StartInfo.ArgumentList.Add(CookiesFilePath);
            }

            process.StartInfo.ArgumentList.Add("-o");
            process.StartInfo.ArgumentList.Add(outputTemplate);
            process.StartInfo.ArgumentList.Add(url);

            var stdOutput = new StringBuilder();
            var stdError = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    stdOutput.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    stdError.AppendLine(e.Data);
                }
            };

            if (File.Exists(destinationFile))
            {
                File.Delete(destinationFile);
            }

            try
            {
                if (!process.Start())
                {
                    throw new InvalidOperationException("Failed to start yt-dlp process.");
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"yt-dlp exited with code {process.ExitCode}: {stdError.ToString().Trim()}");
                }

                var downloadedFile = Directory
                    .GetFiles(tempDirectory)
                    .Where(f =>
                        !f.EndsWith(".part", StringComparison.OrdinalIgnoreCase) &&
                        !f.EndsWith(".info.json", StringComparison.OrdinalIgnoreCase) &&
                        !f.EndsWith(".description", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
                if (downloadedFile == null)
                {
                    throw new FileNotFoundException("yt-dlp did not produce an output file.");
                }

                File.Copy(downloadedFile, destinationFile, true);
                return destinationFile;
            }
            finally
            {
                try
                {
                    Directory.Delete(tempDirectory, true);
                }
                catch
                {
                }
            }
        }
    }
}
