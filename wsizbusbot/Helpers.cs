using Newtonsoft.Json;
using OpenWeatherMap;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace wsizbusbot
{
    public static class DateHelper
    {
        public static DateTime? IsDate(string str)
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
    }
    public class FileStorageManager<T>
    {
        string FilePath { get; set; }
        public List<T> entities = new List<T>();

        public FileStorageManager(string filepath)
        {
            FilePath = filepath;
            LoadFromFile();
        }
        public List<T> Set()
        {
            return entities;
        }
        public void Add(T entity)
        {
            entities.Add(entity);
        }

        //Serialize and write object to .json file
        public void Save()
        {
            string jsonFile = JsonConvert.SerializeObject(entities);

            File.WriteAllText(FilePath, jsonFile);
        }

        //Read file and deserialize to object
        void LoadFromFile()
        {
            try
            {
                string dataString = File.ReadAllText(FilePath);
                entities = JsonConvert.DeserializeObject<List<T>>(dataString);
            }
            catch (Exception ex)
            {
                if (ex is FileNotFoundException)
                    Console.WriteLine($"File {FilePath} not found.\nCreating a new one...");
                else
                    Console.WriteLine(ex.ToString());
            }
            entities = new List<T>();
        }
    }
    public class ArgParser
    {
        public static Dictionary<string, string> ParseCallbackData(string query)
        {
            var returnDictionary = new Dictionary<string, string>();

            var queryArgs = query.Split('?').ToList();

            string methodName = queryArgs.First();
            methodName = methodName.First().ToString().ToUpper() + methodName.Substring(1);

            returnDictionary.Add("MethodName", methodName);

            if (query.Contains("?"))
            {
                var args = query.Split('?').Skip(1).FirstOrDefault()?.Split(',');

                foreach (var arg in args)
                {
                    var parsedArg = arg.Split("=");
                    returnDictionary.Add(parsedArg[0], parsedArg[1]);
                }
            }

            return returnDictionary;
        }
        public static Dictionary<string, object> ParseCommand(string query)
        {
            var returnDictionary = new Dictionary<string, object>();

            var queryArgs = query.Split(' ').ToList();

            string methodName = queryArgs.First().Trim('/');
            methodName = methodName.First().ToString().ToUpper() + methodName.Substring(1);

            returnDictionary.Add("MethodName", methodName);

            if (queryArgs.Count > 1)
                returnDictionary.Add("args", queryArgs.Skip(1).ToList());

            return returnDictionary;
        }
    }
    public static class ScheduleHelper
    {
        public static async Task<bool> ParseFiles()
        {
            try
            {
                var files = Directory.GetFiles(Config.DataPath, "*.xlsx");
                if (files.Count() == 0)
                    ApplicationData.StopMode = true;

                foreach (var filename in files)
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
        public static void GetDataTableFromExcel(string path, bool hasHeader = true)
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

                    ApplicationData.Schedule.StationNames.Add(cell.Text + '/' + cell.Address);
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
                            var _isDate = DateHelper.IsDate(cell.Text);
                            if (_isDate != null)
                            {
                                ApplicationData.Schedule.CreateDay(_isDate.Value);
                            }
                        }
                        else
                        {
                            ApplicationData.Schedule.AddStation(cell.Text, cell.Address);
                        }
                    }
                }
            }
            Console.WriteLine($"{path} is parsed in {DateTime.Now - start}ms");
        }

        public static string GenerateSchedule(DateTime date, Way direction, int lang)
        {
            string retString = Local.NoDataForMonth[lang];

            string directionName = direction == Way.ToCTIR ? Local.ToCtir[lang] : Local.ToRzeszow[lang];

            var day = ApplicationData.Schedule.Days.Where(d => d.DayDateTime.Day == date.Day && d.DayDateTime.Month == date.Month).FirstOrDefault();
            if (day == null)
                return retString;

            var filtered = day.Stations.Where(s => s.Destination == direction && s.Time != null).ToList();
            if (filtered.Count() == 0)
                return retString;

            var grouped = filtered.GroupBy(x => x.RouteId).ToList();

            var monthName = Local.GetMonthNames(lang)[date.Month];

            string weather = WeatherHelper.GetWeatherForBusRide(date, lang).Result;

            string harmonogram = $"*{Local.GetDaysOfWeekNames(lang)[(int)date.DayOfWeek]} {date.Day} {monthName}* {weather}{Local.BusSchedule[lang]} {directionName}*\n\n";

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
    }

    public static class WeatherHelper
    {
        private static OpenWeatherMapClient Client = new OpenWeatherMapClient("94d06ec9cb3d5ea1000cc2e9ccf05492");
        public static WeatherForecast WeatherForecast { get; set; } = new WeatherForecast();

        public static async Task<List<List<ForecastTime>>> Update()
        {
            if (WeatherForecast.LastUpdate.AddHours(1) < DateTime.UtcNow)
            {
                WeatherForecast.LastUpdate = DateTime.UtcNow;

                var responseEn = await Client.Forecast.GetByName("Rzeszow", language: OpenWeatherMapLanguage.EN);
                var responseUa = await Client.Forecast.GetByName("Rzeszow", language: OpenWeatherMapLanguage.UA);
                var responsePl = await Client.Forecast.GetByName("Rzeszow", language: OpenWeatherMapLanguage.PL);

                WeatherForecast.Forecasts = new List<List<ForecastTime>> { responseEn.Forecast.ToList(), responseUa.Forecast.ToList(), responsePl.Forecast.ToList() };

                WeatherForecast.Forecasts.ForEach(f => f.ForEach(a => a.Symbol.Name = char.ToUpper(a.Symbol.Name[0]) + a.Symbol.Name.Substring(1)));
                WeatherForecast.Forecasts.ForEach(f => f.ForEach(a => a.From = a.From.AddHours(2)));

                return WeatherForecast.Forecasts;
            }
            return WeatherForecast.Forecasts;
        }

        public static async Task<string> GetWeatherForBusRide(DateTime forDay, int userLanguage)
        {
            string result = "";

            try
            {
                var forecasts = await Update();
                var translatedForecasts = forecasts[userLanguage];

                //If is not forecasts for that day
                if (!translatedForecasts.Select(a => a.From.Date).Contains(forDay))
                    throw new Exception();

                var selectedForecasts = translatedForecasts.Where(a => a.From.Date == forDay && a.From.Hour > 6).ToList();
                foreach (var item in selectedForecasts.Take(5))
                {
                    List<int> musTHaveUmbrella = new List<int>() { 611, 612, 615, 616 };

                    if (item.Symbol.Number < 300)
                        result += "🌩";
                    else if (item.Symbol.Number < 600 || musTHaveUmbrella.Contains(item.Symbol.Number) || item.Symbol.Number == 906)
                        result += "🌧";
                }
                result += $"` {(selectedForecasts.First().Temperature.Value - 273.15).ToString("f1")}°C`\n";
                return result;
            }
            catch
            {
                return "\n";
            }
        }

        public static async Task<string> GetWeather(int userLanguage)
        {
            string result = $"*{Local.WeatherInRzeszow[userLanguage]} {DateTime.UtcNow.AddHours(2).ToShortDateString()}*\n\n";

            try
            {
                var forecasts = await Update();
                var translatedForecasts = forecasts[userLanguage];

                foreach (var item in translatedForecasts.Take(12))
                {
                    result += $"`{item.From.ToString("HH:MM")}`  {item.Symbol.Name} `{(item.Temperature.Value - 273.15).ToString("f1")}°C`\n";
                }
            }
            catch
            {}

            return result;
        }
    }
}
