using System.Globalization;
using AutoService.Domain.Common;

namespace AutoService.Services.Formatting
{
    /// <summary>
    /// Converts <see cref="Money"/> into short player-facing text. Stateless pure function.
    /// </summary>
    public static class MoneyFormatter
    {
        private const long Thousand = 1000L;
        private const char DecimalSeparator = '.';

        /// <summary>
        /// "$0", "$950", "$1.2K", "$12.3K", "$123K", "$1.23M", "$4.5B", "$1.2T" (then "Qa", "Qi" up to <see cref="long.MaxValue"/>).
        /// Values &lt; 1000 are exact. Otherwise 3 significant digits, TRUNCATED (never shows more than the player has),
        /// trailing zeros removed ("$1K", not "$1.00K"). InvariantCulture.
        /// </summary>
        /// <remarks>
        /// Allocates the result string, so callers (presenters) should format only when the value changes, not every frame.
        /// </remarks>
        public static string Format(Money money)
        {
            long amount = money.Amount;
            if (amount < Thousand)
            {
                return "$" + amount.ToString(CultureInfo.InvariantCulture);
            }

            // Why: pure integer math — floating point would round 999_999 up to "1M" and break the truncation guarantee.
            int tier = 0;
            long divisor = 1L;
            while (amount / divisor >= Thousand)
            {
                divisor *= Thousand;
                tier++;
            }

            long integerPart = amount / divisor;
            int decimals = integerPart >= 100L ? 0 : integerPart >= 10L ? 1 : 2;
            long decimalScale = decimals == 0 ? 1L : decimals == 1 ? 10L : 100L;

            // Digits after the decimal point, truncated: e.g. 1_234 → 23 (of "1.23K").
            long fraction = amount / (divisor / decimalScale) % decimalScale;

            string integerText = integerPart.ToString(CultureInfo.InvariantCulture);
            string suffix = GetSuffix(tier);
            if (fraction == 0L)
            {
                return "$" + integerText + suffix;
            }

            // Pad to keep leading zeros ("1.05K"), then drop trailing zeros ("1.2K", not "1.20K").
            string fractionText = fraction
                .ToString(CultureInfo.InvariantCulture)
                .PadLeft(decimals, '0')
                .TrimEnd('0');

            return "$" + integerText + DecimalSeparator + fractionText + suffix;
        }

        private static string GetSuffix(int tier)
        {
            switch (tier)
            {
                case 1: return "K";
                case 2: return "M";
                case 3: return "B";
                case 4: return "T";
                case 5: return "Qa";
                default: return "Qi"; // tier 6 is the largest possible for a long (~9.22 quintillion).
            }
        }
    }
}
