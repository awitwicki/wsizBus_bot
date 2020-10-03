using System;
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
        public static string UsersFilePath = DataPath + "users.json";
        public static string StatsFilePath = DataPath + "stats.json";

        //logs
        public static string LogsPath = DataPath + @"logs/";
        public static string LogsFileName = LogsPath + "log.txt";

        //monitoring influxDB
        public static readonly bool UseInfluxDB = false;
        public static string InfluxDBDConnectionString = "http://[your db ip]:8086";
        public static string InfluxDBDbName = "mydb";
        public static string InfluxDBUserName = "admin";
        public static string InfluxDBPassword = "admin";
    }
}
