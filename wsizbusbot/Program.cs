using OfficeOpenXml;
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

namespace wsizbusbot
{
    class Program
    {
        private static readonly TelegramBotClient Bot = new TelegramBotClient(Config.TelegramAccessToken);

        public static List<User> Users = new List<User>();
        public static List<long> BlockList = new List<long>();
        public static List<Stats> Stats = new List<Stats>();
        public static Schedule schedule = new Schedule();
        public static bool stopMode = false;
        static void Main(string[] args)
        {
            var tryCreateDirectory = Directory.CreateDirectory(Config.DataPath);

            stopMode = !ParseFiles().Result;

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

            //Load Stats
            var file_stats = FileHelper.DeSerializeObject<List<Stats>>(Config.StatsFilePath);
            if (file_stats == null)
                FileHelper.SerializeObject<List<Stats>>(new List<Stats>(), Config.StatsFilePath);
            else
                Stats = file_stats;

            var me = Bot.GetMeAsync().Result;
            Console.Title = me.Username;

            Bot.OnMessage += BotOnMessageReceived;
            Bot.OnCallbackQuery += BotOnCallbackQueryReceived;

            Bot.StartReceiving(Array.Empty<UpdateType>());
            Bot.SendTextMessageAsync(Config.AdminId, $"WsizBusBot is started\nBot version `{Config.BotVersion}.`", ParseMode.Markdown);
            if(stopMode)
                Bot.SendTextMessageAsync(Config.AdminId, "Error with file parsing", ParseMode.Markdown);

            Console.WriteLine($"Start listening for @{me.Username}");

            while (true) { }
            Console.ReadLine();

            Bot.StopReceiving();
        }
        public static async Task<bool> ParseFiles()
        {
            try
            {
                var files = Directory.GetFiles(Config.DataPath, "*.xlsx");
                if (files.Count() == 0)
                    stopMode = true;

                var filtered_files = files.Select(x => Convert.ToInt32(Path.GetFileNameWithoutExtension(x).Remove(6))).ToList();
                foreach(var filename in files)
                {
                    GetDataTableFromExcel(filename);
                    Console.WriteLine($"{filename} parsed");
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
        
        public static async Task<bool> TrySendMessage(long chatId, string messageText, ParseMode parseMode)
        {
            try
            {
                await Bot.SendTextMessageAsync(chatId, messageText, parseMode);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return false;
            }
            
        }
        public static async Task<bool> TryDeleteMessage(long chatId, int messageId)
        {
            try
            {
                await Bot.DeleteMessageAsync(chatId, messageId);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return false;
            }
        }
        private static async void BotOnMessageReceived(object sender, MessageEventArgs messageEventArgs)
        {
            var message = messageEventArgs.Message;

            if (message == null || (message.Type != MessageType.Text && message.Type != MessageType.Document)) return;
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

            //Store Stats
            {
                if (Stats.Where(s => s.Date == DateTime.UtcNow.Date).FirstOrDefault() == null)
                {
                    //Create new day
                    Stats.Add(new Stats
                    {
                        ActiveClicks = 1,
                        Date = DateTime.UtcNow.Date
                    });
                }
                else
                {
                    Stats.Where(s => s.Date == DateTime.UtcNow.Date).First().ActiveClicks++;
                }
                //Save to file
                FileHelper.SerializeObject<List<Stats>>(Stats, Config.StatsFilePath);
            }

            //Download file
            if (message.Type == MessageType.Document)
            {
                try
                {
                    var test = Bot.GetFileAsync(message.Document.FileId);
                    var download_url = $"https://api.telegram.org/file/bot{Config.TelegramAccessToken}/" + test.Result.FilePath;
                    using (WebClient client = new WebClient())
                    {
                        client.DownloadFile(new Uri(download_url), $"{Config.DataPath}{message.Document.FileName}");
                    }
                    var res = await TrySendMessage(Config.AdminId, "Success, reboot required", ParseMode.Default);
                }
                catch (Exception ex)
                {
                    var res = await TrySendMessage(Config.AdminId, ex.ToString(), ParseMode.Default);
                }
                return;
            }

            if (message.Text[0] == '/')
                switch (message.Text.Split(' ').First())
                {
                    case "/help":
                        {
                            //Authorize
                            if (access != Acceess.Admin)
                            {
                                await Bot.DeleteMessageAsync(message.Chat.Id, message.MessageId);
                                return;
                            }

                            string help_string = $"*Admin functions*:\n" +
                                $"/me - print your `id`\n" +
                                $"/help - help\n" +
                                $"/stats - bot stats\n" +
                                $"/users - top 7 days users list\n" +
                                $"/users_all - all users list\n" +
                                $"/ban\\_list - banned users list\n" +
                                $"/add\\_ban [user id or user id] - banned users list\n" +
                                $"/send\\_all [message text] - send text to all users\n" +
                                $"/send\\_test [message text] - send text to you\n";

                            //get userId
                            await Bot.SendTextMessageAsync(message.Chat.Id, help_string, ParseMode.Markdown);
                            break;
                        }
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
                    case "/stats":
                        {
                            //Authorize
                            if (access != Acceess.Admin)
                            {
                                await Bot.DeleteMessageAsync(message.Chat.Id, message.MessageId);
                                return;
                            }

                            string stats = Users.Count() > 0 ? "*Users* list:\n" : "*Users list is empty";
                            var topUsers = Users.Where(u => u.ActiveAt > DateTime.UtcNow.AddDays(-7)).ToList();
                            var grouped = topUsers.GroupBy(u => u.ActiveAt.Date).Select(x => new { Value = x.Count(), Date = x.Key }).ToList();

                            foreach (var key in grouped)
                            {
                                stats += $"{key.Date.ToShortDateString()} - `{key.Value}`\n";
                            }
                            stats += "\n" + (Stats.Count() > 0 ? "*Activity Days* list:\n" : "*Activity Days list is empty");
                            foreach (var stat in Stats)
                            {
                                stats += $"{stat.Date.ToShortDateString()} - `{stat.ActiveClicks}`\n";
                            }

                            await Bot.SendTextMessageAsync(message.Chat.Id, stats, ParseMode.Markdown);

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

                            string users = Users.Count() > 0 ? "Users list:\n" : "Users list is empty";

                            string stats = Users.Count() > 0 ? "*Users* list:\n" : "*Users list is empty";
                            var topUsers = Users.Where(u => u.ActiveAt > DateTime.UtcNow.AddDays(-7)).OrderByDescending(u => u.ActiveAt).ToList();

                            for (int i = 0; i < topUsers.Count(); i++)
                            {
                                var user = topUsers[i];
                                users += $"{user.Name} `{user.Id}` @{(user.UserName != null ? user.UserName.Replace("_", "\\_") : "hidden")}  {user.ActiveAt.ToShortDateString()}\n";
                                if (i == 49)
                                {
                                    await Bot.SendTextMessageAsync(message.Chat.Id, users, ParseMode.Markdown);
                                    users = topUsers.Count() > 0 ? "Users list:\n" : "Users list is empty";
                                }
                            }
                            if (users != (topUsers.Count() > 0 ? "Users list:\n" : "Users list is empty"))
                                await Bot.SendTextMessageAsync(message.Chat.Id, users, ParseMode.Markdown);

                            break;
                        }
                    case "/users_all":
                        {
                            //Authorize
                            if (access != Acceess.Admin)
                            {
                                await Bot.DeleteMessageAsync(message.Chat.Id, message.MessageId);
                                return;
                            }

                            string users = Users.Count() > 0 ? "Users list:\n" : "Users list is empty";

                            for (int i = 0; i < Users.Count(); i++)
                            {
                                var user = Users[i];
                                users += $"{i}  {user.Name} `{user.Id}` @{(user.UserName != null ? user.UserName.Replace("_", "\\_") : "hidden")}  {user.ActiveAt.ToShortDateString()}\n";
                                if (i == 49)
                                {
                                    await Bot.SendTextMessageAsync(message.Chat.Id, users, ParseMode.Markdown);
                                    users = Users.Count() > 0 ? "Users list:\n" : "Users list is empty";
                                }
                            }
                            if (users != (Users.Count() > 0 ? "Users list:\n" : "Users list is empty"))
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
                    case "/send_all":
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
                                    var text = message.Text.Replace("/send_all ","");

                                    foreach (var user in Users)
                                        await TrySendMessage(user.Id, text, ParseMode.Markdown);
                                    
                                    await Bot.SendTextMessageAsync(message.Chat.Id, "Success", ParseMode.Markdown);
                                }
                                catch
                                {
                                    await Bot.SendTextMessageAsync(message.Chat.Id, "error", ParseMode.Markdown);
                                }
                            }
                            break;
                        }
                    case "/send_test":
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
                                    var text = message.Text.Replace("/send_test ", "");
                                    await TrySendMessage(message.Chat.Id, text, ParseMode.Markdown);
                                }
                                catch
                                {
                                    await Bot.SendTextMessageAsync(message.Chat.Id, "error", ParseMode.Markdown);
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
            
            var usr = Users.Where(u => u.Id == callbackQuery.From.Id).FirstOrDefault();
            if (usr != null)
            {
                //If new User then add
                usr.ActiveAt = DateTime.UtcNow;

                //Save to file
                FileHelper.SerializeObject<List<User>>(Users, Config.UsersFilePath);
            }

            //Store Stats
            {
                if (Stats.Where(s => s.Date == DateTime.UtcNow.Date).FirstOrDefault() == null)
                {
                    //Create new day
                    Stats.Add(new Stats
                    {
                        ActiveClicks = 1,
                        Date = DateTime.UtcNow.Date
                    });
                }
                else
                {
                    Stats.Where(s => s.Date == DateTime.UtcNow.Date).First().ActiveClicks++;
                }
                //Save to file
                FileHelper.SerializeObject<List<Stats>>(Stats, Config.StatsFilePath);
            }

            if (commands.Count() == 1)
            {
                try
                {
                    Way direction = commands[0] == "CTIR" ? Way.ToCTIR : Way.ToRzeszow;
                    var directionString = commands[0];

                    var monthName = Enum.GetName(typeof(MonthNamesUa), DateTime.UtcNow.Month - 1);
                    var actualSchedule = schedule.Days.Where(d => d.DayDateTime.Month == DateTime.UtcNow.Month).ToList();

                    var calendarKeyboard = new List<List<InlineKeyboardButton>>();

                    //today-tomorrow
                    if (actualSchedule.Count > 0)
                    {
                        calendarKeyboard.Add(new List<InlineKeyboardButton>
                                {
                                     InlineKeyboardButton.WithCallbackData("Інший місяць", $"{directionString}-MONTHS")
                                });
                        calendarKeyboard.Add(new List<InlineKeyboardButton>
                         {
                            InlineKeyboardButton.WithCallbackData("Сьогодні", $"{directionString}-today"),
                            InlineKeyboardButton.WithCallbackData("Завтра", $"{directionString}-tomorrow")
                         });
                    }
                    else
                    {
                        var months = schedule.avaliableMonths;
                        foreach (var month in months)
                        {
                            calendarKeyboard.Add(new List<InlineKeyboardButton>() { InlineKeyboardButton.WithCallbackData($"{month}", $"{directionString}-month-{(int)month + 1}") });
                        }
                    }

                    for (int i = 0; i < actualSchedule.Count; i++)
                    {
                        if (i == 0 || i % 4 == 0)
                            calendarKeyboard.Add(new List<InlineKeyboardButton>());

                        calendarKeyboard.Last().Add(InlineKeyboardButton.WithCallbackData($"{actualSchedule[i].DayDateTime.Day}", $"{directionString}-date-{actualSchedule[i].DayDateTime.ToShortDateString()}"));
                    }

                    var inlineKeyboard = new InlineKeyboardMarkup(calendarKeyboard);
                    var keyboardText = (actualSchedule.Count > 0) ? $"Обери дату ({Enum.GetName(typeof(MonthNamesUa), DateTime.UtcNow.Month - 1)})" : $"Нема розкладу на цей місяць ({Enum.GetName(typeof(MonthNamesUa), DateTime.UtcNow.Month - 1)}), обери інший.";
                    await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, keyboardText, ParseMode.Markdown, replyMarkup: inlineKeyboard);

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
            }
            else if (commands.Count() > 1)
            {
                try
                {
                    Way direction = commands[0] == "CTIR" ? Way.ToCTIR : Way.ToRzeszow;
                    var directionString = commands[0];

                    DateTime selectedDay = new DateTime();

                    //Parse date
                    switch (commands[1])
                    {
                        case "MONTHS":
                            {
                                try
                                {
                                    var months = schedule.avaliableMonths;
                                    var monthsKeyboard = new List<List<InlineKeyboardButton>>();

                                    foreach (var month in months)
                                    {
                                        monthsKeyboard.Add(new List<InlineKeyboardButton>() { InlineKeyboardButton.WithCallbackData($"{month}", $"{directionString}-month-{(int)month + 1}") });
                                    }

                                    var monthsInlineKeyboard = new InlineKeyboardMarkup(monthsKeyboard);
                                    await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, $"Обери місяць:", ParseMode.Markdown, replyMarkup: monthsInlineKeyboard);
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
                                await TryDeleteMessage(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);
                                return;
                            }
                        case "month":
                            {
                                try
                                {
                                    var monthNumber = Convert.ToInt32(commands[2]);
                                    var monthName = Enum.GetName(typeof(MonthNamesUa), monthNumber - 1);
                                    var monthSchedule = schedule.Days.Where(d => d.DayDateTime.Month == monthNumber).ToList();

                                    var calendarKeyboard = new List<List<InlineKeyboardButton>>();
                                    calendarKeyboard.Add(new List<InlineKeyboardButton>
                                    {
                                     InlineKeyboardButton.WithCallbackData("Інший місяць", $"{directionString}-MONTHS")
                                    });

                                    for (int i = 0; i < monthSchedule.Count; i++)
                                    {
                                        if (i == 0 || i % 4 == 0)
                                            calendarKeyboard.Add(new List<InlineKeyboardButton>());

                                        calendarKeyboard.Last().Add(InlineKeyboardButton.WithCallbackData($"{monthSchedule[i].DayDateTime.Day}", $"{directionString}-date-{monthSchedule[i].DayDateTime.ToShortDateString()}"));
                                    }

                                    var calendarInlineKeyboard = new InlineKeyboardMarkup(calendarKeyboard);
                                    await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, $"Обери дату ({monthName})", ParseMode.Markdown, replyMarkup: calendarInlineKeyboard);
                                    await TryDeleteMessage(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId); 
                                    return;
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
                                await TryDeleteMessage(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);
                                return;
                            }
                        case "date":
                            {
                                selectedDay = DateTime.Parse(commands[2]);
                                break;
                            }
                        case "today":
                            {
                                selectedDay = DateTime.Today;
                                break;
                            }
                        case "tomorrow":
                            {
                                selectedDay = DateTime.Today.AddDays(1);

                                if (DateTime.UtcNow.AddDays(1).Month != DateTime.UtcNow.Month)
                                {
                                    await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Шо ти робиш, завтра інший місяць, іди спати О.о", ParseMode.Markdown);
                                    return;
                                }

                                break;
                            }
                    }

                    var text = GenerateSchedule(selectedDay, direction);
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

                await TryDeleteMessage(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);
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
        }
        public static Acceess Authorize(long userId)
        {
            if (BlockList.Contains(userId)) return Acceess.Ban;
            if (userId == Config.AdminId) return Acceess.Admin;
            return Acceess.User;
        }
        public static string GenerateSchedule(DateTime date, Way direction)
        {
            string retString = "Сьогодні бус не їде, іди спати О.о";

            string directionName = direction == Way.ToCTIR ? "Кельнарової" : "Жешова";

            var day = schedule.Days.Where(d => d.DayDateTime.Day == date.Day && d.DayDateTime.Month == date.Month).FirstOrDefault();
            if (day == null)
                return retString;

            var filtered = day.Stations.Where(s => s.Destination == direction && s.Time != null).ToList();
            if (filtered.Count() == 0)
                return retString;

            var grouped = filtered.GroupBy(x => x.RouteId).ToList();

            var monthName = Enum.GetName(typeof(MonthNamesUa), date.Month-1);

            string harmonogram = $"*{Local.DaysOfWeekNamesUa[(int)date.DayOfWeek]} {date.Day} {monthName}* розклад бусiв\n*до {directionName}*\n\n";

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
        public DateTime ActiveAt { get; set; }
        public long Id { get; set; }
    }
    public class Stats
    {
        public DateTime Date { get; set; }
        public long ActiveClicks { get; set; }
    }
    public enum Acceess
    {
        Admin,
        User,
        Ban
    }
}