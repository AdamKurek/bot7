using System;
using System.Collections.Generic;
using System.Text;
using Discord.Commands;
using YoutubeExplode;
using YoutubeExplode.Search;

namespace bot7
{
    public class MusicSearchModule : ModuleBase<SocketCommandContext>
    {
        private readonly YoutubeClient _youtubeClient = new YoutubeClient();
        private const int DefaultResultCount = 5;
        private const int MaxResultCount = 15;

        [Command("musicsearch")]
        [Alias("music", "findmusic")]
        [Summary("Searches YouTube for top music videos or playlists matching the query and returns their links.")]
        public Task MusicSearchAsync([Remainder] string query)
            => MusicSearchAsync(DefaultResultCount, query);

        [Command("musicsearch")]
        [Alias("music", "findmusic")]
        public async Task MusicSearchAsync(int resultCount, [Remainder] string query)
        {
            query = query?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(query))
            {
                await ReplyAsync("Please provide the type of music you want me to search for, e.g. `!musicsearch sad music`.");
                return;
            }

            if (resultCount <= 0)
            {
                await ReplyAsync("Please provide a positive number of results to return.");
                return;
            }

            resultCount = Math.Min(resultCount, MaxResultCount);

            var results = new List<(string Title, string Url, string Type)>();

            await foreach (var result in _youtubeClient.Search.GetResultsAsync(query))
            {
                switch (result)
                {
                    case VideoSearchResult video:
                        results.Add((video.Title, $"https://www.youtube.com/watch?v={video.Id}", "Song"));
                        break;
                    case PlaylistSearchResult playlist:
                        results.Add((playlist.Title, $"https://www.youtube.com/playlist?list={playlist.Id}", "Playlist"));
                        break;
                    default:
                        continue;
                }

                if (results.Count >= resultCount)
                {
                    break;
                }
            }

            if (results.Count == 0)
            {
                await ReplyAsync($"I couldn't find any music results for `{query}`.");
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine($"Top {results.Count} YouTube results for `{query}`:");
            for (int i = 0; i < results.Count; i++)
            {
                var (title, url, type) = results[i];
                builder.AppendLine($"{i + 1}. [{type}] {title} - {url}");
            }

            await ReplyAsync(builder.ToString());
        }
    }
}
