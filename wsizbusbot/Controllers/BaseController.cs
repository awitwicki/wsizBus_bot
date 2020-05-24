using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Telegram.Bot.Args;
using Telegram.Bot.Types;

namespace wsizbusbot.Controllers
{
    public class BaseController
    {
        public class Role: System.Attribute
        {
            public Acceess Access { get; set; }
            public Role(Acceess access)
            {
                this.Access = access;
            }
        }

        public static bool ValidateMethodAttributes(ChatId chatId, MethodInfo methodInfo, User user)
        { 
            Object[] attributes = methodInfo.GetCustomAttributes(true);
            
            foreach (var attribute in attributes)
            {
                if (attribute.GetType() == typeof(Role))
                {
                    var access = ((Role)attribute).Access;
                    if (user.Access != access)
                    {
                        //CoreBot.SendMessage(chatId, "No access", Telegram.Bot.Types.Enums.ParseMode.Markdown);
                        return false;
                    }
                }
            }
            
            return true;
        }
    }
}
