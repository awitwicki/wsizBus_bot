using System;
using System.Collections.Generic;
using System.Text;

namespace wsizbusbot
{
    public static class Config
    {
        public static string TelegramAccessToken = "TELEGRAM BOT TOKEN";
        public static string BotVersion = "1.3b 290919";
        public static long AdminId = 0; //type your telegram Id
        public static string DataPath = @"..\..\..\data\";
        public static string BlocklistFilePath = DataPath + "blocklist.xml";
        public static string UsersFilePath = DataPath + "users.xml";
    }
}
