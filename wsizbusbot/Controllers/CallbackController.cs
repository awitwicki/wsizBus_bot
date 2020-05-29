using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using wsizbusbot.Models;

namespace wsizbusbot.Controllers
{
    public class CallbackController: BaseController
    {
        //Change language
        public void ChangeLanguage(CallbackQueryEventArgs callbackQueryEventArgs)
        {
            var user = ApplicationData.GetUser(callbackQueryEventArgs);
            var callbackQuery = callbackQueryEventArgs.CallbackQuery;

            var args = ArgParser.ParseCallbackData(callbackQueryEventArgs.CallbackQuery.Data);

            var lang = args.GetValueOrDefault(Commands.Language);

            LocalLanguage newLanguage = LocalLanguage.English;
            if (lang == "ua")
                newLanguage = LocalLanguage.Ukrainian;
            else if (lang == "pl")
                newLanguage = LocalLanguage.Polish;

            //Save User Language
            if (user.Language != newLanguage)
            {
                user.Language = newLanguage;
                ApplicationData.SaveUsers();
            }

            string messageText = Local.StartString[user.GetLanguage] + $"Bot version `{ApplicationData.BotVersion}`";
            var inlineKeyboard = TemplateModelsBuilder.BuildStartMenuMarkup();

            CoreBot.EditMessageText(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, messageText, replyMarkup: inlineKeyboard, parseMode: ParseMode.Markdown);
        }

        //Refresh stats
        [Role(Acceess.Admin)]
        public void RefreshStats(CallbackQueryEventArgs callbackQueryEventArgs)
        {
            var user = ApplicationData.GetUser(callbackQueryEventArgs);
            var callbackQuery = callbackQueryEventArgs.CallbackQuery;

            string stats = $"*Monthly Users:* `{ApplicationData.Users.Set().Count(u => u.ActiveAt > DateTime.UtcNow.AddDays(-30))}`\n";
            stats += $"*Weekly Users:* `{ApplicationData.Users.Set().Count(u => u.ActiveAt > DateTime.UtcNow.AddDays(-7))}`\n\n";

            stats += (ApplicationData.Users.Set().Count() > 0 ? "*Activity Days* list:\n" : "*Activity Days* list is empty");
            foreach (var stat in ApplicationData.Stats.Set().OrderByDescending(s => s.Date).Where(s => s.Date > DateTime.UtcNow.AddDays(-7)))
            {
                stats += $"{stat.Date.ToString("dd/MM/yy")} - `{stat.UserId.Count}` - `{stat.ActiveClicks}`\n";
            }

            stats += "\n*Languages:*\n";
            stats += $"🇬🇧 - {ApplicationData.Users.Set().Count(u => u.Language == LocalLanguage.English)}";
            stats += $" 🇺🇦 - {ApplicationData.Users.Set().Count(u => u.Language == LocalLanguage.Ukrainian)}";
            stats += $" 🇮🇩 - {ApplicationData.Users.Set().Count(u => u.Language == LocalLanguage.Polish)}";

            stats += $"\n\n`{DateTime.UtcNow}`";

            var inlineKeyboard = TemplateModelsBuilder.StatsMarkup();

            CoreBot.EditMessageText(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, stats, ParseMode.Markdown, replyMarkup: inlineKeyboard);
        }

        //Refresh users
        [Role(Acceess.Admin)]
        public void RefreshUsers(CallbackQueryEventArgs callbackQueryEventArgs)
        {
            var user = ApplicationData.GetUser(callbackQueryEventArgs);
            var callbackQuery = callbackQueryEventArgs.CallbackQuery;

            var users = TemplateModelsBuilder.GetTopUsers();
            users += $"\n`{DateTime.UtcNow}`";

            var inlineKeyboard = TemplateModelsBuilder.UsersStatsMarkup();

            CoreBot.EditMessageText(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, users, ParseMode.Markdown, replyMarkup: inlineKeyboard);
        }

        //Start menu
        public void GetStartMenu(CallbackQueryEventArgs callbackQueryEventArgs)
        {
            var user = ApplicationData.GetUser(callbackQueryEventArgs);
            var callbackQuery = callbackQueryEventArgs.CallbackQuery;

            var inlineKeyboard = TemplateModelsBuilder.BuildStartMenuMarkup();

            CoreBot.EditMessageText(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, Local.StartString[user.GetLanguage], replyMarkup: inlineKeyboard);
        }

