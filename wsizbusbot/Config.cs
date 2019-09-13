using System;
using System.Collections.Generic;
using System.Text;

namespace wsizbusbot
{
    public static class Config
    {
        public static string TelegramAccessToken = "TELEGRAM BOT TOKEN";
        public static string BotVersion = "1.0a 030919";
        public static long AdminId = 0; //type your telegram Id
        public static string BlocklistFilePath = @"..\..\..\data\blocklist.xml";
        public static string UsersFilePath = @"..\..\..\data\users.xml";
        public static string DataPath = @"..\..\..\data\";
    }
}
