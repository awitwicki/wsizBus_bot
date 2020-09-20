using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Text;
using System.Threading;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace wsizbusbot.Controllers
{
    public class BaseController
    {
        public TelegramBotClient Bot { get; set; }
        public CoreBotUser User { get; set; }
        public int MessageId { get; set; }
        public long ChatId { get; set; }

        //Methods attributes
        public class MessageReaction : System.Attribute
        {
            public ChatAction ChatAction { get; set; }
            public MessageReaction(ChatAction chatAction)
            {
                this.ChatAction = chatAction;
            }
        }

        public class Role : System.Attribute
        {
            public UserAccess UserAccess { get; set; }
            public Role(UserAccess userAccess)
            {
                this.UserAccess = userAccess;
            }
        }


        //Attribute validators

        //Send chat action
        public static Nullable<ChatAction> GetChatActionAttributes(MethodInfo methodInfo)
        {
            Object[] attributes = methodInfo.GetCustomAttributes(true);

            //find and return chatAction
            foreach (var attribute in attributes)
            {
                if (attribute.GetType() == typeof(MessageReaction))
                {
                    var chatAction = ((MessageReaction)attribute).ChatAction;
                    return chatAction;
                }
            }

            return null;
        }

        //Validate user access role
        public static bool ValidateAccess(MethodInfo methodInfo, CoreBotUser user)
        {
            Object[] attributes = methodInfo.GetCustomAttributes(true);

            foreach (var attribute in attributes)
            {
                if (attribute.GetType() == typeof(Role))
                {
                    var access = ((Role)attribute).UserAccess;
                    if (user.UserAccess != access)
                    {
                        //CoreBot.SendMessage(chatId, "No access", ParseMode.Markdown);
                        return false;
                    }
                }
            }

            return true;
        }

        //Telegram Bot methods
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
                Log.Error(ex, "SendMessageAsync error");
            }
        }
        public async void EditMessageTextAsync(ChatId chatId, int messageId, string text, ParseMode parseMode = ParseMode.Default, bool disableWebPagePreview = false, InlineKeyboardMarkup replyMarkup = null, CancellationToken cancellationToken = default)
        {
            try
            {
                //Markdown cant parse single _ symbol so we need change it  to \\_
                if (parseMode == ParseMode.Markdown || parseMode == ParseMode.MarkdownV2)
                    text = text.Replace("_", "\\_");

                await Bot.EditMessageTextAsync(chatId, messageId, text, parseMode, disableWebPagePreview, replyMarkup, cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "EditMessageTextAsync error");
            }
        }
        public async void SendLocationAsync(ChatId chatId, float latitude, float longitude, int livePeriod = 0, bool disableNotification = false, int replyToMessageId = 0, IReplyMarkup replyMarkup = null, CancellationToken cancellationToken = default)
        {
            try
            {
                await Bot.SendLocationAsync(chatId, latitude, longitude);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "SendLocationAsync error");
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
                Log.Error(ex, "DeleteMessageAsync error");
            }
        }
    }
}
