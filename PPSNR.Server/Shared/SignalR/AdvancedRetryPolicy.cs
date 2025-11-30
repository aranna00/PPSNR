using System.Net;
using System.Net.WebSockets;
using System.Security.Authentication;
using Microsoft.AspNetCore.SignalR.Client;

namespace PPSNR.Server.Shared.SignalR;

/// <summary>
/// Custom SignalR retry policy implementing:
/// - Exponential backoff with jitter
/// - Error-type differentiation (stop on auth/config errors, fast on transient)
/// - Simple circuit breaker (threshold in window, cooldown)
/// </summary>
public sealed class AdvancedRetryPolicy : IRetryPolicy
{
    public sealed class Options
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

    private readonly Options _opt;

    // failure timestamps within window
    private readonly LinkedList<DateTimeOffset> _failures = new();
    private DateTimeOffset? _circuitOpenUntil;
    private readonly object _gate = new();

    // random per instance
    private readonly ThreadLocal<Random> _rand = new(() => new Random(unchecked(Environment.TickCount * 397) ^ Guid.NewGuid().GetHashCode()));

    public AdvancedRetryPolicy() : this(new Options()) { }

    public AdvancedRetryPolicy(Options options)
    {
        _opt = options ?? new Options();
    }

    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        var now = DateTimeOffset.UtcNow;

        // Stop if we've reached an overall retry cap
        if (_opt.MaxRetryCount.HasValue && retryContext.PreviousRetryCount >= _opt.MaxRetryCount.Value)
            return null;

        // Differentiate based on the error type
        if (IsNonRetryable(retryContext.RetryReason))
        {
            return null; // don't retry on auth/configuration problems
        }

        // Circuit breaker accounting
        TimeSpan? breakerDelay = null;
        lock (_gate)
        {
            // Prune old failures
            Prune(now);
            _failures.AddLast(now);

            if (_circuitOpenUntil.HasValue)
            {
                if (now < _circuitOpenUntil.Value)
                {
                    breakerDelay = _circuitOpenUntil.Value - now;
                }
                else
                {
                    // cooldown elapsed; close circuit
                    _circuitOpenUntil = null;
                }
            }

            if (breakerDelay is null && _failures.Count >= _opt.CircuitBreakerThreshold)
            {
                _circuitOpenUntil = now + _opt.CircuitBreakerCooldown;
                breakerDelay = _opt.CircuitBreakerCooldown;
                // reset the rolling window when opening the circuit
                _failures.Clear();
            }
        }

        if (breakerDelay is not null)
        {
            // Keep reconnecting, but after cooldown period
            return CapAndJitter(breakerDelay.Value, forceAsIs: true);
        }

        // Prefer shorter delays for well-known transient network errors
        var baseDelay = IsHighlyTransient(retryContext.RetryReason)
            ? TimeSpan.FromMilliseconds(Math.Min(_opt.InitialBackoff.TotalMilliseconds, 250))
            : _opt.InitialBackoff;

        // Standard exponential backoff using the previous retry count
        var exp = Math.Pow(_opt.Multiplier, Math.Max(0, retryContext.PreviousRetryCount));
        var delay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * exp);
        return CapAndJitter(delay);
    }

    private TimeSpan CapAndJitter(TimeSpan delay, bool forceAsIs = false)
    {
        // Cap
        var capped = delay > _opt.MaxBackoff ? _opt.MaxBackoff : delay;

        if (forceAsIs || _opt.JitterRatio <= 0)
            return capped;

        var r = _rand.Value!;
        var jitterRangeMs = capped.TotalMilliseconds * _opt.JitterRatio;
        var delta = (r.NextDouble() * 2 - 1) * jitterRangeMs; // [-j..+j]
        var jitteredMs = Math.Max(0, capped.TotalMilliseconds + delta);
        return TimeSpan.FromMilliseconds(jitteredMs);
    }

    private static void AddStatusCodes(HashSet<HttpStatusCode> set)
    {
        set.Add(HttpStatusCode.Unauthorized);
        set.Add(HttpStatusCode.Forbidden);
        set.Add(HttpStatusCode.NotFound);
    }

    private static bool IsNonRetryable(Exception? ex)
    {
        // Authentication/configuration issues that are unlikely to succeed without user action
        if (ex is null) return false;

        // HttpRequestException with specific status codes
        if (ex is HttpRequestException httpEx && httpEx.StatusCode is HttpStatusCode code)
        {
            return code is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound;
        }

        // TLS/SSL failures
        if (ex is AuthenticationException)
            return true;

        // Protocol/negotiation errors indicating misconfiguration
        if (ex is InvalidOperationException ioe && ioe.Message.IndexOf("negot", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }

    private static bool IsHighlyTransient(Exception? ex)
    {
        if (ex is null) return false;
        return ex is TimeoutException
               || ex is TaskCanceledException
               || ex is WebSocketException
               || ex is OperationCanceledException
               || (ex is HttpRequestException httpEx && httpEx.StatusCode is null);
    }

    private void Prune(DateTimeOffset now)
    {
        while (_failures.First is { } node)
        {
            if (now - node.Value <= _opt.CircuitBreakerWindow) break;
            _failures.RemoveFirst();
        }
    }
}
