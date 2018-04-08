﻿using Discord;
using Discord.Commands;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using VainBot.Configs;
using VainBot.Preconditions;
using VainBot.Services;

namespace VainBot.Modules
{
    [FitzyGuild]
    [RequireUserPermission(GuildPermission.BanMembers)]
    public class FitzyRecordModule : ModuleBase
    {
        private readonly FitzyConfig _config;
        private readonly HttpClient _httpClient;

        private readonly LogService _logSvc;

        public FitzyRecordModule(IOptions<FitzyConfig> options, HttpClient httpClient, LogService logSvc)
        {
            _config = options.Value;
            _httpClient = httpClient;

            _logSvc = logSvc;
        }

        [Command("w")]
        [Alias("win", "wins")]
        public async Task Wins(int num = -1)
        {
            num = NormalizeNum(num);
            var success = await SendApiCallAsync(num, RecordType.wins);

            await HandleReply(success);
        }

        [Command("l")]
        [Alias("loss", "losses", "k", "kill", "kills")]
        public async Task Losses(int num = -1)
        {
            num = NormalizeNum(num);
            var success = await SendApiCallAsync(num, RecordType.losses);

            await HandleReply(success);
        }

        [Command("d")]
        [Alias("draw", "draws", "death", "deaths")]
        public async Task Draws(int num = -1)
        {
            num = NormalizeNum(num);
            var success = await SendApiCallAsync(num, RecordType.draws);

            await HandleReply(success);
        }

        [Command("clear")]
        [Alias("reset")]
        public async Task Clear()
        {
            var success = await SendApiCallAsync(0, RecordType.wins);
            success &= await SendApiCallAsync(0, RecordType.losses);
            success &= await SendApiCallAsync(0, RecordType.draws);

            await HandleReply(success);
        }

        private async Task<bool> SendApiCallAsync(int num, RecordType type)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, $"{_config.ApiBaseUrl}/{type}/{num}");
            request.Headers.Authorization = new AuthenticationHeaderValue(_config.ApiSecret);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                await _logSvc.LogMessageAsync(LogSeverity.Error, $"Fitzy API call failed. Status {response.StatusCode}, body: {body}");
            }

            return response.IsSuccessStatusCode;
        }

        private async Task HandleReply(bool success)
        {
            if (success)
                await ReplyAsync($"{Context.Message.Author.Mention}: Updated successfully");
            else
                await ReplyAsync($"{Context.Message.Author.Mention}: Error occurred while updating. " +
                    "The appropriate authorities have already been notified.");
        }

        private int NormalizeNum(int num)
        {
            if (num < -1)
                num = 0;
            else if (num > 99)
                num = 99;

            return num;
        }

        // breaking conventions and making this lowercase is so much nicer than making them uppercase and
        // converting to lowercase above
        private enum RecordType
        {
            wins,
            losses,
            draws
        }
    }
}
