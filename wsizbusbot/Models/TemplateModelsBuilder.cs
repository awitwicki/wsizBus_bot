using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Telegram.Bot.Types.ReplyMarkups;

namespace wsizbusbot.Models
{
    public static class TemplateModelsBuilder
    {
        public static InlineKeyboardButton[] WaysForBus()
        {
            return new []
            {
                InlineKeyboardButton.WithCallbackData(Local.GetWayDisplayName(Way.ToCTIR), $"{Commands.GetScheduleForMonth}?{Commands.Direction}={Way.ToCTIR.ToString()}"),
                InlineKeyboardButton.WithCallbackData(Local.GetWayDisplayName(Way.ToRzeszow), $"{Commands.GetScheduleForMonth}?{Commands.Direction}={Way.ToRzeszow.ToString()}")
            };
        }
        public static InlineKeyboardMarkup BuildStartMenuMarkup()
        {
            return new InlineKeyboardMarkup(new[]
            {
                WaysForBus(),
                 new [] // first row
                {
                    InlineKeyboardButton.WithCallbackData("Weather", $"{Commands.RefreshWeather}")
                },
                new [] // first row
                {
                    InlineKeyboardButton.WithCallbackData("🇵🇱", $"{Commands.ChangeLanguage}?{Commands.Language}=pl"),
                    InlineKeyboardButton.WithCallbackData("🇺🇦", $"{Commands.ChangeLanguage}?{Commands.Language}=ua"),
                    InlineKeyboardButton.WithCallbackData("🇬🇧", $"{Commands.ChangeLanguage}?{Commands.Language}=en"),
                }
            });
        }
        public static InlineKeyboardMarkup UsersStatsMarkup()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new [] // first row
                {
                    InlineKeyboardButton.WithCallbackData("Refresh", Commands.RefreshUsers),
                },
            });
        }
        public static InlineKeyboardMarkup RefreshWeather(int language)
        {
            return new InlineKeyboardMarkup(new[]
            {
                new []
                {
                    InlineKeyboardButton.WithCallbackData(Local.Menu[language], Commands.GetStartMenu),
                },
                new []
                {
                    InlineKeyboardButton.WithCallbackData("Refresh", $"{Commands.RefreshWeather}?{Commands.IsEdit}=true"),
                }
            });
        }
        public static InlineKeyboardMarkup StatsMarkup()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new [] // first row
                {
                    InlineKeyboardButton.WithCallbackData("Refresh", Commands.RefreshStats),
                },
            });
        }
        public static InlineKeyboardMarkup BusStations()
        {
            var locationsInline = new List<List<InlineKeyboardButton>>();
            foreach (var station in Local.BusPointNames)
            {
                locationsInline.Add(
                    new List<InlineKeyboardButton>() { 
                        InlineKeyboardButton.WithCallbackData($"{station.Value}", $"{Commands.GetBusStation}?{Commands.BusStation}={(int)station.Key}") 
                    });
            }

            return new InlineKeyboardMarkup(locationsInline);
        }
        public static string GetTopUsers()
        {
            string users = ApplicationData.Users.Set().Count > 0 ? "*Top 50 users list:*\n" : "Users list is empty";

            var topUsers = ApplicationData.Users.Set().Where(u => u.ActiveAt > DateTime.UtcNow.AddDays(-7)).OrderByDescending(u => u.ActiveAt).Take(50).ToList();

            foreach (var u in topUsers)
            {
                users += $"{Local.LangIcon[u.GetLanguage]} {u.Name} `{u.Id}` @{(u.UserName != null ? u.UserName : "hidden")}  {u.ActiveAt.ToString("dd/MM/yy")}\n";
            }

            return users;
        }
    }
}
