namespace PPSNR.Server.Shared;

public sealed class RetryOptions
{
    public TimeSpan InitialBackoff { get; init; } = TimeSpan.FromMilliseconds(500);

    public TimeSpan MaxBackoff { get; init; } = TimeSpan.FromSeconds(30);

    public double Multiplier { get; init; } = 2.0; // exponential factor

    public double JitterRatio { get; init; } = 0.2; // +/-20%

    // Circuit breaker
    public int CircuitBreakerThreshold { get; init; } = 6; // failures

    public TimeSpan CircuitBreakerWindow { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan CircuitBreakerCooldown { get; init; } = TimeSpan.FromSeconds(60);

    // Overall retry cap (null for unlimited; circuit breaker still applies)
    public int? MaxRetryCount { get; init; }
}