namespace World.NET;

internal static class ConstantNumbers
{
    public const double kPi = 3.1415926535897932384;
    public const double kMySafeGuardMinimum = 0.000000000001;
    public const double kDefaultF0 = 500.0;

    // for D4C()
    public const double kUpperLimit = 15000.0;
    public const double kFrequencyInterval = 3000.0;

    // for Codec (Mel scale)
    // S. Stevens & J. Volkmann,
    // The Relation of Pitch to Frequency: A Revised Scale,
    // American Journal of Psychology, vol. 53, no. 3, pp. 329-353, 1940.
    public const double kM0 = 1127.01048;
    public const double kF0 = 700.0;
    public const double kFloorFrequency = 40.0;
    public const double kCeilFrequency = 20000.0;
}