        //Get schedule for month
        public void GetScheduleForMonth(CallbackQueryEventArgs callbackQueryEventArgs)
        {
            var user = ApplicationData.GetUser(callbackQueryEventArgs);
            var callbackQuery = callbackQueryEventArgs.CallbackQuery;

            var args = ArgParser.ParseCallbackData(callbackQueryEventArgs.CallbackQuery.Data);

            var direction = args.GetValueOrDefault(Commands.Direction);
            string directionName = direction == Way.ToCTIR.ToString() ? Way.ToCTIR.ToString() : Way.ToRzeszow.ToString();

            DateTime activeDate = DateTime.UtcNow;
            var argDate = args.GetValueOrDefault(Commands.Month);
            if (argDate != null)
                activeDate = DateTime.Parse(argDate);

            //Get avaliable days for month
            var monthName = Local.GetMonthNames(user.Language)[activeDate.Month];
            var actualSchedule = ApplicationData.Schedule.Days.Where(d => d.DayDateTime.Month == activeDate.Month && d.DayDateTime.Year == activeDate.Year).ToList();

            var calendarKeyboard = new List<List<InlineKeyboardButton>>();

            if (actualSchedule.Count > 0)
            {
                calendarKeyboard.Add(new List<InlineKeyboardButton>
                {
                    InlineKeyboardButton.WithCallbackData(Local.AnotherMonth[user.GetLanguage], $"{Commands.GetMonths}?{Commands.Direction}={directionName}")
                });

                //Today/tomorrow buttons
                { 
                    var todayAndTomorrowRow = new List<InlineKeyboardButton>();

                    //Add Today
                    if (actualSchedule.FirstOrDefault(s => s.DayDateTime.Date == DateTime.Today) != null)
                        todayAndTomorrowRow.Add(InlineKeyboardButton.WithCallbackData(Local.Today[user.GetLanguage], $"{Commands.GetScheduleForDay}?{Commands.Direction}={directionName},{Commands.Date}={DateTime.Today}"));

                    //Add Tomorrow
                    if (actualSchedule.FirstOrDefault(s => s.DayDateTime.Date == DateTime.Today.AddDays(1)) != null)
                        todayAndTomorrowRow.Add(InlineKeyboardButton.WithCallbackData(Local.Tomorrow[user.GetLanguage], $"{Commands.GetScheduleForDay}?{Commands.Direction}={directionName},{Commands.Date}={DateTime.Today.AddDays(1)}"));

                    if (todayAndTomorrowRow.Count > 0)
                        calendarKeyboard.Add(todayAndTomorrowRow);
                }

                for (int i = 0; i < actualSchedule.Count; i++)
                {
                    if (i == 0 || i % 4 == 0)
                        calendarKeyboard.Add(new List<InlineKeyboardButton>());

                    calendarKeyboard.Last().Add(
                        InlineKeyboardButton.WithCallbackData(
                            $"{actualSchedule[i].DayDateTime.Day}",
                            $"{Commands.GetScheduleForDay}?{Commands.Direction}={directionName},{Commands.Date}={actualSchedule[i].DayDateTime.ToShortDateString()}"
                        ));
                }

                var inlineKeyboard = new InlineKeyboardMarkup(calendarKeyboard);
                var keyboardText = (actualSchedule.Count > 0) ? $"{Local.PickDate[user.GetLanguage]} ({Local.GetMonthNames(user.Language)[activeDate.Month]})" : $"{Local.NoDataForMonth[user.GetLanguage]}(*{Local.GetMonthNames(user.Language)[activeDate.Month]}*) {Local.PickAnother[user.GetLanguage]}";
                CoreBot.EditMessageText(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, keyboardText, ParseMode.Markdown, replyMarkup: inlineKeyboard);
            }
            //If no data for this month, find other months
            else
            {
                foreach (var month in ApplicationData.Schedule.avaliableMonths)
                {
                    calendarKeyboard.Add(new List<InlineKeyboardButton>() { InlineKeyboardButton.WithCallbackData($"{Local.GetMonthNames(user.Language)[month.Month]}", $"{Commands.GetScheduleForMonth}?{Commands.Direction}={directionName},{Commands.Month}={month.ToShortDateString()}") });
                }
                var inlineKeyboard = new InlineKeyboardMarkup(calendarKeyboard);
                var keyboardText = $"{Local.NoDataForMonth[user.GetLanguage]} (*{Local.GetMonthNames(user.Language)[activeDate.Month]}*){Local.PickAnother[user.GetLanguage]}";
                CoreBot.EditMessageText(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, keyboardText, ParseMode.Markdown, replyMarkup: inlineKeyboard);
            }
        }

