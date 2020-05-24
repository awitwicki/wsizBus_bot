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
    public static class FileHelper
    {
        //Serialize and write object to xml file
        public static void SerializeObject<T>(T serializableObject, string fileName)
        {
            if (serializableObject == null) { return; }

            try
            {
                XmlDocument xmlDocument = new XmlDocument();
                XmlSerializer serializer = new XmlSerializer(serializableObject.GetType());
                using (MemoryStream stream = new MemoryStream())
                {
                    serializer.Serialize(stream, serializableObject);
                    stream.Position = 0;
                    xmlDocument.Load(stream);
                    xmlDocument.Save(fileName);
                    return;
                }
            }
            catch (Exception ex)
            {
                //Log exception here
                Console.WriteLine(ex.ToString());
            }
        }

        //Read file and deserialize to object
        public static T DeSerializeObject<T>(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) { return default(T); }

            T objectOut = default(T);

            try
            {
                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.Load(fileName);
                string xmlString = xmlDocument.OuterXml;

                using (StringReader read = new StringReader(xmlString))
                {
                    Type outType = typeof(T);

                    XmlSerializer serializer = new XmlSerializer(outType);
                    using (XmlReader reader = new XmlTextReader(read))
                    {
                        objectOut = (T)serializer.Deserialize(reader);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is FileNotFoundException)
                    Console.WriteLine($"File {fileName} not found");
                else
                    Console.WriteLine(ex.ToString());
            }

            return objectOut;
        }

        //Download files from web
      /*  public static void DownloadFleFromUrl(string url, string fileName)
        {
           
                using (WebClient client = new WebClient())
                {
                    client.DownloadFile(new Uri(url), $"{Config.DataPath}{fileName}");
                }
                var res = await TrySendMessage(Config.AdminId, "Downloaded", ParseMode.Default);

                if (res != null)
                    await Bot.EditMessageTextAsync(res.Chat.Id, res.MessageId, res.Text + "\nParsed", ParseMode.Default);
            }
            catch (Exception ex)
            {
                await TrySendMessage(Config.AdminId, ex.ToString(), ParseMode.Default);
            }
            return;
        }*/

    }
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
        public void Save()
        {
            //Save to file
            FileHelper.SerializeObject<List<T>>(entities, FilePath);
        }
        void LoadFromFile()
        {
            var file_entities = FileHelper.DeSerializeObject<List<T>>(FilePath);
            if (file_entities == null)
                FileHelper.SerializeObject<List<T>>(new List<T>(), FilePath);
            else
                entities = file_entities;
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
    }
}
