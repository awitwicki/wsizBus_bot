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
using System.ComponentModel.DataAnnotations;

namespace wsizbusbot
{
    class Program
    {
        public static string BotVersion = "1.9 011219";
        private static readonly TelegramBotClient Bot = new TelegramBotClient(Config.TelegramAccessToken);

        public static List<User> Users = new List<User>();
        public static List<Stats> Stats = new List<Stats>();
        public static Schedule schedule = new Schedule();
        public static bool stopMode = false;
        static void Main(string[] args)
        {
            var tryCreateDirectory = Directory.CreateDirectory(Config.DataPath);

            stopMode = !ParseFiles().Result;

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
            Bot.SendTextMessageAsync(Config.AdminId, $"WsizBusBot is started\nBot version `{BotVersion}.`", ParseMode.Markdown);
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

                foreach(var filename in files)
                {
                    GetDataTableFromExcel(filename);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
        public static async Task<Telegram.Bot.Types.Message> TrySendMessage(long chatId, string messageText, ParseMode parseMode)
        {
            try
            {
                var res = await Bot.SendTextMessageAsync(chatId, messageText, parseMode);
                return res;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
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
            var usr = Users.Where(u => u.Id == message.From.Id).FirstOrDefault();

            //Store Users
            if (usr == null)
            {
                usr = new User
                {
                    Id = message.From.Id,
                    Name = message.From.FirstName + " " + message.From.LastName,
                    UserName = message.From.Username,
                    ActiveAt = DateTime.UtcNow,
                    Access = (message.From.Id == Config.AdminId) ? Acceess.Admin : Acceess.User
                };

                //If new User then add
                Users.Add(usr);

                //Save to file
                FileHelper.SerializeObject<List<User>>(Users, Config.UsersFilePath);

                Console.WriteLine($"New User {message.From.FirstName} {message.From.LastName}");
                await Bot.SendTextMessageAsync(Config.AdminId, $"New User {message.From.FirstName} {message.From.LastName}");
            }
            else
            {
                //Update User
                usr.ActiveAt = DateTime.UtcNow;

                //Save to file
                FileHelper.SerializeObject<List<User>>(Users, Config.UsersFilePath);
            }

            if (message == null || (message.Type != MessageType.Text && message.Type != MessageType.Document)) return;
            if (message.Date.AddMinutes(1) < DateTime.UtcNow) return;

            await Bot.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);

            //Authorize User
            var access = usr.Access;
            if (access == Acceess.Ban)
            {
                try
                {
                    await Bot.SendTextMessageAsync(message.Chat.Id, Local.Permaban, ParseMode.Markdown);
                    await Bot.DeleteMessageAsync(message.Chat.Id, message.MessageId);
                }
                catch { }
                return;
            }

            //Store Stats
            {
                if (Stats.Where(s => s.Date == DateTime.UtcNow.Date).FirstOrDefault() == null)
                {
                    //Create new day
                    Stats.Add(new Stats
                    {
                        ActiveClicks = 1,
                        Date = DateTime.UtcNow.Date,
                        UserId = new List<long>() { usr.Id }
                    });
                }
                else
                {
                    Stats.Where(s => s.Date == DateTime.UtcNow.Date).First().ActiveClicks++;
                    if (Stats.Where(s => s.Date == DateTime.UtcNow.Date).First().UserId.FirstOrDefault(u => u == usr.Id) == null)
                    {
                        Stats.Where(s => s.Date == DateTime.UtcNow.Date).First().UserId.Add(usr.Id);
                    };
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
                    var res = await TrySendMessage(Config.AdminId, "Downloaded", ParseMode.Default);

                    GetDataTableFromExcel(Config.DataPath + message.Document.FileName);

                    if(res != null)
                       await Bot.EditMessageTextAsync(res.Chat.Id, res.MessageId, res.Text + "\nParsed", ParseMode.Default);
                }
                catch (Exception ex)
                {
                    await TrySendMessage(Config.AdminId, ex.ToString(), ParseMode.Default);
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
                                $"/users\\_all - all users list\n" +
                                $"/ban\\_list - banned users list\n" +
                                $"/ban [user id] - bann\\unban user by id\n" +
                                $"/send\\_all [message text] - send text to all users\n" +
                                $"/send\\_test [message text] - send text to you\n";

                            //get userId
                            await Bot.SendTextMessageAsync(message.Chat.Id, help_string, ParseMode.Markdown);
                            break;
                        }
                    case "/start":
                        {
                            string messageText = Local.StartString[usr.GetLanguage] + $"Bot version `{BotVersion}`";

                            var inlineKeyboard = new InlineKeyboardMarkup(new[]
                            {
                                new [] // first row
                                {
                                    InlineKeyboardButton.WithCallbackData(Local.GetWayDisplayName(Way.ToCTIR), "CTIR"),
                                    InlineKeyboardButton.WithCallbackData(Local.GetWayDisplayName(Way.ToRzeszow), "RZESZOW"),
                                },
                                 new [] // first row
                                {
                                    InlineKeyboardButton.WithCallbackData("🇮🇩", "pl"),
                                    InlineKeyboardButton.WithCallbackData("🇺🇦", "ua"),
                                    InlineKeyboardButton.WithCallbackData("🇬🇧", "en"),
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

                            string stats = $"*Monthly Users:* `{Users.Count(u => u.ActiveAt > DateTime.UtcNow.AddDays(-30))}`\n";
                            stats += $"*Weekly Users:* `{Users.Count(u => u.ActiveAt > DateTime.UtcNow.AddDays(-7))}`\n\n";
                         
                            stats += (Stats.Count() > 0 ? "*Activity Days* list:\n" : "*Activity Days* list is empty");
                            foreach (var stat in Stats.OrderByDescending(s => s.Date).Where(s => s.Date > DateTime.UtcNow.AddDays(-7)))
                            {
                                stats += $"{stat.Date.ToString("dd/MM/yy")} - `{stat.UserId.Count}` - `{stat.ActiveClicks}`\n";
                            }

                            stats += "\n*Languages:*\n";
                            stats += $"🇬🇧 - {Users.Count(u => u.Language == LocalLanguage.English)}";     
                            stats += $" 🇺🇦 - {Users.Count(u => u.Language == LocalLanguage.Ukrainian)}";
                            stats += $" 🇮🇩 - {Users.Count(u => u.Language == LocalLanguage.Polish)}";

                            var inlineKeyboard = new InlineKeyboardMarkup(new[]
                            {
                                new [] // first row
                                {
                                    InlineKeyboardButton.WithCallbackData("Refresh", "refresh"),
                                },
                            });

                            await Bot.SendTextMessageAsync(message.Chat.Id, stats, ParseMode.Markdown, replyMarkup: inlineKeyboard);

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

                            var users = GenerateTopUsers();

                            var inlineKeyboard = new InlineKeyboardMarkup(new[]
                            {
                                new [] // first row
                                {
                                    InlineKeyboardButton.WithCallbackData("Refresh", "refresh_users"),
                                },
                            });

                            await Bot.SendTextMessageAsync(message.Chat.Id, users, ParseMode.Markdown, replyMarkup: inlineKeyboard);
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
                                users += $"{i} {Local.LangIcon[(int)user.Language]} {user.Name} `{user.Id}` @{(user.UserName != null ? user.UserName.Replace("_", "\\_") : "hidden")}  {user.ActiveAt.ToShortDateString()}\n";
                                if (i % 50 == 0 && i > 0)
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

                            string users = Users.Count(u => u.Access == Acceess.Ban) == 0? "Ban list is empty":"";
                            foreach (var user_id in Users.Where(u => u.Access == Acceess.Ban).ToList())
                            {
                                users += $"`{user_id.Id}` {user_id.Name.Replace("_", "\\_")} @{(user_id.UserName != null ? user_id.UserName.Replace("_", "\\_") : "hidden")}\n";
                            }
                            await Bot.SendTextMessageAsync(message.Chat.Id, users, ParseMode.Markdown);
                            break;
                        }
                    case "/ban":
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

                                    if (Users.Select(u => u.Id).Contains((long)id))
                                    {
                                        var usr_ = Users.First(u => u.Id == id);
                                        usr_.Access = usr_.Access == Acceess.User ? Acceess.Ban : Acceess.User;
                                    }
                                    
                                    //save to file
                                    FileHelper.SerializeObject<List<User>>(Users, Config.UsersFilePath);
                                    await Bot.SendTextMessageAsync(message.Chat.Id, $"Users access is changed", ParseMode.Markdown);
                                }
                                catch
                                {
                                    await Bot.SendTextMessageAsync(message.Chat.Id, $"There is no users with id `{msgs[1]}`", ParseMode.Markdown);
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
                                    var text = message.Text.Replace("/send_all ", "");

                                    foreach (var user in Users)
                                    {
                                        Console.WriteLine($"-{user.Id} {user.Name} {user.UserName}");
                                        await TrySendMessage(user.Id, text, ParseMode.Markdown);
                                        await Task.Delay(200);
                                    }

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
                    case "/clean":
                        {
                            Users = Users.Where(u => u.ActiveAt > DateTime.UtcNow.AddYears(-1)).ToList();

                            //Save to file
                            FileHelper.SerializeObject<List<User>>(Users, Config.UsersFilePath);

                            await Bot.SendTextMessageAsync(message.Chat.Id, "ok");
                            break;
                        }
                }
        }
        private static async void BotOnCallbackQueryReceived(object sender, CallbackQueryEventArgs callbackQueryEventArgs)
        {
            var callbackQuery = callbackQueryEventArgs.CallbackQuery;
            var commands = callbackQuery.Data.Split('-');
            
            var usr = Users.Where(u => u.Id == callbackQuery.From.Id).FirstOrDefault();

            //Authorize User
            var access = usr.Access;
            if (access == Acceess.Ban)
            {
                try
                {
                    await Bot.EditMessageTextAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, Local.Permaban);
                }
                catch { }
                return;
            }

            //Store Stats
            if (callbackQuery.Message.From.Id != Config.AdminId)
            {
                var stat = Stats.Where(s => s.Date == DateTime.UtcNow.Date).FirstOrDefault();
                if (stat == null)
                {
                    //Create new day
                    Stats.Add(new Stats
                    {
                        ActiveClicks = 1,
                        Date = DateTime.UtcNow.Date,
                        UserId = new List<long>() { usr.Id }
                    });
                }
                else
                {
                    stat.ActiveClicks++;
                    if (stat.UserId.Count(u => u == usr.Id) == 0)
                    {
                        stat.UserId.Add(usr.Id);
                    };
                }
                //Save to file
                FileHelper.SerializeObject<List<Stats>>(Stats, Config.StatsFilePath);
            }

            //Update Users
            {
                usr.ActiveAt = DateTime.UtcNow;

                //Save to file
                FileHelper.SerializeObject<List<User>>(Users, Config.UsersFilePath);
            }

            //Change language
            if (callbackQuery.Data.Contains("ua") || callbackQuery.Data.Contains("pl") || callbackQuery.Data.Contains("en"))
            {
                var lan = callbackQuery.Data;
                LocalLanguage newLanguage = LocalLanguage.English;
                if (lan == "ua")
                    newLanguage = LocalLanguage.Ukrainian;
                else if (lan == "pl")
                    newLanguage = LocalLanguage.Polish;
                else if (lan == "en")
                    newLanguage = LocalLanguage.English;

                if (newLanguage == usr.Language)
                    return;

                string messageText = Local.StartString[(int)newLanguage] + $"Bot version `{BotVersion}`";

                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new [] // first row
                    {
                        InlineKeyboardButton.WithCallbackData(Local.GetWayDisplayName(Way.ToCTIR), "CTIR"),
                        InlineKeyboardButton.WithCallbackData(Local.GetWayDisplayName(Way.ToRzeszow), "RZESZOW")
                    },
                    new [] // first row
                    {
                        InlineKeyboardButton.WithCallbackData("🇮🇩", "pl"),
                        InlineKeyboardButton.WithCallbackData("🇺🇦", "ua"),
                        InlineKeyboardButton.WithCallbackData("🇬🇧", "en"),
                    }
                });

                await Bot.EditMessageTextAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, messageText, ParseMode.Markdown, replyMarkup: inlineKeyboard);

                //Store User Lang
                if (usr != null)
                {
                    //If new User then add
                    usr.Language = newLanguage;

                    //Save to file
                    FileHelper.SerializeObject<List<User>>(Users, Config.UsersFilePath);
                }

                return;
            }

            //Refresh stats
            if (callbackQuery.Data == "refresh")
            {
                string stats = $"*Monthly Users:* `{Users.Count(u => u.ActiveAt > DateTime.UtcNow.AddDays(-30))}`\n";
                stats += $"*Weekly Users:* `{Users.Count(u => u.ActiveAt > DateTime.UtcNow.AddDays(-7))}`\n\n";

                stats += (Stats.Count() > 0 ? "*Activity Days* list:\n" : "*Activity Days* list is empty");
                foreach (var stat in Stats.OrderByDescending(s => s.Date).Where(s => s.Date > DateTime.UtcNow.AddDays(-7)))
                {
                    stats += $"{stat.Date.ToString("dd/MM/yy")} - `{stat.UserId.Count}` - `{stat.ActiveClicks}`\n";
                }

                stats += "\n*Languages:*\n";
                stats += $"🇬🇧 - {Users.Count(u => u.Language == LocalLanguage.English)}";
                stats += $" 🇺🇦 - {Users.Count(u => u.Language == LocalLanguage.Ukrainian)}";
                stats += $" 🇮🇩 - {Users.Count(u => u.Language == LocalLanguage.Polish)}";

                stats += $"\n\n`{DateTime.UtcNow}`";

                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new [] // first row
                    {
                        InlineKeyboardButton.WithCallbackData("Refresh", "refresh"),
                    },
                });

                try
                {
                    await Bot.EditMessageTextAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, stats, ParseMode.Markdown, replyMarkup: inlineKeyboard);
                }
                catch { }

