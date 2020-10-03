using OpenWeatherMap;
using System;
using System.Collections.Generic;
using System.Text;

namespace wsizbusbot
{
    public class CoreBotUser
    {
        public string Name { get; set; }
        public string UserName { get; set; }
        public DateTime ActiveAt { get; set; }
        public long Id { get; set; }
        public LocalLanguage Language { get; set; }
        public UserAccess UserAccess { get; set; }
        public int GetLanguage
        {
            get { return (int)Language; }
        }

        public bool IsBanned() => UserAccess == UserAccess.Ban;
    }
    public class Stats
    {
        public DateTime Date { get; set; }
        public long ActiveClicks { get; set; }
        public List<long> UserId { get; set; } = new List<long>();
    }
    public enum UserAccess
    {
        User = 0,
        Admin = 1,
        Ban = 2
    }

    public class WeatherForecast
    {
        public DateTime LastUpdate { get; set; }
        public List<List<ForecastTime>> Forecasts = new List<List<ForecastTime>>();
    }
}
