using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;

namespace wsizbusbot
{
    //Black magic
    public class Schedule
    {
        public List<Day> Days = new List<Day>();
        public List<String> StationNames = new List<String>();
        public List<DateTime> avaliableMonths = new List<DateTime>();

        public void CreateDay(DateTime day)
        {
            if (!avaliableMonths.Contains(new DateTime(day.Year, day.Month , 1)))
                avaliableMonths.Add(new DateTime(day.Year, day.Month, 1));

            Days.Add(new Day(day));
        }
        public void AddStation(string time, string stationAddress)
        {
            if (stationAddress.Contains('A'))
                return;

            if (stationAddress.Contains('B'))
                Days.Last().StationsText.Add(new List<string>());

            var routeRow = Convert.ToInt32(stationAddress.Remove(0,1))-3;

            Days.Last().StationsText.Last().Add((time != "" && time != " ") ? time : "--");

            DateTime? datetime = null;

            if (time != "" && time != " ")
            {
                var hours = Convert.ToInt32(time.Split(':').First());
                var minutes = Convert.ToInt32(time.Split(':')[1]);

                datetime = Days.Last().DayDateTime.AddHours(hours).AddMinutes(minutes);
            }

            Way destination;
            if (stationAddress.Contains('H') || stationAddress.Contains('I') || stationAddress.Contains('J'))
                destination = Way.ToRzeszow;
            else destination = Way.ToCTIR;

            Days.Last().Stations.Add(new Station
            {
                RouteId = routeRow,
                Time = datetime,
                StationName = StationNames.Where(sn => sn.Contains(stationAddress[0])).FirstOrDefault()?.Split('/').First(),
                Destination = destination
            });
        }
    }
    public class Day
    {
        public DateTime DayDateTime { get; set; }
        public List<Station> Stations = new List<Station>();
        public List<List<String>> StationsText = new List<List<String>>();

        public Day(DateTime date)
        {
            DayDateTime = date;
        }
    }
    public class Station
    {
        public int RouteId { get; set; }
        public string StationName { get; set; }
        public DateTime? Time { get; set; }
        public Way Destination { get; set; }

        public string GenerateScheduleString()
        {
            return Time.HasValue ? (Time?.ToString("HH:mm") + " \t  ") : " ------- \t  ";
        }
    }

    public enum Way
    {
        ToRzeszow,
        ToCTIR
    }
}
