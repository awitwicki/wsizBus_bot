using System;
using System.Collections.Generic;
using System.Text;

namespace wsizbusbot
{
    public class User
    {
        public string Name { get; set; }
        public string UserName { get; set; }
        public DateTime ActiveAt { get; set; }
        public long Id { get; set; }
        public LocalLanguage Language { get; set; }
        public Acceess Access { get; set; }
        public int GetLanguage
        {
            get { return (int)Language; }
        }
    }
    public class Stats
    {
        public DateTime Date { get; set; }
        public long ActiveClicks { get; set; }
        public List<long> UserId { get; set; } = new List<long>();
    }
    public enum Acceess
    {
        User = 0,
        Admin = 1,
        Ban = 2
    }
}
