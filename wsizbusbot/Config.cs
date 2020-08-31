﻿using System;
using System.Collections.Generic;
using System.Text;

namespace wsizbusbot
{
    public static class Config
    {
        public static string TelegramAccessToken = "TELEGRAM BOT TOKEN";
        public static string OpenWeatherMapAppId = "OpenWeatherMap AppId";
        public static long AdminId = 0; //type your telegram Id

        public static string DataPath = @"data/";
        public static string UsersFilePath = DataPath + @"users.json";
        public static string StatsFilePath = DataPath + @"stats.json";
    }
}
