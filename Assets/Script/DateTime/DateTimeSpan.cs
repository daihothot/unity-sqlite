namespace Guru.SDK.Framework.Utils.DateTime
{
    using System;

    public readonly struct DateTimeSpan
    {
        public readonly DateTime Begin;
        public readonly DateTime End;
        public TimeSpan Duration => End - Begin;
        
        public TimeSpan Remaining => End - DateTime.Now;

        public DateTimeSpan(DateTime begin, DateTime end)
        {
            if (begin > end)
                throw new ArgumentException("Start time must be before end time");

            Begin = begin;
            End = end;
        }

        public bool IsExpired() => DateTime.Now > End;

        public override string ToString()
        {
            return $"Begin: {Begin}, End: {End}, Duration: {Duration}";
        }
        
        public long BeginTimeInMillis => new DateTimeOffset(Begin).ToUnixTimeMilliseconds();
        
        public long EndTimeInMillis => new DateTimeOffset(End).ToUnixTimeMilliseconds();
    }
}