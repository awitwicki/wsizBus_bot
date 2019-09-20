using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace wsizbusbot
{
    class Program
    {
        private static readonly TelegramBotClient Bot = new TelegramBotClient(Config.TelegramAccessToken);


        public static List<User> Users = new List<User>();
        public static List<long> BlockList = new List<long>();
        public static Schedule schedule = new Schedule();
        public static bool stopMode = false;
        static void Main(string[] args)
        {
            var files = Directory.GetFiles(Config.DataPath, "*.xlsx");
            if (files.Count() == 0)
                stopMode = true;

            var filtered_files = files.Select(x => Convert.ToInt32(Path.GetFileNameWithoutExtension(x).Remove(6))).ToList();
            var lastMonth = filtered_files.Max().ToString();
            string fileName = files.Where(f => f.Contains(lastMonth)).First();

            if (fileName == null)
                stopMode = true;
            
            GetDataTableFromExcel(fileName);

            //Load blocklist
            var file_blocklist = FileHelper.DeSerializeObject<List<long>>(Config.BlocklistFilePath);
            if (file_blocklist == null)
                FileHelper.SerializeObject<List<long>>(new List<long>(), Config.BlocklistFilePath);
            else
                BlockList = file_blocklist;

            //Load Users
            var file_users = FileHelper.DeSerializeObject<List<User>>(Config.UsersFilePath);
            if (file_users == null)
                FileHelper.SerializeObject<List<User>>(new List<User>(), Config.UsersFilePath);
            else
                Users = file_users;

            var me = Bot.GetMeAsync().Result;
            Console.Title = me.Username;

            Bot.OnMessage += BotOnMessageReceived;
            Bot.OnCallbackQuery += BotOnCallbackQueryReceived;

            Bot.StartReceiving(Array.Empty<UpdateType>());
            Bot.SendTextMessageAsync(Config.AdminId, $"WsizBusBot is started\nBot version `{Config.BotVersion}.`", ParseMode.Markdown);
            Console.WriteLine($"Start listening for @{me.Username}");

            while (true) { }
            Console.ReadLine();

            Bot.StopReceiving();
        }

        private static async void BotOnMessageReceived(object sender, MessageEventArgs messageEventArgs)
        {
            var message = messageEventArgs.Message;

            if (message == null || message.Type != MessageType.Text) return;
            if (message.Date.AddMinutes(1) < DateTime.UtcNow) return;

            await Bot.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);

            //Authorize User
            var access = Authorize(message.From.Id);

            Console.WriteLine($"User {message.From.FirstName} {message.From.LastName}");

            //Store Users
            if (!Users.Select(u => u.Id).ToList().Contains(message.From.Id))
            {
                //If new User then add
                Users.Add(new User
                {
                    Id = message.From.Id,
                    Name = message.From.FirstName + " " + message.From.LastName,
                    UserName = message.From.Username
                });

                //Save to file
                FileHelper.SerializeObject<List<User>>(Users, Config.UsersFilePath);

                Console.WriteLine($"New User {message.From.FirstName} {message.From.LastName}");
                await Bot.SendTextMessageAsync(Config.AdminId, $"New User {message.From.FirstName} {message.From.LastName}");
            }

            if (message.Text[0] == '/')
                switch (message.Text.Split(' ').First())
                {
                    case "/start":
                        {
                            string messageText =
                                "Привіт, Я знаю де і коли буде всізобус, щоб дізнатися - обери куди ти хочеш доїхати\n\n" +

                               // $"*Используй бота на свой страх и риск, если он неправильно показывает расписание то виноват только ТЫ" +
                                $"Bot version `{Config.BotVersion}`";

                            var inlineKeyboard = new InlineKeyboardMarkup(new[]
                            {
                                new [] // first row
                                {
                                    InlineKeyboardButton.WithCallbackData("До CTIR", "CTIR"),
                                    InlineKeyboardButton.WithCallbackData("До Rzeszówа", "RZESZOW"),
                                }
                            });

                            await Bot.SendTextMessageAsync(message.Chat.Id, messageText, ParseMode.Markdown, replyMarkup: inlineKeyboard);

                            break;
                        }
                    case "/me":
                        {
                            var userid = message.From.Id;
                            var chatId = message.Chat.Id;

                            string info_data = $"Your id is {userid}, chatId is {chatId}.";
                            await Bot.SendTextMessageAsync(message.Chat.Id, info_data);
                            break;
                        }
                    case "/users":
                        {
                            //Authorize
                            if (access != Acceess.Admin)
                            {
                                await Bot.DeleteMessageAsync(message.Chat.Id, message.MessageId);
                                return;
                            }

                            int i = 0;
                            string users = Users.Count()>0 ? "Users list:\n" : "Users list is empty";
                            foreach (var user in Users)
                            {
                                i++;
                                users += $"{i}  {user.Name} `{user.Id}` @{(user.UserName != null ? user.UserName.Replace("_","\\_") : "hidden")}\n";
                            }
                            await Bot.SendTextMessageAsync(message.Chat.Id, users, ParseMode.Markdown);
                            break;
                        }
                    case "/ban_list":
                        {
                            //Authorize
                            if (access != Acceess.Admin)
                            {
                                await Bot.DeleteMessageAsync(message.Chat.Id, message.MessageId);
                                return;
                            }

                            string users = BlockList.Count() == 0? "Ban list is empty":"";
                            foreach (var user_id in BlockList)
                            {
                                users += $"`{user_id}`\n";
                            }
                            await Bot.SendTextMessageAsync(message.Chat.Id, users, ParseMode.Markdown);
                            break;
                        }
                    case "/add_ban":
                        {
                            //Authorize
                            if (access != Acceess.Admin)
                            {
                                await Bot.DeleteMessageAsync(message.Chat.Id, message.MessageId);
                                return;
                            }

                            //get userId
                            var msgs = message.Text.Split(' ');
                            if (msgs.Count() > 1)
                            {
                                try
                                {
                                    var usrId = msgs[1];
                                    var id = Convert.ToInt64(usrId);
                                    if (id > 10000)
                                    {
                                        if (!BlockList.Contains(id))
                                            BlockList.Add(id);
                                    }
                                    else
                                    {
                                        try
                                        {
                                            id = Users[(int)id].Id;
                                            if (!BlockList.Contains(id))
                                                BlockList.Add(id);
                                        }
                                        catch
                                        {

                                        }
                                    }

                                    //save to file
                                    FileHelper.SerializeObject<List<long>>(BlockList, Config.BlocklistFilePath);
                                    await Bot.SendTextMessageAsync(message.Chat.Id, "User added to blocklist", ParseMode.Markdown);
                                }
                                catch
                                {

                                }
                            }
                            break;
                        }
                }
        }
        private static async void BotOnCallbackQueryReceived(object sender, CallbackQueryEventArgs callbackQueryEventArgs)
        {
            var callbackQuery = callbackQueryEventArgs.CallbackQuery;

            var commands = callbackQuery.Data.Split('-');

            if (commands.Count() == 1)
                switch (callbackQuery.Data)
                {
                    case "CTIR":
                        {
                            try
                            {
                                //send today
                                var today = schedule.Days.Where(d => d.DayDateTime.Day == 6).FirstOrDefault();
                                var filtered = today.Stations.Where(s => s.Destination == Way.ToCTIR).ToList();

                                var calendarKeyboard = new List<List<InlineKeyboardButton>>();
                                calendarKeyboard.Add(new List<InlineKeyboardButton>
                                {
                                     InlineKeyboardButton.WithCallbackData("Сьогодні", "CTIR-today"),
                                     InlineKeyboardButton.WithCallbackData("Завтра", "CTIR-tomorrow")
                                });

                                for (int i = 0; i < schedule.Days.Count; i++)
                                {
                                    if (i == 0 || i % 4 == 0)
                                        calendarKeyboard.Add(new List<InlineKeyboardButton>());

                                    calendarKeyboard.Last().Add(InlineKeyboardButton.WithCallbackData($"{schedule.Days[i].DayDateTime.Day}", $"CTIR-date-{schedule.Days[i].DayDateTime.Day}"));
                                }


                                var inlineKeyboard = new InlineKeyboardMarkup(calendarKeyboard);
                                await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, $"Обери дату ({Enum.GetName(typeof(MonthNamesUa), schedule.Days[0].DayDateTime.Month)})", ParseMode.Markdown, replyMarkup: inlineKeyboard);
                            }
                            catch (Exception ex)
                            {
                                if (callbackQuery.Message.From.Id == Config.AdminId)
                                    await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, ex.Message.ToString(), ParseMode.Markdown);
                                else
                                {
                                    await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, ex.Message.ToString(), ParseMode.Markdown);
                                    await Bot.SendTextMessageAsync(Config.AdminId, callbackQuery.Message.Text);
                                    await Bot.SendTextMessageAsync(Config.AdminId, ex.ToString());
                                }
                            }
                            try
                            {
                                await Bot.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);
                            }
                            catch
                            {

                            }
                            break;
                        }
                    case "RZESZOW":
                        {
                            try
                            {
                                //send today
                                var today = schedule.Days.Where(d => d.DayDateTime.Day == 6).FirstOrDefault();
                                var filtered = today.Stations.Where(s => s.Destination == Way.ToCTIR).ToList();

                                var calendarKeyboard = new List<List<InlineKeyboardButton>>();
                                calendarKeyboard.Add(new List<InlineKeyboardButton>
                                {
                                     InlineKeyboardButton.WithCallbackData("Сьогодні", "RZESZOW-today"),
                                     InlineKeyboardButton.WithCallbackData("Завтра", "RZESZOW-tomorrow")
                                });

                                for (int i = 0; i < schedule.Days.Count; i++)
                                {
                                    if (i == 0 || i % 4 == 0)
                                        calendarKeyboard.Add(new List<InlineKeyboardButton>());

                                    calendarKeyboard.Last().Add(InlineKeyboardButton.WithCallbackData($"{schedule.Days[i].DayDateTime.Day}", $"RZESZOW-date-{schedule.Days[i].DayDateTime.Day}"));
                                }


                                var inlineKeyboard = new InlineKeyboardMarkup(calendarKeyboard);
                                await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, $"Обери дату ({Enum.GetName(typeof(MonthNamesUa), schedule.Days[0].DayDateTime.Month)})", ParseMode.Markdown, replyMarkup: inlineKeyboard);
                            }
                            catch (Exception ex)
                            {
                                if (callbackQuery.Message.From.Id == Config.AdminId)
                                    await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, ex.Message.ToString(), ParseMode.Markdown);
                                else
                                {
                                    await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, ex.Message.ToString(), ParseMode.Markdown);
                                    await Bot.SendTextMessageAsync(Config.AdminId, callbackQuery.Message.Text);
                                    await Bot.SendTextMessageAsync(Config.AdminId, ex.ToString());
                                }
                            }
                            try
                            {
                                await Bot.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);
                            }
                            catch
                            { }
                            break;
                        }
                }
            else if (commands.Count() > 1)
            {
                try
                {
                    Way direction = commands[0] == "CTIR" ? Way.ToCTIR : Way.ToRzeszow;
                    int dayNumber = 0;

                    //Parse date
                    switch (commands[1])
                    {
                        case "date":
                            {
                                dayNumber = Convert.ToInt32(commands[2]);
                                break;
                            }
                        case "today":
                            {
                                dayNumber = DateTime.UtcNow.Day;
                                break;
                            }
                        case "tomorrow":
                            {
                                if (DateTime.UtcNow.AddDays(1).Month != DateTime.UtcNow.Month)
                                {
                                    await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Шо ти робиш, завтра інший місяць, іди спати О.о", ParseMode.Markdown);
                                    return;
                                }

                                dayNumber = DateTime.UtcNow.AddDays(1).Day;
                                break;
                            }
                    }

                    var text = GenerateSchedule(dayNumber, direction);
                    await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, text, ParseMode.Markdown);

                    //Send menu to get back
                    var inlineKeyboard = new InlineKeyboardMarkup(new[]
                    {
                        new [] // first row
                        {
                            InlineKeyboardButton.WithCallbackData("До CTIR", "CTIR"),
                            InlineKeyboardButton.WithCallbackData("До Rzeszówа", "RZESZOW")
                        }
                    });

                    await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Запрашами ще", ParseMode.Markdown, replyMarkup: inlineKeyboard);
                }
                catch (Exception ex)
                {
                    if (callbackQuery.Message.From.Id == Config.AdminId)
                        await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, ex.Message.ToString(), ParseMode.Markdown);
                    else
                    {
                        await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Сорі, щось пішло не так (*варунек*)", ParseMode.Markdown);
                        await Bot.SendTextMessageAsync(Config.AdminId, callbackQuery.Message.Text);
                        await Bot.SendTextMessageAsync(Config.AdminId, ex.ToString());
                    }
                }

                try
                {
                    await Bot.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);
                }
                catch { }
            }
        }
        private static void GetDataTableFromExcel(string path, bool hasHeader = true)
        {
            var start = DateTime.Now;

            using (var pck = new OfficeOpenXml.ExcelPackage())
            {
                using (var stream = File.OpenRead(path))
                {
                    pck.Load(stream);
                }
                var ws = pck.Workbook.Worksheets.First();

                //Read headers
                foreach (var cell in ws.Cells[2, 1, 2, 16])
                {
                    if (cell.Text == "" || cell.Text == " ") continue;

                    schedule.StationNames.Add(cell.Text + '/' + cell.Address);
                }

                //Read data
                for (int rowNum = 3; rowNum <= ws.Dimension.End.Row; rowNum++)
                {
                    var wsRow = ws.Cells[rowNum, 1, rowNum, ws.Dimension.End.Column];
                    foreach (var cell in wsRow)
                    {
                        //filter address
                        var letter = (int)cell.Address[0];
                        if (letter > 65 + 9)
                            continue;

                        //is it new day (with date)?
                        if (cell.Address[0] == 'A' && cell.Text != "")
                        {
                            var _isDate = isDate(cell.Text);
                            if (_isDate != null)
                            {
                                schedule.CreateDay(_isDate.Value);
                                Console.WriteLine($"Day {cell.Text} added");
                            }
                        }
                        else
                        {
                            schedule.AddStation(cell.Text, cell.Address);
                        }
                    }
                }
            }
            Console.WriteLine($"Parsed in {DateTime.Now - start}ms");

        }
        private static DateTime? isDate(string str)
        {
            try
            {
                if (str.Contains(":"))
                    return null;

                return DateTime.Parse(str);
            }
            catch
            {
                return null;
            }

            return null;
        }
        public static Acceess Authorize(long userId)
        {
            if (BlockList.Contains(userId)) return Acceess.Ban;
            if (userId == Config.AdminId) return Acceess.Admin;
            return Acceess.User;
        }
        public static string GenerateSchedule(int dayNumber, Way direction)
        {
            string retString = "Сьогодні бус не їде, іди спати О.о";

            string directionName = direction == Way.ToCTIR ? "Кельнарової" : "Жешова";

            var day = schedule.Days.Where(d => d.DayDateTime.Day == dayNumber).FirstOrDefault();
            if (day == null)
                return retString;

            var filtered = day.Stations.Where(s => s.Destination == direction && s.Time != null).ToList();
            if (filtered.Count() == 0)
                return retString;

            var grouped = filtered.GroupBy(x => x.RouteId).ToList();

            var monthName = Enum.GetName(typeof(MonthNamesUa), schedule.Days[0].DayDateTime.Month);

            string harmonogram = $"*{dayNumber} {monthName}* розклад бусiв\n*до {directionName}*\n\n";

            var firstRoute = grouped[0].Where(g => g.Time != null).ToList();


            if (firstRoute.Count > 3 && direction == Way.ToCTIR)
            {
                harmonogram += $"Перший бус їде:\n" +
                $"Of. Katynia - `{firstRoute[0].Time?.ToString("HH:mm")}`\n" +
                $"Cieplińskiego - `{firstRoute[1].Time?.ToString("HH:mm")}`\n" +
                $"Powst. W-wy - `{firstRoute[2].Time?.ToString("HH:mm")}`\n" +
                $"Tyczyn - `{firstRoute[3].Time?.ToString("HH:mm")}`\n" +
                $"CTIR - `{firstRoute[4].Time?.ToString("HH:mm")}`\n\n";
                
                grouped.RemoveRange(0, 1);

                if (grouped.Count > 0)
                    harmonogram += $"Потім як звикле:\n";
                else
                    harmonogram = harmonogram.Replace("Перший бус", "Бус");
            }
            if (grouped.Count > 0)
            {
                harmonogram += direction == Way.ToCTIR ? "Tesco \t  Tyczyn   \tCTIR\n" : "CTIR    \t  Tyczyn \t  Tesco\n";
                foreach (var route in grouped)
                {
                    foreach (var station in route)
                    {
                        harmonogram += ("`" + station.Time?.ToString("HH:mm") + "` \t  ");
                    }
                    harmonogram += "\n";
                }
            }

            return harmonogram;
        }
    }
    public class User
    {
        public string Name { get; set; }
        public string UserName { get; set; }
        public long Id { get; set; }
    }
    public enum Acceess
    {
        Admin,
        User,
        Ban
    }
}