        //Get Schedule For Day
        public void GetScheduleForDay(CallbackQueryEventArgs callbackQueryEventArgs)
        {
            var user = ApplicationData.GetUser(callbackQueryEventArgs);
            var callbackQuery = callbackQueryEventArgs.CallbackQuery;

            var args = ArgParser.ParseCallbackData(callbackQueryEventArgs.CallbackQuery.Data);

            var directionArg = args.GetValueOrDefault(Commands.Direction);
            var direction = directionArg == Way.ToCTIR.ToString() ? Way.ToCTIR : Way.ToRzeszow;

            DateTime selectedDay = DateTime.Parse(args.GetValueOrDefault(Commands.Date));

            //Get schedule for date
            var text = ScheduleHelper.GenerateSchedule(selectedDay, direction, user.GetLanguage);

            //Send menu to get back
            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new [] //First row
                {
                    InlineKeyboardButton.WithCallbackData(Local.BusStations[user.GetLanguage], Commands.GetBusStation),
                },
                new [] //First row
                {
                    InlineKeyboardButton.WithCallbackData(Local.Weather[user.GetLanguage], $"{Commands.RefreshWeather}"),
                },
                
                TemplateModelsBuilder.WaysForBus()
            });

            CoreBot.SendMessage(callbackQuery.Message.Chat.Id, text, ParseMode.Markdown, replyMarkup: inlineKeyboard);
        }
        
        //Bus stations
        public void GetBusStation(CallbackQueryEventArgs callbackQueryEventArgs)
        {
            var user = ApplicationData.GetUser(callbackQueryEventArgs);
            var callbackQuery = callbackQueryEventArgs.CallbackQuery;

            var args = ArgParser.ParseCallbackData(callbackQueryEventArgs.CallbackQuery.Data);

            var busStation = args.GetValueOrDefault(Commands.BusStation);
            
            //List All stations
            if (busStation == null)
            {
                var stationsInline = TemplateModelsBuilder.BusStations();
                CoreBot.EditMessageText(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, Local.PickBusStation[user.GetLanguage], ParseMode.Markdown, replyMarkup: stationsInline);
            }
            //Get station info
            else
            {
                var stationId = int.Parse(busStation);
                var station = (Local.PointNames)stationId;

                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new [] //First row
                    {
                        InlineKeyboardButton.WithCallbackData(Local.BusStations[user.GetLanguage], Commands.GetBusStation),
                    },
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData(Local.Menu[user.GetLanguage], Commands.GetStartMenu),
                    },
                    TemplateModelsBuilder.WaysForBus()
                });

                CoreBot.DeleteMessage(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);

                CoreBot.SendMessage(callbackQuery.Message.Chat.Id, Local.BusPointNames[station], ParseMode.Markdown);
                CoreBot.SendLocation(callbackQuery.Message.Chat.Id, Local.BusPoints[station][0], Local.BusPoints[station][1]);
                CoreBot.SendMessage(callbackQuery.Message.Chat.Id, Local.ReturnBack[user.GetLanguage], ParseMode.Markdown, replyMarkup: inlineKeyboard);
            }
        }

        //Avaliable months
        public void GetMonths(CallbackQueryEventArgs callbackQueryEventArgs)
        {
            var user = ApplicationData.GetUser(callbackQueryEventArgs);
            var callbackQuery = callbackQueryEventArgs.CallbackQuery;

            var args = ArgParser.ParseCallbackData(callbackQueryEventArgs.CallbackQuery.Data);

            var months = ApplicationData.Schedule.avaliableMonths;

            string directionName = args.GetValueOrDefault(Commands.Direction);

            var monthsKeyboard = new List<List<InlineKeyboardButton>>();

            foreach (var month in months)
            {
                monthsKeyboard.Add(new List<InlineKeyboardButton>() 
                {
                    InlineKeyboardButton.WithCallbackData($"{Local.GetMonthNames(user.GetLanguage)[month.Month]}", $"{Commands.GetScheduleForMonth}?{Commands.Direction}={directionName},{Commands.Month}={month.ToShortDateString()}") 
                });
            }

            var inlineKeyboard = new InlineKeyboardMarkup(monthsKeyboard);

            CoreBot.EditMessageText(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, Local.PickMonth[user.GetLanguage], replyMarkup: inlineKeyboard);
        }

        //Refresh weather
        public async void RefreshWeather(CallbackQueryEventArgs callbackQueryEventArgs)
        {
            var user = ApplicationData.GetUser(callbackQueryEventArgs);
            var message = callbackQueryEventArgs.CallbackQuery.Message;

            var args = ArgParser.ParseCallbackData(callbackQueryEventArgs.CallbackQuery.Data);
            bool isEdit = args.ContainsKey(Commands.IsEdit);

            var weather = await WeatherHelper.GetWeather(user.GetLanguage);
            weather += $"\n`{DateTime.UtcNow.AddHours(2)}`";
            weather += "\n@wsizBus\\_bot";

            var inlineKeyboard = TemplateModelsBuilder.RefreshWeather(user.GetLanguage);

            if (isEdit)
                CoreBot.EditMessageText(message.Chat.Id, message.MessageId, weather, ParseMode.Markdown, replyMarkup: inlineKeyboard);
            else
                CoreBot.SendMessage(message.Chat.Id, weather, ParseMode.Markdown, replyMarkup: inlineKeyboard);
        }
    }
}
