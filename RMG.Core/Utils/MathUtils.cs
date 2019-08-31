using System;

namespace RMG.Core.Utils
{
    public static class MathUtils
    {
        public static int Mod(int a, int b)
        {
            var d = a % b;
            return d >= 0 ? d : d + b;
        }

        public static double Mod(double a, double b)
        {
            return a - b * Math.Floor(a / b);
        }
    }
}
