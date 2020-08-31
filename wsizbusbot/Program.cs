using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using System.ComponentModel.DataAnnotations;
using Telegram.Bot.Types.InputFiles;
using wsizbusbot.Controllers;
using System.Reflection;
using Telegram.Bot.Types;

namespace wsizbusbot
{
  class Program
    {
        
        static void Main(string[] args)
        {
            ApplicationData.StopMode = !ScheduleHelper.ParseFiles().Result;

            var corebot = new CoreBot(Config.TelegramAccessToken);


            if (ApplicationData.StopMode)
                corebot.SendMessageAsync(Config.AdminId, "Error with file parsing", ParseMode.Markdown);

            while (true) { }
            Console.ReadLine();
        }
    }
}