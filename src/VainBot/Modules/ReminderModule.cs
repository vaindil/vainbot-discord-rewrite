﻿using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VainBot.Services;

namespace VainBot.Modules
{
    [Group("reminder")]
    [Alias("remindme", "remind")]
    public class ReminderModule : ModuleBase
    {
        private readonly ReminderService _reminderSvc;

        private readonly Regex _validDelay = new Regex("^[dhm0-9]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private const string UseHelpIfNeededError = "Use `!reminder help` if you need it.";
        private const string TooFarIntoFutureError = "I don't think you need a reminder more than two years into the future.";
        private const string OverflowError = "You can't overflow me, I'm better than that.";

        public ReminderModule(ReminderService reminderSvc)
        {
            _reminderSvc = reminderSvc;
        }

        [Command]
        [Alias("help")]
        [Priority(3)]
        public async Task Help()
        {
            await ReplyAsync("Get a reminder in a certain amount of time.\n" +
                "Example: `!reminder 12h5m My message here`\n" +
                "You can specify a combination of days, hours, and minutes. Valid examples include:\n" +
                "```\n" +
                "1h22m\n" +
                "27h96m\n" +
                "1d4h32m\n" +
                "4d8m\n" +
                "```");
        }

        [Command]
        [Priority(1)]
        public async Task Invalid([Remainder]string blah)
        {
            await ReplyAsync("Invalid command. " + UseHelpIfNeededError);
        }

        [Command]
        [Priority(2)]
        public async Task CreateReminder(string delay, [Remainder]string message)
        {
            if (message.Length > 500)
            {
                await ReplyAsync("Reminder message must be 500 characters or fewer.");
                return;
            }

            TimeSpan delayTs;
            try
            {
                delayTs = await ParseDelay(delay);
            }
            catch (Exception ex)
            {
                await ReplyAsync(ex.Message);
                return;
            }

            await _reminderSvc.CreateReminderAsync(
                Context.Message.Author.Id, Context.Channel.Id, Context.Message.Id, Context.Guild?.Id, message, delayTs);

            var finalTime = DateTime.UtcNow.Add(delayTs);
            var finalTimeString = finalTime.ToString("HH:mm") + " on " + finalTime.ToString("yyyy-MM-dd") + " UTC";
            await ReplyAsync($"{Context.Message.Author.Mention}: Reminder set for {delay} from now ({finalTimeString}).");
        }

        private async Task<TimeSpan> ParseDelay(string delay)
        {
            if (string.IsNullOrWhiteSpace(delay))
                throw new Exception("Delay string cannot be empty. " + UseHelpIfNeededError);

            delay = delay.ToLower();

            if (!_validDelay.IsMatch(delay))
            {
                if (delay.Contains('-'))
                    throw new Exception("Invalid delay string. Negative numbers are not allowed. " + UseHelpIfNeededError);

                if (delay.Contains('.'))
                    throw new Exception("Invalid delay string. Decimals are not allowed. " +  UseHelpIfNeededError);

                throw new Exception("Invalid delay string. " + UseHelpIfNeededError);
            }

            var newDelay = delay.Replace("d", "d|").Replace("h", "h|").Replace("m", "m|");
            var split = newDelay.Split('|');

            var days = Array.Find(split, s => s.Contains('d'));
            var hours = Array.Find(split, s => s.Contains('h'));
            var minutes = Array.Find(split, s => s.Contains('m'));

            var target = TimeSpan.Zero;
            if (days != null)
            {
                int numDays;
                try
                {
                    numDays = int.Parse(days.TrimEnd('d'));
                }
                catch (OverflowException)
                {
                    throw new Exception(OverflowError);
                }

                if (numDays > 730)
                    throw new Exception(TooFarIntoFutureError);

                target = target.Add(TimeSpan.FromDays(numDays));
            }
            if (hours != null)
            {
                int numHours;
                try
                {
                    numHours = int.Parse(hours.TrimEnd('h'));
                }
                catch (OverflowException)
                {
                    throw new Exception(OverflowError);
                }

                if (numHours > 17520)
                    throw new Exception(TooFarIntoFutureError);

                target = target.Add(TimeSpan.FromHours(numHours));
            }
            if (minutes != null)
            {
                int numMinutes;
                try
                {
                    numMinutes = int.Parse(minutes.TrimEnd('m'));
                }
                catch (OverflowException)
                {
                    throw new Exception(OverflowError);
                }

                if (numMinutes > 1051200)
                    throw new Exception(TooFarIntoFutureError);

                target = target.Add(TimeSpan.FromMinutes(numMinutes));
            }

            if (target > TimeSpan.FromMinutes(1051200))
                throw new Exception(TooFarIntoFutureError);

            if (target == TimeSpan.Zero)
            {
                var ownerId = (await Context.Client.GetApplicationInfoAsync()).Owner.Id;
                if (Context.User.Id != ownerId)
                    throw new Exception("You can't set a reminder for right now, that defeats the purpose.");
            }

            return target;
        }
    }
}
