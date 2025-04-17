namespace Guru.SDK.Framework.Utils.DateTime
{
    /// <summary>
    /// 为了便于理解，这里的命名规范并没有按照 C# 的命名规范来
    /// </summary>
    public static class DateTimeFormats
    {
        
        public const string yyyyMMddDateFormat = "yyyyMMdd";
        public const string yyMMddDateFormat = "yyMMdd";
        public const string yyyyMMNormalDateFormat = "yyyy-MM";
        public const string yyyyMMddNormalDateFormat = "yyyy-MM-dd";
        public const string MMddNormalDateFormat = "MM.dd";
        public const string StandardDatetimeFormat = "yyyy-MM-dd HH:mm:ss";
        public const string StandardTimeFormat = "HH:mm:ss";
        public const string yyyyMMddTHHmmssDateTimeFormat = "yyyyMMddTHHmmss";
        
        public const string yyyyMMDateFormat = "yyyyMM";
        public const string MMMDateFormat = "MMM";
        public const string MonthAbbreviatedFormat = "MMM";
        public const string HumanDateFormat = "M/d/yyyy h:mm tt";  // 对应 yMd().add_jm()

    }
}