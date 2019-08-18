namespace RMG.Core.Utils
{
    public static class PositionMath
    {
        public static double DenormalizePosition(double normalizedPosition, double offset, double scale)
        {
            var position = (normalizedPosition * scale + offset) % scale;
            return position >= 0 ? position : scale - position;
        }

        public static double DenormalizePosition(double normalizedPosition, double offset, double scale, int cycle)
        {
            return DenormalizePosition(normalizedPosition, offset, scale) + scale * cycle;
        }


        public static double NormalizePosition(double denormalizedPosition, double offset, double scale)
        {
            var position = (denormalizedPosition - offset) / scale % 1;
            return position >= 0 ? position : 1 - position;
        }
    }
}
