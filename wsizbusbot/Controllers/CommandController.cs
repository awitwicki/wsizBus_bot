using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;
using wsizbusbot.Models;

namespace wsizbusbot.Controllers
{
    public class CommandController: BaseController
    {
        public void Start(MessageEventArgs messageEventArgs)
        {
            var message = messageEventArgs.Message;

            string messageText = Local.StartString[0] + $"Bot version `{ApplicationData.BotVersion}`";

            var inlineKeyboard = TemplateModelsBuilder.BuildStartMenuMarkup();

            CoreBot.SendMessage(message.Chat.Id, messageText, ParseMode.Markdown, replyMarkup: inlineKeyboard);
        }
        
        public void Me(MessageEventArgs messageEventArgs)
        {
            var message = messageEventArgs.Message;

            var userid = message.From.Id;
            var chatId = message.Chat.Id;

            string info_data = $"Your id is {userid}, chatId is {chatId}.";

            var acc = (message.From.Id == Config.AdminId);
            if (acc)
            {
                ApplicationData.Users.Set().First(u => u.Id == userid).Access = Acceess.Admin;
                info_data += "\nYou are Admin";
            }

            CoreBot.SendMessage(message.Chat.Id, info_data);
        }

        [Role(Acceess.Admin)]
        public void Stats(MessageEventArgs messageEventArgs)
        {
            var message = messageEventArgs.Message;
            var user = ApplicationData.GetUser(message.From.Id);

            string stats = $"*Monthly Users:* `{ ApplicationData.Users.Set().Count(u => u.ActiveAt > DateTime.UtcNow.AddDays(-30))}`\n";
            stats += $"*Weekly Users:* `{ApplicationData.Users.Set().Count(u => u.ActiveAt > DateTime.UtcNow.AddDays(-7))}`\n\n";

            stats += (ApplicationData.Users.Set().Count() > 0 ? "*Activity Days* list:\n" : "*Activity Days* list is empty");
            foreach (var stat in ApplicationData.Stats.Set().OrderByDescending(s => s.Date).Where(s => s.Date > DateTime.UtcNow.AddDays(-7)))
            {
                stats += $"{stat.Date.ToString("dd/MM/yy")} - `{stat.UserId.Count}` - `{stat.ActiveClicks}`\n";
            }

            stats += "\n*Languages:*\n";
            stats += $"🇬🇧 - {ApplicationData.Users.Set().Count(u => u.Language == LocalLanguage.English)}";
            stats += $" 🇺🇦 - {ApplicationData.Users.Set().Count(u => u.Language == LocalLanguage.Ukrainian)}";
            stats += $" 🇮🇩 - {ApplicationData.Users.Set().Count(u => u.Language == LocalLanguage.Polish)}";

            var inlineKeyboard = TemplateModelsBuilder.StatsMarkup();

            CoreBot.SendMessage(message.Chat.Id, stats, ParseMode.Markdown, replyMarkup: inlineKeyboard);
        }
        
        [Role(Acceess.Admin)]
        public void Users(MessageEventArgs messageEventArgs)
        {
            var message = messageEventArgs.Message;

            var users = TemplateModelsBuilder.GetTopUsers();
            var inlineKeyboard = TemplateModelsBuilder.UsersStatsMarkup();

            CoreBot.SendMessage(message.Chat.Id, users, ParseMode.Markdown, replyMarkup: inlineKeyboard);
        }
        
        [Role(Acceess.Admin)]
        public  void Users_all(MessageEventArgs messageEventArgs)
        {
            var message = messageEventArgs.Message;

            string users = ApplicationData.Users.Set().Count > 0 ? "Users list:\n" : "Users list is empty";

            for (int i = 0; i < ApplicationData.Users.Set().Count; i++)
            {
                var tempUser = ApplicationData.Users.Set()[i];
                users += $"{i} {Local.LangIcon[tempUser.GetLanguage]} {tempUser.Name} `{tempUser.Id}` @{(tempUser.UserName != null ? tempUser.UserName.Replace("_", "\\_") : "hidden")}  {tempUser.ActiveAt.ToShortDateString()}\n";
                if (i % 50 == 0 && i > 0)
                {
                    CoreBot.SendMessage(message.Chat.Id, users, ParseMode.Markdown);
                    users = ApplicationData.Users.Set().Count > 0 ? "Users list:\n" : "Users list is empty";
                }
            }
            if (users != (ApplicationData.Users.Set().Count > 0 ? "Users list:\n" : "Users list is empty"))
                CoreBot.SendMessage(message.Chat.Id, users, ParseMode.Markdown);
        }
        
        [Role(Acceess.Admin)]
        public  void Ban_list(MessageEventArgs messageEventArgs)
        {
            var message = messageEventArgs.Message;
            var user = ApplicationData.GetUser(message.From.Id);

            string users = ApplicationData.Users.Set().Count(u => u.Access == Acceess.Ban) == 0 ? "Ban list is empty" : "";
            foreach (var user_id in ApplicationData.Users.Set().Where(u => u.Access == Acceess.Ban).ToList())
            {
                users += $"`{user_id.Id}` {user_id.Name.Replace("_", "\\_")} @{(user_id.UserName != null ? user_id.UserName.Replace("_", "\\_") : "hidden")}\n";
            }
            CoreBot.SendMessage(message.Chat.Id, users, ParseMode.Markdown);
        }
        