                return;
            }

            //Refresh users
            if (callbackQuery.Data == "refresh_users")
            {
                var users = GenerateTopUsers();
                users += $"\n`{DateTime.UtcNow}`";

                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new [] // first row
                    {
                        InlineKeyboardButton.WithCallbackData("Refresh", "refresh_users"),
                    },
                });

                try
                {
                    await Bot.EditMessageTextAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, users, ParseMode.Markdown, replyMarkup: inlineKeyboard);
                }
                catch { }

                return;
            }

            //Start menu
            if (callbackQuery.Data == "start")
            {
                var replyMessageText = Local.StartString[usr.GetLanguage] + $"Bot version `{BotVersion}`";
                var replyInlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                                new [] // first row
                                {
                                    InlineKeyboardButton.WithCallbackData(Local.GetWayDisplayName(Way.ToCTIR), "CTIR"),
                                    InlineKeyboardButton.WithCallbackData(Local.GetWayDisplayName(Way.ToRzeszow), "RZESZOW"),
                                },
                                 new [] // first row
                                {
                                    InlineKeyboardButton.WithCallbackData("🇮🇩", "pl"),
                                    InlineKeyboardButton.WithCallbackData("🇺🇦", "ua"),
                                    InlineKeyboardButton.WithCallbackData("🇬🇧", "en"),
                                }
                            });

                await Bot.EditMessageTextAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, replyMessageText, ParseMode.Markdown, replyMarkup: replyInlineKeyboard);
                return;
            }

            //Bus stations
            if (callbackQuery.Data.Contains("busStations"))
            {
                switch (commands[1])
                {
                    case "allstations":
                        {
                            var locationsInline = new List<List<InlineKeyboardButton>>();
                            foreach (var station in Local.BusPointNames)
                            {
                                locationsInline.Add(new List<InlineKeyboardButton>() { InlineKeyboardButton.WithCallbackData($"{station.Value}", $"busStations-station-{(int)station.Key}")});
                            }
                            
                            var stationsInline = new InlineKeyboardMarkup(locationsInline);
                            await Bot.EditMessageTextAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, Local.PickBusStation[usr.GetLanguage], ParseMode.Markdown, replyMarkup: stationsInline);
                            return;
                        }
                    case "station":
                        {
                            try
                            {
                                var stationId = int.Parse(commands[2]);
                                var station = (Local.PointNames)stationId;
                                
                                await Bot.DeleteMessageAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);

                                var menuInline = new List<List<InlineKeyboardButton>>();
                                var inlineKeyboard2 = new InlineKeyboardMarkup(new[]
                                {
                                    new [] // first row
                                        {
                                            InlineKeyboardButton.WithCallbackData(Local.BusStations[usr.GetLanguage], "busStations-allstations"),
                                        },
                                        new [] // first row
                                        {
                                            InlineKeyboardButton.WithCallbackData(Local.Menu[usr.GetLanguage], "start"),
                                        },
                                         new [] // first row
                                    {
                                        InlineKeyboardButton.WithCallbackData(Local.GetWayDisplayName(Way.ToCTIR), "CTIR"),
                                        InlineKeyboardButton.WithCallbackData(Local.GetWayDisplayName(Way.ToRzeszow), "RZESZOW")
                                    }
                                });
                                await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, Local.BusPointNames[station], ParseMode.Markdown);
                                await Bot.SendLocationAsync(callbackQuery.Message.Chat.Id, Local.BusPoints[station][0], Local.BusPoints[station][1]);
                                await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, Local.ReturnBack[usr.GetLanguage], ParseMode.Markdown, replyMarkup: inlineKeyboard2);
                                return;
                            }
                            catch
                            {
                                await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, Local.ErrorMessage[usr.GetLanguage], ParseMode.Markdown);
                                return;
                            }
                        }
                }
             
                string messageText = $"Bot version `{BotVersion}`";

                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new [] // first row
                    {
                        InlineKeyboardButton.WithCallbackData(Local.GetWayDisplayName(Way.ToCTIR), "CTIR"),
                        InlineKeyboardButton.WithCallbackData(Local.GetWayDisplayName(Way.ToRzeszow), "RZESZOW")
                    },
                    new [] // first row
                    {
                        InlineKeyboardButton.WithCallbackData("🇮🇩", "pl"),
                        InlineKeyboardButton.WithCallbackData("🇺🇦", "ua"),
                        InlineKeyboardButton.WithCallbackData("🇬🇧", "en"),
                    }
                });

                await Bot.EditMessageTextAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId, messageText, ParseMode.Markdown, replyMarkup: inlineKeyboard);

                return;
            }

            if (commands.Count() == 1)
            {
                try
                {
                    Way direction = commands[0] == "CTIR" ? Way.ToCTIR : Way.ToRzeszow;
                    var directionString = commands[0];

                    var monthName = Local.GetMonthNames(usr.Language)[DateTime.UtcNow.Month - 1];
                    var actualSchedule = schedule.Days.Where(d => d.DayDateTime.Month == DateTime.UtcNow.Month).ToList();

                    var calendarKeyboard = new List<List<InlineKeyboardButton>>();

                    //today-tomorrow
                    if (actualSchedule.Count > 0)
                    {
                        calendarKeyboard.Add(new List<InlineKeyboardButton>
                    {
                         InlineKeyboardButton.WithCallbackData(Local.AnotherMonth[(int)usr.Language], $"{directionString}-MONTHS")
                    });
                        calendarKeyboard.Add(new List<InlineKeyboardButton>
                        {
                            InlineKeyboardButton.WithCallbackData(Local.Today[usr.GetLanguage], $"{directionString}-today"),
                            InlineKeyboardButton.WithCallbackData(Local.Tomorrow[usr.GetLanguage], $"{directionString}-tomorrow")
                        });
                    }
                    else
                    {
                        var months = schedule.avaliableMonths;
                        foreach (var month in months)
                        {
                            calendarKeyboard.Add(new List<InlineKeyboardButton>() { InlineKeyboardButton.WithCallbackData($"{Local.GetMonthNames(usr.Language)[month]}", $"{directionString}-month-{(int)month + 1}") });
                        }
                    }

                    for (int i = 0; i < actualSchedule.Count; i++)
                    {
                        if (i == 0 || i % 4 == 0)
                            calendarKeyboard.Add(new List<InlineKeyboardButton>());

                        calendarKeyboard.Last().Add(InlineKeyboardButton.WithCallbackData($"{actualSchedule[i].DayDateTime.Day}", $"{directionString}-date-{actualSchedule[i].DayDateTime.ToShortDateString()}"));
                    }

                    var inlineKeyboard = new InlineKeyboardMarkup(calendarKeyboard);
                    var keyboardText = (actualSchedule.Count > 0) ? $"{Local.PickDate[(int)usr.Language]} ({Local.GetMonthNames(usr.Language)[DateTime.UtcNow.Month - 1]})" : $"{Local.NoDataForMonth[usr.GetLanguage]} (*{Local.GetMonthNames(usr.Language)[DateTime.UtcNow.Month - 1]}*) {Local.PickAnother[usr.GetLanguage]}";
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
                                        monthsKeyboard.Add(new List<InlineKeyboardButton>() { InlineKeyboardButton.WithCallbackData($"{Local.GetMonthNames(usr.GetLanguage)[month]}", $"{directionString}-month-{month + 1}") });
                                    }

                                    var monthsInlineKeyboard = new InlineKeyboardMarkup(monthsKeyboard);
                                    await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, Local.PickMonth[usr.GetLanguage], ParseMode.Markdown, replyMarkup: monthsInlineKeyboard);
                                }
                                catch (Exception ex)
                                {
                                    if (callbackQuery.Message.From.Id == Config.AdminId)
                                        await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, ex.Message.ToString(), ParseMode.Markdown);
                                    else
                                    {
                                        await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, Local.ErrorMessage[usr.GetLanguage], ParseMode.Markdown);
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
                                    var monthName = Local.GetMonthNames(usr.Language)[monthNumber - 1];
                                    var monthSchedule = schedule.Days.Where(d => d.DayDateTime.Month == monthNumber).ToList();

                                    var calendarKeyboard = new List<List<InlineKeyboardButton>>();
                                    calendarKeyboard.Add(new List<InlineKeyboardButton>
                                    {
                                        InlineKeyboardButton.WithCallbackData(Local.AnotherMonth[(int)usr.Language], $"{directionString}-MONTHS")
                                    });

                                    for (int i = 0; i < monthSchedule.Count; i++)
                                    {
                                        if (i == 0 || i % 4 == 0)
                                            calendarKeyboard.Add(new List<InlineKeyboardButton>());

                                        calendarKeyboard.Last().Add(InlineKeyboardButton.WithCallbackData($"{monthSchedule[i].DayDateTime.Day}", $"{directionString}-date-{monthSchedule[i].DayDateTime.ToShortDateString()}"));
                                    }

                                    var calendarInlineKeyboard = new InlineKeyboardMarkup(calendarKeyboard);
                                    await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, $"{Local.PickDate[(int)usr.Language]}({monthName})", ParseMode.Markdown, replyMarkup: calendarInlineKeyboard);
                                    await TryDeleteMessage(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId);
                                    return;
                                }
                                catch (Exception ex)
                                {
                                    if (callbackQuery.Message.From.Id == Config.AdminId)
                                        await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, ex.Message.ToString(), ParseMode.Markdown);
                                    else
                                    {
                                        await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, Local.ErrorMessage[usr.GetLanguage], ParseMode.Markdown);
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

                    var text = GenerateSchedule(selectedDay, direction, usr.GetLanguage);
                    await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, text, ParseMode.Markdown);

                    //Send menu to get back
                    var inlineKeyboard = new InlineKeyboardMarkup(new[]
                    {
            new [] // first row
            {
                InlineKeyboardButton.WithCallbackData(Local.BusStations[usr.GetLanguage], "busStations-allstations"),
            },
            new [] // second row
            {
                InlineKeyboardButton.WithCallbackData(Local.GetWayDisplayName(Way.ToCTIR), "CTIR"),
                InlineKeyboardButton.WithCallbackData(Local.GetWayDisplayName(Way.ToRzeszow), "RZESZOW"),
            }
        });

                    await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, Local.YouAreWelcome[usr.GetLanguage], ParseMode.Markdown, replyMarkup: inlineKeyboard);
                }
                catch (Exception ex)
                {
                    if (callbackQuery.Message.From.Id == Config.AdminId)
                        await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, ex.Message.ToString(), ParseMode.Markdown);
                    else
                    {
                        await Bot.SendTextMessageAsync(callbackQuery.Message.Chat.Id, Local.ErrorMessage[usr.GetLanguage], ParseMode.Markdown);
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
                            }
                        }
                        else
                        {
                            schedule.AddStation(cell.Text, cell.Address);
                        }
                    }
                }
            }
            Console.WriteLine($"{path} is parsed in {DateTime.Now - start}ms");
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
        public static string GenerateSchedule(DateTime date, Way direction, int lang)
        {
            string retString = Local.ErrorMessage[lang];

            string directionName = direction == Way.ToCTIR ? Local.ToCtir[lang] : Local.ToRzeszow[lang];

            var day = schedule.Days.Where(d => d.DayDateTime.Day == date.Day && d.DayDateTime.Month == date.Month).FirstOrDefault();
            if (day == null)
                return retString;

            var filtered = day.Stations.Where(s => s.Destination == direction && s.Time != null).ToList();
            if (filtered.Count() == 0)
                return retString;

            var grouped = filtered.GroupBy(x => x.RouteId).ToList();

            var monthName = Local.GetMonthNames(lang)[date.Month -1];

            string harmonogram = $"*{Local.GetDaysOfWeekNames(lang)[(int)date.DayOfWeek]} {date.Day} {monthName}* {Local.BusSchedule[lang]} {directionName}*\n\n";

            var firstRoute = grouped[0].Where(g => g.Time != null).ToList();


            if (firstRoute.Count > 3 && direction == Way.ToCTIR)
            {
                harmonogram += $"{Local.FirstBus[lang]}\n" +
                $"Of. Katynia - `{firstRoute[0].Time?.ToString("HH:mm")}`\n" +
                $"Cieplińskiego - `{firstRoute[1].Time?.ToString("HH:mm")}`\n" +
                $"Powst. W-wy - `{firstRoute[2].Time?.ToString("HH:mm")}`\n" +
                $"Tyczyn - `{firstRoute[3].Time?.ToString("HH:mm")}`\n" +
                $"CTIR - `{firstRoute[4].Time?.ToString("HH:mm")}`\n\n";

                grouped.RemoveRange(0, 1);
                if (grouped.Count > 0)
                    harmonogram += Local.ThenLikeAlways[lang] + "\n";
            }
            if (grouped.Count > 0)
            {
                harmonogram += direction == Way.ToCTIR ? "Tesco \t  Tyczyn   \tCTIR\n" : "CTIR    \t  Tyczyn \t  Tesco\n";
                foreach (var route in grouped)
                {
                    foreach (var station in route)
                    {
                        harmonogram += ("`" + (station.Time?.Hour != 0 ? station.Time?.ToString("HH:mm") : " --  ") + "` \t  ");
                    }
                    harmonogram += "\n";
                }
            }
            harmonogram += "\n@wsizBus\\_bot";
            return harmonogram;
        }
        public static string GenerateTopUsers()
        {
            string users = Users.Count() > 0 ? "*Users list:*\n" : "Users list is empty";

            var topUsers = Users.Where(u => u.ActiveAt > DateTime.UtcNow.AddDays(-7)).OrderByDescending(u => u.ActiveAt).Take(50).ToList();

            foreach (var u in topUsers)
            {
                users += $"{Local.LangIcon[(int)u.Language]} {u.Name} `{u.Id}` @{(u.UserName != null ? u.UserName.Replace("_", "\\_") : "hidden")}  {u.ActiveAt.ToString("dd/MM/yy")}\n";
            }

            return users;
        }
    }
}