using System;
using System.Collections.Generic;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;

namespace wsizbusbot.Controllers
{
    public class InlineQueryController
    {
        public TelegramBotClient Bot { get; set; }
        public CoreBotUser User { get; set; }
        public InlineQueryEventArgs InlineQueryEventArgs { get; set; }

        public async void Execute()
        {
            if (User == null)
            {
                await Bot.AnswerInlineQueryAsync(inlineQueryId: InlineQueryEventArgs.InlineQuery.Id, results: new InlineQueryResultBase[] { }, switchPmParameter: "start", switchPmText: "Start bot");
                return;
            }

            //check if user banned!
            if (User.IsBanned())
                return;

            var weather = await WeatherHelper.GetWeather(User.GetLanguage);
            weather = weather.Replace("_", "\\_");

            InlineQueryResultBase[] results = {
               // displayed result
               new InlineQueryResultArticle(
                    id: "0",
                    title: Local.Weather[User.GetLanguage],
                    inputMessageContent: new InputTextMessageContent(weather)
                    {
                        ParseMode = ParseMode.Markdown
                    }
                ),
                new InlineQueryResultArticle(
                    id: "1",
                    title: $"{Local.TodayTo[User.GetLanguage]} CTIR",
                    inputMessageContent: new InputTextMessageContent(ScheduleHelper.GenerateSchedule(DateTime.Today, Way.ToCTIR, User.GetLanguage).Replace("_", "\\_"))
                    {
                        ParseMode = ParseMode.Markdown
                    }
                ),
                new InlineQueryResultArticle(
                    id: "2",
                    title: $"{Local.TodayTo[User.GetLanguage]} Rzeszow",
                    inputMessageContent: new InputTextMessageContent(ScheduleHelper.GenerateSchedule(DateTime.Today, Way.ToRzeszow, User.GetLanguage).Replace("_", "\\_"))
                    {
                        ParseMode = ParseMode.Markdown
                    }
                ),
                new InlineQueryResultArticle(
                    id: "3",
                    title: $"{Local.TomorrowTo[User.GetLanguage]} CTIR",
                    inputMessageContent: new InputTextMessageContent(ScheduleHelper.GenerateSchedule(DateTime.Today.AddDays(1), Way.ToCTIR, User.GetLanguage).Replace("_", "\\_"))
                    {
                        ParseMode = ParseMode.Markdown
                    }
                ),
                new InlineQueryResultArticle(
                    id: "4",
                    title: $"{Local.TomorrowTo[User.GetLanguage]} Rzeszow",
                    inputMessageContent: new InputTextMessageContent(ScheduleHelper.GenerateSchedule(DateTime.Today.AddDays(1), Way.ToRzeszow, User.GetLanguage).Replace("_", "\\_"))
                    {
                        ParseMode = ParseMode.Markdown
                    }
                )
            };

            await Bot.AnswerInlineQueryAsync(inlineQueryId: InlineQueryEventArgs.InlineQuery.Id, results: results, cacheTime: 300);
        }
    }
}
