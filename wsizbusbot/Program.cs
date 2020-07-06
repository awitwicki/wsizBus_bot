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
            Directory.CreateDirectory(Config.DataPath);

            ApplicationData.StopMode = !ScheduleHelper.ParseFiles().Result;

            CoreBot.StartReceiving();
            
            var me = CoreBot.Bot.GetMeAsync().Result;
            Console.Title = me.Username;

            CoreBot.SendMessage(Config.AdminId, $"WsizBusBot is started\nBot version `{ApplicationData.BotVersion}.`", ParseMode.Markdown);

            if (ApplicationData.StopMode)
                CoreBot.SendMessage(Config.AdminId, "Error with file parsing", ParseMode.Markdown);

            while (true) { }
            Console.ReadLine();

            CoreBot.Bot.StopReceiving();
        }
    }
}