using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using wsizbusbot.Controllers;
using wsizbusbot.Models;

namespace wsizbusbot
{
    public static class CoreBot
    {
        public static TelegramBotClient Bot;

        public static void StartReceiving()
        {
            Bot = new TelegramBotClient(Config.TelegramAccessToken);

            var me = Bot.GetMeAsync().Result;
            Console.Title = me.Username;

            Bot.OnMessage += BotOnMessageReceived;
            //Bot.OnMessageEdited += BotOnMessageReceived;
            Bot.OnCallbackQuery += BotOnCallbackQueryReceived;
            //Bot.OnInlineResultChosen += BotOnChosenInlineResultReceived;
            Bot.OnReceiveError += BotOnReceiveError;

            Bot.StartReceiving(Array.Empty<UpdateType>());
            Console.WriteLine($"Start listening for @{me.Username}");
        }
        public static void SendLocation(ChatId chatId, float latitude, float longitude, int livePeriod = 0, bool disableNotification = false, int replyToMessageId = 0, IReplyMarkup replyMarkup = null, CancellationToken cancellationToken = default)
        {
            try
            {
                Bot.SendLocationAsync(chatId, latitude, longitude).Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
        public static void SendMessage(ChatId chatId, string text, ParseMode parseMode = ParseMode.Default, bool disableWebPagePreview = false, bool disableNotification = false, int replyToMessageId = 0, IReplyMarkup replyMarkup = null, CancellationToken cancellationToken = default)
        {
            try
            {
                Bot.SendTextMessageAsync(chatId, text, parseMode, disableWebPagePreview, disableNotification, replyToMessageId, replyMarkup, cancellationToken).Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
        public static void EditMessageReplyMarkup(ChatId chatId, int messageId, InlineKeyboardMarkup replyMarkup = null, CancellationToken cancellationToken = default)
        {
            try
            {
                Bot.EditMessageReplyMarkupAsync(chatId, messageId, replyMarkup, cancellationToken).Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
        public static void EditMessageText(ChatId chatId, int messageId, string text, ParseMode parseMode = ParseMode.Default, bool disableWebPagePreview = false, InlineKeyboardMarkup replyMarkup = null, CancellationToken cancellationToken = default)
        {
            try
            {
                Bot.EditMessageTextAsync(chatId, messageId, text, parseMode, disableWebPagePreview,replyMarkup, cancellationToken).Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
        public static void DeleteMessage(ChatId chatId, int messageId, CancellationToken cancellationToken = default)
         {
            try
            {
                Bot.DeleteMessageAsync(chatId, messageId, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        //Handlers
        private static void BotOnMessageReceived(object sender, MessageEventArgs messageEventArgs)
        {
            var message = messageEventArgs.Message;

            //Handle stats, access, filters
            if (!CheckMessage(messageEventArgs))
                return;

            if (message.Text != null && message.Text[0] == '/') //handle command
                Invoker(messageEventArgs);
            /*else
                HandleMessage(messageEventArgs); //handle simple message */
        }
        private static async void BotOnCallbackQueryReceived(object sender, CallbackQueryEventArgs callbackQueryEventArgs)
        {
            var callbackQuery = callbackQueryEventArgs.CallbackQuery;

            //Handle stats, access, filters
            if (!CheckMessage(callbackQueryEventArgs : callbackQueryEventArgs))
                return;

            await Bot.AnswerCallbackQueryAsync(callbackQuery.Id);

            Invoker(callbackQueryEventArgs: callbackQueryEventArgs);
            
            //Debug inline commands
            string methodName = ArgParser.ParseCallbackData(callbackQuery.Data).GetValueOrDefault(Commands.MethodName);
            Console.WriteLine($"{methodName} - {callbackQuery.Data}");
        }

        private static async void Invoker(MessageEventArgs messageEventArgs = null, CallbackQueryEventArgs callbackQueryEventArgs = null)
        {
            var chatId = messageEventArgs != null ? messageEventArgs.Message.Chat.Id : callbackQueryEventArgs.CallbackQuery.Message.Chat.Id;
            var messageId = messageEventArgs != null ? messageEventArgs.Message.MessageId : callbackQueryEventArgs.CallbackQuery.Message.MessageId;
            var sender = messageEventArgs != null ? messageEventArgs.Message.From : callbackQueryEventArgs.CallbackQuery.From;

            var user = ApplicationData.GetUser(sender.Id);
            Type controllerType = messageEventArgs != null ? typeof(CommandController) : typeof(CallbackController);

            string methodName = "";
            if (messageEventArgs != null)
                methodName =  (string)ArgParser.ParseCommand(messageEventArgs.Message.Text).GetValueOrDefault(Commands.MethodName);
            else
                methodName = (string)ArgParser.ParseCallbackData(callbackQueryEventArgs.CallbackQuery.Data).GetValueOrDefault(Commands.MethodName);


            MethodInfo method = controllerType.GetMethod(methodName);
            if (method != null)
            {
                //Check user access by role
                if (!BaseController.ValidateAccess(method, user))
                    return;
                try
                {
                    //Get and send chatAction from attributes
                    var chatAction = BaseController.GetChatActionAttributes(method);
                    if (chatAction.HasValue)
                        await CoreBot.Bot.SendChatActionAsync(chatId, chatAction.Value);

                    //Cast controller object
                    var controller = Activator.CreateInstance(controllerType);

                    //Set params
                    ((BaseController)controller).ChatId = chatId;
                    ((BaseController)controller).MessageId = messageId;
                    ((BaseController)controller).User = user;

                    //Invoke method
                    if (messageEventArgs != null)
                        method.Invoke(controller, parameters: new object[] { messageEventArgs });
                    else
                        method.Invoke(controller, parameters: new object[] { callbackQueryEventArgs });
                }
                catch (Exception ex)
                {
                    SendMessage(chatId, Local.ErrorMessage[user.GetLanguage], ParseMode.Markdown, replyMarkup: TemplateModelsBuilder.BuildStartMenuMarkup());
                    SendMessage(Config.AdminId, ex.ToString());
                    SendMessage(Config.AdminId, callbackQueryEventArgs?.CallbackQuery.Data ?? "F");
                }
            }
            else
            {
                //Method did not exists
                SendMessage(Config.AdminId, callbackQueryEventArgs != null? $"Cant find method for: {callbackQueryEventArgs.CallbackQuery.Data}" : $"Cant find method for: {messageEventArgs.Message.Text}");
                SendMessage(chatId, Local.ErrorMessage[user.GetLanguage], ParseMode.Markdown, replyMarkup: TemplateModelsBuilder.BuildStartMenuMarkup());
            }
        }
        private static bool CheckMessage(MessageEventArgs messageEventArgs = null, CallbackQueryEventArgs callbackQueryEventArgs = null)
        {
            if (messageEventArgs == null && callbackQueryEventArgs == null)
                return false;

            var chatId = messageEventArgs != null ? messageEventArgs.Message.Chat.Id : callbackQueryEventArgs.CallbackQuery.Message.Chat.Id;
            var messageId = messageEventArgs != null ? messageEventArgs.Message.MessageId : callbackQueryEventArgs.CallbackQuery.Message.MessageId;
            var sender = messageEventArgs != null ? messageEventArgs.Message.From : callbackQueryEventArgs.CallbackQuery.From;

            var user = ApplicationData.GetUser(sender.Id);

            //Store Users
            if (user == null)
            {
                user = new CoreBotUser
                {
                    Id = sender.Id,
                    Name = sender.FirstName + " " + sender.LastName,
                    UserName = sender.Username,
                    ActiveAt = DateTime.UtcNow,
                    UserAccess = (sender.Id == Config.AdminId) ? UserAccess.Admin : UserAccess.User
                };
            }

            //If new User then add
            ApplicationData.AddOrUpdateUser(user);

            //Filters for messages
            if (messageEventArgs != null)
            {
                var message = messageEventArgs.Message;
                if (message == null || (message.Type != MessageType.Text && message.Type != MessageType.Document)) return false;

                //Ignore old messages
                if (message.Date.AddMinutes(1) < DateTime.UtcNow)
                {
                    SendMessage(chatId, Local.Offline[user.GetLanguage]);
                    return false;
                }
            }

            //Authorize User
            if (user.UserAccess == UserAccess.Ban)
            {
                SendMessage(chatId, Local.Permaban, ParseMode.Markdown);
                DeleteMessage(chatId, messageId);
                return false;
            }

            //Store Stats
            {
                ApplicationData.UpdateStats(sender);
            }

            return true;
        }
        private static void BotOnReceiveError(object sender, ReceiveErrorEventArgs receiveErrorEventArgs)
        {
            Console.WriteLine("Received error: {0} — {1}",
                receiveErrorEventArgs.ApiRequestException.ErrorCode,
                receiveErrorEventArgs.ApiRequestException.Message
            );
            SendMessage(Config.AdminId, $"Error {receiveErrorEventArgs.ApiRequestException.ErrorCode} : {receiveErrorEventArgs.ApiRequestException.Message}");
        }
    }
}
