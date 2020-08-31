﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
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
    public class CoreBot
    {
        public TelegramBotClient Bot;

        public CoreBot(string accessToken)
        {
            Directory.CreateDirectory(Config.DataPath);

            Bot = new TelegramBotClient(Config.TelegramAccessToken);

            var me = Bot.GetMeAsync().Result;
            Console.Title = me.Username;

            Bot.OnMessage += BotOnMessageReceived;
            Bot.OnCallbackQuery += BotOnCallbackQueryReceived;
            Bot.OnReceiveError += BotOnReceiveError;


            Bot.StartReceiving(Array.Empty<UpdateType>());
            Console.WriteLine($"Start listening for @{me.Username}");

            SendMessageAsync(Config.AdminId, $"WsizBusBot is started\nBot version `{ApplicationData.BotVersion}.`", ParseMode.Markdown);
        }

        ~CoreBot() => Bot.StopReceiving();

        //Save Bot methods

        public async void SendMessageAsync(ChatId chatId, string text, ParseMode parseMode = ParseMode.Default, bool disableWebPagePreview = false, bool disableNotification = false, int replyToMessageId = 0, IReplyMarkup replyMarkup = null, CancellationToken cancellationToken = default)
        {
            try
            {
                //Markdown cant parse single _ symbol so we need change it  to \\_
                if (parseMode == ParseMode.Markdown || parseMode == ParseMode.MarkdownV2)
                    text = text.Replace("_", "\\_");
                await Bot.SendTextMessageAsync(chatId, text, parseMode, disableWebPagePreview, disableNotification, replyToMessageId, replyMarkup, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
        
        public async void DeleteMessageAsync(ChatId chatId, int messageId, CancellationToken cancellationToken = default)
        {
            try
            {
                await Bot.DeleteMessageAsync(chatId, messageId, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        //Handlers
        private async void BotOnMessageReceived(object sender, MessageEventArgs messageEventArgs)
        {
            var message = messageEventArgs.Message;

            await Bot.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);

            //Handle stats, access, filters
            if (!CheckMessage(messageEventArgs))
                return;

            if (message.Text != null && message.Text[0] == '/') //handle command
                Invoker(messageEventArgs);
            /*else
                HandleMessage(messageEventArgs); //handle simple message */
        }
        private async void BotOnCallbackQueryReceived(object sender, CallbackQueryEventArgs callbackQueryEventArgs)
        {
            var callbackQuery = callbackQueryEventArgs.CallbackQuery;

            //Handle stats, access, filters
            if (!CheckMessage(callbackQueryEventArgs: callbackQueryEventArgs))
                return;

            await Bot.AnswerCallbackQueryAsync(callbackQuery.Id);

            Invoker(callbackQueryEventArgs: callbackQueryEventArgs);

            //Debug inline commands
            //string methodName = ArgParser.ParseCallbackData(callbackQuery.Data).GetValueOrDefault(Commands.MethodName);
            //Console.WriteLine($"{methodName} - {callbackQuery.Data}");
        }

        private async void Invoker(MessageEventArgs messageEventArgs = null, CallbackQueryEventArgs callbackQueryEventArgs = null)
        {
            //Get message data
            var chatId = messageEventArgs != null ? messageEventArgs.Message.Chat.Id : callbackQueryEventArgs.CallbackQuery.Message.Chat.Id;
            var messageId = messageEventArgs != null ? messageEventArgs.Message.MessageId : callbackQueryEventArgs.CallbackQuery.Message.MessageId;
            var sender = messageEventArgs != null ? messageEventArgs.Message.From : callbackQueryEventArgs.CallbackQuery.From;

            var user = ApplicationData.GetUser(sender.Id);

            //Get controller type
            Type controllerType = messageEventArgs != null ? typeof(CommandController) : typeof(CallbackController);

            //Get method name from message
            string methodName = "";
            if (messageEventArgs != null)
                methodName = (string)ArgParser.ParseCommand(messageEventArgs.Message.Text).GetValueOrDefault(Commands.MethodName);
            else
                methodName = (string)ArgParser.ParseCallbackData(callbackQueryEventArgs.CallbackQuery.Data).GetValueOrDefault(Commands.MethodName);

            //Find method in controller
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
                        await Bot.SendChatActionAsync(chatId, chatAction.Value);

                    //Cast controller object
                    var controller = Activator.CreateInstance(controllerType);

                    //Set params
                    ((BaseController)controller).Bot = Bot;
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
                    SendMessageAsync(Config.AdminId, ex.ToString());
                    SendMessageAsync(Config.AdminId, callbackQueryEventArgs?.CallbackQuery.Data ?? "F");
                }
            }
            else
            {
                //Cant method did not exists
                SendMessageAsync(Config.AdminId, callbackQueryEventArgs != null ? $"Cant find method for: {callbackQueryEventArgs.CallbackQuery.Data}" : $"Cant find method for: {messageEventArgs.Message.Text}");
                SendMessageAsync(chatId, $"Command `{methodName}` not exists", ParseMode.Markdown);
            }
        }
        private bool CheckMessage(MessageEventArgs messageEventArgs = null, CallbackQueryEventArgs callbackQueryEventArgs = null)
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
                    return false;
                }
            }

            //Authorize User (if user banned - ignore)
            if (user.UserAccess == UserAccess.Ban)
            {
                //SendMessage(chatId, "Banned");
                DeleteMessageAsync(chatId, messageId);
                return false;
            }

            //Store Stats
            {
                ApplicationData.UpdateStats(sender);
            }

            return true;
        }
        
        /*private async void HandleMessage(MessageEventArgs messageEventArgs)
        {
            //Get message data
            var chatId = messageEventArgs.Message.Chat.Id;
            var messageId = messageEventArgs.Message.MessageId;
            var sender = messageEventArgs.Message.From;

            var user = ApplicationData.GetUser(sender.Id);

            //Create controller
            ConversationController controller = new ConversationController() { ChatId = chatId, MessageId = messageId, User = user };

            MethodInfo method = typeof(ConversationController).GetMethod("Start");

            //Get and send chatAction from attributes
            var chatAction = BaseController.GetChatActionAttributes(method);
            if (chatAction.HasValue)
                await Bot.SendChatActionAsync(chatId, chatAction.Value);

            controller.Start(messageEventArgs);
        }*/
        
        private void BotOnReceiveError(object sender, ReceiveErrorEventArgs receiveErrorEventArgs)
        {
            //ToDo logging to file
            Console.WriteLine("Received error: {0} — {1}", receiveErrorEventArgs.ApiRequestException.ErrorCode, receiveErrorEventArgs.ApiRequestException.Message);
            SendMessageAsync(Config.AdminId, $"Error {receiveErrorEventArgs.ApiRequestException.ErrorCode} : {receiveErrorEventArgs.ApiRequestException.Message}");
        }
    }
}
