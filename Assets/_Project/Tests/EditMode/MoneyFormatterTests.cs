using AutoService.Domain.Common;
using AutoService.Services.Formatting;
using NUnit.Framework;

namespace AutoService.Tests.EditMode
{
    /// <summary>Tests for <see cref="MoneyFormatter"/>.</summary>
    public sealed class MoneyFormatterTests
    {
        [TestCase(0L, "$0")]
        [TestCase(950L, "$950")]
        [TestCase(999L, "$999")]
        [TestCase(1_000L, "$1K")]
        [TestCase(1_050L, "$1.05K")]
        [TestCase(1_200L, "$1.2K")]
        [TestCase(1_234L, "$1.23K")]
        [TestCase(12_345L, "$12.3K")]
        [TestCase(123_456L, "$123K")]
        [TestCase(999_999L, "$999K")]
        [TestCase(1_234_567L, "$1.23M")]
        [TestCase(4_500_000_000L, "$4.5B")]
        [TestCase(1_200_000_000_000L, "$1.2T")]
        [TestCase(long.MaxValue, "$9.22Qi")]
        public void Format_ProducesShortTruncatedText(long amount, string expected)
        {
            Assert.AreEqual(expected, MoneyFormatter.Format(new Money(amount)));
        }
    }
}
