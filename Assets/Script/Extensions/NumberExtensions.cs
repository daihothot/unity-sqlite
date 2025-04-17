namespace Guru.SDK.Framework.Utils.Extensions
{
    using System;
    public static class NumberExtensions
    {
        public static int Clamp(this int value, int min, int max)
        {
            return Math.Clamp(value, min, max);
        }

        public static long Clamp(this long value, long min, long max)
        {
            return Math.Clamp(value, min, max);
        }

        public static double Clamp(this double value, double min, double max)
        {
            return Math.Clamp(value, min, max);
        }
        
        public static float Clamp(this float value, float min, float max)
        {
            return Math.Clamp(value, min, max);
        }
    }
}