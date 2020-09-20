using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Telegram.Bot.Args;

namespace wsizbusbot
{
    public static class ApplicationData
    {
        public static string BotVersion = "2.8 Beta 20092020";

        public static FileStorageManager<CoreBotUser> Users { get; set; } = new FileStorageManager<CoreBotUser>(Config.UsersFilePath);
        public static FileStorageManager<Stats> Stats { get; set; } = new FileStorageManager<Stats>(Config.StatsFilePath);
        public static Schedule Schedule = new Schedule();

        public static bool StopMode = false;

        public static CoreBotUser GetUser(int userId)
        {
            return Users.Set().Where(u => u.Id == userId).FirstOrDefault();
        }
        public static CoreBotUser AddOrUpdateUser(CoreBotUser user)
        {
            var usr = Users.Set().FirstOrDefault(u => u.Id == user.Id);

            //Store Users
            if (usr == null)
            {
                //If new User then add
                Users.Add(user);

                Serilog.Log.Information($"New User {user.Name} {user.UserName}");
                //bot.SendTextMessageAsync(Config.AdminId, $"New User {user.Name} {user.UserName}");
            }
            else
            {
                //Update User
                usr.ActiveAt = DateTime.UtcNow;
            }

            //Save to file
            ApplicationData.Users.Save();

            return usr;
        }
        public static void SaveUsers()
        {  
            //Save to file
            ApplicationData.Users.Save();
        }
        public static void UpdateStats(Telegram.Bot.Types.User user)
        {
            var statsForToday = Stats.Set().Where(s => s.Date == DateTime.UtcNow.Date).FirstOrDefault();

            if (statsForToday == null)
            {
                //Create new day
                Stats.Add(new Stats
                {
                    ActiveClicks = 1,
                    Date = DateTime.UtcNow.Date,
                    UserId = new List<long>() { user.Id }
                });
            }
            else
            {
                statsForToday.ActiveClicks++;
                if (!statsForToday.UserId.Contains(user.Id))
                {
                    statsForToday.UserId.Add(user.Id);
                };
            }

            //Save to file
            Stats.Save();
        }
    }
}