        [Role(Acceess.Admin)]
        public void Ban(MessageEventArgs messageEventArgs)
        {
            var message = messageEventArgs.Message;
            var user = ApplicationData.GetUser(message.From.Id);

            //Get usersIds
            var args = ArgParser.ParseCommand(message.Text);
            var users_ids = (List<string>)args.GetValueOrDefault("args");

            if (users_ids.Count > 0)
            {
                foreach (var usrId in users_ids)
                {
                    try
                    {
                        var id = Convert.ToInt64(usrId);

                        if (ApplicationData.Users.Set().Select(u => u.Id).Contains((long)id))
                        {
                            var usr_ = ApplicationData.Users.Set().First(u => u.Id == id);
                            usr_.Access = usr_.Access == Acceess.User ? Acceess.Ban : Acceess.User;

                            //Save to file
                            ApplicationData.SaveUsers();
                            CoreBot.SendMessage(message.Chat.Id, $"User {(usr_.UserName != null ? "@" + usr_.UserName.Replace("_", "\\_") : usr_.Id.ToString())} access is changed to `{usr_.Access}`", ParseMode.Markdown);
                        }
                        else
                        {
                            CoreBot.SendMessage(message.Chat.Id, $"There is no users with id `{usrId}`", ParseMode.Markdown);
                        }
                    }
                    catch (Exception ex)
                    {
                        CoreBot.SendMessage(message.Chat.Id, $"Error: {ex}", ParseMode.Markdown);
                    }
                }
            }
            else
            {
                CoreBot.SendMessage(message.Chat.Id, $"There is no users id's", ParseMode.Markdown);
            }
        }
        
        [Role(Acceess.Admin)]
        public void Send_all(MessageEventArgs messageEventArgs)
        {
            var message = messageEventArgs.Message;

            var msgs = message.Text.Split(' ');
            if (msgs.Count() > 1)
            {
                try
                {
                    var text = message.Text.Replace("/send_all ", "");

                    foreach (var tempUser in ApplicationData.Users.Set())
                    {
                        Console.WriteLine($"{tempUser.Id} {tempUser.Name} {tempUser.UserName}");
                        CoreBot.SendMessage(tempUser.Id, text, ParseMode.Markdown);
                        Task.Delay(200);
                    }

                    CoreBot.SendMessage(message.Chat.Id, "Success", ParseMode.Markdown);
                }
                catch
                {
                    CoreBot.SendMessage(message.Chat.Id, "Error", ParseMode.Markdown);
                }
            }
        }
        
        [Role(Acceess.Admin)]
        public void Send_test(MessageEventArgs messageEventArgs)
        {
            var message = messageEventArgs.Message;
            var user = ApplicationData.GetUser(message.From.Id);

            var msgs = message.Text.Split(' ');
            if (msgs.Count() > 1)
            {
                try
                {
                    var text = message.Text.Replace("/send_test ", "");
                    CoreBot.SendMessage(user.Id, text, ParseMode.Markdown);
                }
                catch
                {
                    CoreBot.SendMessage(message.Chat.Id, "Error", ParseMode.Markdown);
                }
            }
        }
        
        [Role(Acceess.Admin)]
        public async void Get_data(MessageEventArgs messageEventArgs)
        {
            var message = messageEventArgs.Message;

            using (FileStream fs = System.IO.File.OpenRead(Config.UsersFilePath))
            {
                InputOnlineFile inputOnlineFile = new InputOnlineFile(fs, Config.UsersFilePath);
                await CoreBot.Bot.SendDocumentAsync(message.Chat, inputOnlineFile);
            }

            using (FileStream fs = System.IO.File.OpenRead(Config.StatsFilePath))
            {
                InputOnlineFile inputOnlineFile = new InputOnlineFile(fs, Config.StatsFilePath);
                await CoreBot.Bot.SendDocumentAsync(message.Chat, inputOnlineFile);
            }
        }

        [Role(Acceess.Admin)]
        public void Help(MessageEventArgs messageEventArgs)
        {
            var message = messageEventArgs.Message;

            string help_string = $"*Admin functions*:\n" +
                $"/me - print your `id`\n" +
                $"/help - help\n" +
                $"/stats - bot stats\n" +
                $"/users - top 7 days users list\n" +
                $"/users\\_all - all users list\n" +
                $"/ban\\_list - banned users list\n" +
                $"/ban [user id] - bann\\unban user by id\n" +
                $"/send\\_all [message text] - send text to all users\n" +
                $"/send\\_test [message text] - send text to you\n" +
                $"/get\\_data - send data files to you";

            CoreBot.SendMessage(message.Chat.Id, help_string, ParseMode.Markdown);
        }
    }
}

