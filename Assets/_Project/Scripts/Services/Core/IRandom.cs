namespace AutoService.Services.Core
{
    /// <summary>
    /// Source of randomness. Abstracted so chance-based logic (breakdowns, car types, negotiation)
    /// can be tested with a scripted fake.
    /// </summary>
    public interface IRandom
    {
        /// <summary>Returns a uniformly distributed value in [0, 1).</summary>
        float Value();

        /// <summary>Returns a uniformly distributed integer in [<paramref name="minInclusive"/>, <paramref name="maxExclusive"/>).</summary>
        int Range(int minInclusive, int maxExclusive);
    }
}
