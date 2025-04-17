using System;

namespace Guru.SDK.Framework.Utils.DateTime
{
    public static class DateTimeUtils
    {
        public const long SecondInMillis = 1000;
        public const long MinuteInMillis = SecondInMillis * 60;
        public const long FiveMinutesInMillis = MinuteInMillis * 5;
        public const long HourInMillis = SecondInMillis * 3600;
        public const long SixHourInMillis = HourInMillis * 6;
        public const long QuarterOfHourInMillis = MinuteInMillis * 15;
        public const long HalfHourInMillis = MinuteInMillis * 30;
        public const long DayInMillis = HourInMillis * 24;
        public const long WeekInMillis = DayInMillis * 7;

        public const int MinuteInSecond = 60;
        public const int HourInSecond = 3600;
        public const int QuarterOfHourInSecond = 900;
        public const int HalfHourInSecond = 1800;


        public static System.DateTime Now => System.DateTime.Now;

        public static System.DateTime UtcNow => System.DateTime.UtcNow;

        public static string yyyyMMdd => Now.ToString(DateTimeFormats.yyyyMMddDateFormat);

        public static string yyMMdd => Now.ToString(DateTimeFormats.yyMMddDateFormat);

        public static string yyyyMM => Now.ToString(DateTimeFormats.yyyyMMDateFormat);

        public static string MMM => Now.ToString(DateTimeFormats.MMMDateFormat);

        public static string StandardDatetime => Now.ToString(DateTimeFormats.StandardDatetimeFormat);

        public static string CompactDatetime => Now.ToString(DateTimeFormats.yyyyMMddTHHmmssDateTimeFormat);

        public static string StandardTime => Now.ToString(DateTimeFormats.StandardTimeFormat);

        public static int yyyyMMddUtcNum
        {
            get
            {
                var now = UtcNow;
                return now.Year * 10000 + now.Month * 100 + now.Day;
            }
        }

        public static int yyyyMMddNum
        {
            get
            {
                var now = Now;
                return now.Year * 10000 + now.Month * 100 + now.Day;
            }
        }

        public static int YearMonthNum => GenerateYearMonthNum(Now);

        public static string yyyyMMddBuild(System.DateTime dateTime) =>
            $"{dateTime.Year}{dateTime.Month:D2}{dateTime.Day:D2}";

        public static string yyyyMMBuild(System.DateTime dateTime) =>
            $"{dateTime.Year}{dateTime.Month:D2}";

        public static int GenerateYearMonthNum(System.DateTime dateTime) =>
            dateTime.Year * 100 + dateTime.Month;

        public static int GenerateYearMonthDayNum(System.DateTime dateTime) =>
            dateTime.Year * 10000 + dateTime.Month * 100 + dateTime.Day;

        public static int YyyyMMddStr2Num(string yyyyMMdd) =>
            int.Parse(yyyyMMdd);

        public static string yyyyMMddStr2YyyyMMStr(string yyyyMMdd) =>
            yyyyMMdd[..6];

        public static int yyyyMMddStr2YyyyMMNum(string yyyyMMdd) =>
            int.Parse(yyyyMMddStr2YyyyMMStr(yyyyMMdd));

        public static string FormatMMdd(System.DateTime dateTime) =>
            $"{dateTime.Month:D2}{dateTime.Day:D2}";

        public static int Millis2Second(int millis) =>
            millis / 1000;

        public static int Minutes2Millis(int minutes) =>
            minutes * (int)MinuteInMillis;

        public static int Minutes2Seconds(int minutes) =>
            minutes * MinuteInSecond;

        public static int Hour2Millis(int hours) =>
            hours * (int)HourInMillis;

        public static int Hour2Seconds(int hours) =>
            hours * HourInSecond;

        public static System.DateTime CreateDateTimeFromDateNum(int yyyyMMdd)
        {
            var year = yyyyMMdd / 10000;
            var month = (yyyyMMdd - (year * 10000)) / 100;
            var day = yyyyMMdd - (year * 10000 + month * 100);
            return new System.DateTime(year, month, day);
        }

        public static System.DateTime CreateDateTimeFromYearMonth(int yyyyMM)
        {
            var year = yyyyMM / 100;
            var month = yyyyMM - (year * 100);
            return new System.DateTime(year, month, 1);
        }

        public static long CurrentTimeInSecond()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public static long CurrentTimeInMillis()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}