using System;
using System.Collections.Generic;
using System.Text;

namespace wsizbusbot
{
    public static class Local
    {
        public static Dictionary<LocalLanguage, String[]> DaysOfWeek = new Dictionary<LocalLanguage, string[]>
        {
             { LocalLanguage.English, new string[]{"Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" }},
             { LocalLanguage.Ukrainian, new string[]{"Неділя", "Понеділок", "Вівторок", "Середа", "Четвер", "П’ятниця", "Субота"}},
             { LocalLanguage.Polish, new string[]{"Niedziela", "Poniedziałek", "Wtorek", "Środa", "Czwartek", "Piątek", "Sobota"}},
        };
        public static Dictionary<LocalLanguage, String[]> MonthsVocalubrary = new Dictionary<LocalLanguage, string[]>
        {
             { LocalLanguage.English, new string[]{"January", "February", "Marc", "April", "May", "June", "July", "August", "September", "October", "November" }},
             { LocalLanguage.Ukrainian, new string[]{"Cічень", "Лютий", "Березень", "Квітень","Травень","Червень","Липень","Серпень","Вересень","Жовтень","Листопад","Грудень"}},
             { LocalLanguage.Polish, new string[]{"Styczeń", "Luty", "Marzec", "Kwiecień", "Maj", "Czerwiec", "Lipiec", "Sierpień", "Wrzesień", "Październik", "Listopad", "Grudzień"}},
        };

        public static Dictionary<PointNames, float[]> BusPoints = new Dictionary<PointNames, float[]>
        {
             { PointNames.OfiarKatynia, new float[]{ 50.050028f, 21.976944f }},
             { PointNames.Cieplinskiego, new float[]{ 50.038889f, 21.997722f}},
             { PointNames.Powst, new float[]{ 50.017701f, 22.015266f}},
             { PointNames.Tesco, new float[]{ 50.018539f, 22.013951f}},
             { PointNames.Tyczyn, new float[]{ 49.964729f, 22.030035f}},
             { PointNames.CTIR, new float[]{ 49.949825f, 22.059084f}},
        };

        public static Dictionary<PointNames, string> BusPointNames = new Dictionary<PointNames, string>
        {
             { PointNames.OfiarKatynia, "Ofiar Katynia"},
             { PointNames.Cieplinskiego, "Cieplińskiego"},
             { PointNames.Powst, "Powstańców Warszawy"},
             { PointNames.Tesco, "Tesco"},
             { PointNames.Tyczyn, "Tyczyn"},
             { PointNames.CTIR, "Ctir"},
        };

        public static List<string> StartString = new List<string>() { "Hi! I know where and whe will be wsizbus, select direction\n\n", "Привіт, Я знаю де і коли буде всізобус, щоб дізнатися - обери куди ти хочеш доїхати\n\n", "Cześć, wiem kiedy i gdzie będzie jechał wsizbus, najpierw wybierz kierunek\n\n" };
        public static List<string> Today = new List<string>() { "Today", "Сьогодні", "Dziś" };
        public static List<string> ReturnBack = new List<string>() { "Back", "Назад", "Powrót" };
        public static List<string> Tomorrow = new List<string>() { "Tomorrow", "Завтра", "Jutro" };
        public static List<string> PickDate = new List<string>() { "Pick a date", "Обери дату", "Wybierz datę" };
        public static List<string> PickMonth = new List<string>() { "Pick a month", "Обери місяць", "Wybierz miesiąc" };
        public static List<string> PickBusStation = new List<string>() { "Pick the bus station", "Обери пшистанек", "Wybierz Przystanek" };
        public static List<string> AnotherMonth = new List<string>() { "Another month", "Інший місяць", "Inny miesiąc" };
        public static List<string> YouAreWelcome = new List<string>() { "You are welcome", "Запрашами ще", "Zapraszamy ponownie" };
        public static List<string> ErrorMessage = new List<string>() { "Some error o.O", "Сорі, щось пішло не так (*варунек*)", "Jakiś błąd o.O" };
        public static List<string> NoDataForMonth = new List<string>() { "No schedules for that month", "Нема розкладу на цей місяць ", "Nie ma harmonogramu na ten miesiąc, wybierz inny" };
        public static List<string> PickAnother = new List<string>() { ", pick another.", ", обери інший.", ", wybierz inny" };
        public static List<string> FirstBus = new List<string>() { "First bus leaves at:", "Перший бус їде:", "Pierwszy bus jedzie:" };
        public static List<string> ThenLikeAlways = new List<string>() { "Then as always:", "Потім як звикле:", "Potem jak zwykle:" };
        public static List<string> ToCtir = new List<string>() { "CTIR", "Кельнарової", "Kielnarowej" };
        public static List<string> ToRzeszow = new List<string>() { "Rzeszów", "Жешова", "Rzeszowa" };
        public static List<string> BusSchedule = new List<string>() { "bus schedule\n*to", "розклад бусiв\n*до", "harmonogram autobusów\n*do" };
        public static List<string> BusStations = new List<string>() { "WsizBus stations (test version)", "Пшистанкі всізбуса (версія тестова)", "Przystanki WsizBusa (wersja testowa)" };
        public static List<string> Menu = new List<string>() { "Main menu", "Голоне меню", "Główne menu" };
        

        public static List<string> LangIcon = new List<string>() { "🇬🇧", "🇺🇦", "🇮🇩" };
                
        public static String[] GetDaysOfWeekNames(LocalLanguage lang)
        {
            return DaysOfWeek[lang];
        }
        public static String[] GetDaysOfWeekNames(int lang)
        {
            return DaysOfWeek[(LocalLanguage)lang];
        }
        public static String[] GetMonthNames(LocalLanguage lang)
        {
            return MonthsVocalubrary[lang];
        }
        public static String[] GetMonthNames(int lang)
        {
            return MonthsVocalubrary[(LocalLanguage)lang];
        }

        public static string GetWayDisplayName(Way way)
        {
            return way == Way.ToCTIR ? "Rzeszów => CTIR" : "CTIR => Rzeszów";
        }
        public enum PointNames
        {
            OfiarKatynia,
            Cieplinskiego,
            Powst,
            Tesco,
            Tyczyn,
            CTIR
        }
    }
    public enum LocalLanguage
    {
        English = 0,
        Ukrainian = 1,
        Polish = 2
    }
}
