﻿using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using VainBot.Classes;
using VainBot.Infrastructure;
using VainBot.Services;

namespace VainBot
{
    public class Program
    {
        public static void Main() => new Program().MainAsync().GetAwaiter().GetResult();

        DiscordSocketClient _client;
        IConfiguration _config;
        bool _isDev;

        public async Task MainAsync()
        {
            _config = BuildConfig();

            _isDev = Environment.GetEnvironmentVariable("VB_DEV") != null;

            _client = new DiscordSocketClient(
                new DiscordSocketConfig
                {
                    LogLevel = LogSeverity.Warning
                });

            var services = ConfigureServices();

            await SetUpDB(services.GetRequiredService<VbContext>());
            await services.GetRequiredService<CommandHandlingService>().InitializeAsync();

            _client.Ready += async () =>
            {
                await services.GetRequiredService<ReminderService>().InitializeAsync();
                await services.GetRequiredService<TwitchService>().InitializeAsync();
                await services.GetRequiredService<YouTubeService>().InitializeAsync();
                await services.GetRequiredService<UserService>().InitializeAsync();

                if (!_isDev)
                    await services.GetRequiredService<TwitterService>().InitializeAsync();
            };

            await _client.LoginAsync(TokenType.Bot, _config["discord_api_token"]);
            await _client.StartAsync();

            await Task.Delay(-1);
        }

        IServiceProvider ConfigureServices()
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.Clear();
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("VainBotDiscord", "2.0"));
            httpClient.Timeout = TimeSpan.FromSeconds(5);

            return new ServiceCollection()
                .Configure<Configs.TwitterConfig>(_config.GetSection("Twitter"))
                .Configure<Configs.FitzyConfig>(_config.GetSection("Fitzy"))
                .AddSingleton(_client)
                .AddSingleton(new InteractiveService(_client))
                .AddSingleton<CommandService>()
                .AddSingleton<CommandHandlingService>()
                .AddSingleton<TwitchService>()
                .AddSingleton<YouTubeService>()
                .AddSingleton<TwitterService>()
                .AddSingleton<ReminderService>()
                .AddSingleton<UserService>()
                .AddSingleton(httpClient)
                .AddLogging(o => o.AddConsole())
                .Replace(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(TimedLogger<>)))
                .AddSingleton(_config)
                .AddDbContext<VbContext>(o => o.UseNpgsql(_config["connection_string"]), ServiceLifetime.Transient)
                .BuildServiceProvider();
        }

        IConfiguration BuildConfig()
        {
            return new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config.json")
                .Build();
        }

        // https://stackoverflow.com/a/15228558/1672458
        async Task SetUpDB(VbContext db)
        {
            foreach (var key in typeof(KeyValueKeys).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (key.IsLiteral && !key.IsInitOnly)
                {
                    var val = (string)key.GetRawConstantValue();
                    var kv = await db.FindAsync<KeyValue>(val);
                    if (kv == null)
                    {
                        db.Add(new KeyValue(val, ""));
                        await db.SaveChangesAsync();
                    }
                }
            }
        }
    }
}
