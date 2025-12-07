// ============================================================================
// PPSNRRetryPolicy - Exponential Backoff with Circuit Breaker
// Shared retry policy for SignalR reconnection across the application
// ============================================================================

(function() {
    if (window.PPSNR?.RetryPolicy) {
        console.debug('[PPSNR.RetryPolicy] Already loaded, skipping duplicate registration');
        return;
    }

    class PPSNRRetryPolicy {
        constructor(options = {}) {
            this.initialBackoff = options.initialBackoff || 500; // ms
            this.maxBackoff = options.maxBackoff || 30000; // ms
            this.multiplier = options.multiplier || 2.0;
            this.jitterRatio = options.jitterRatio || 0.2; // +/-20%
            this.maxRetryCount = options.maxRetryCount || null; // unlimited

            this.retryCount = 0;
            this.failures = [];
            this.circuitBreakerThreshold = options.circuitBreakerThreshold || 6;
            this.circuitBreakerWindow = options.circuitBreakerWindow || 30000; // ms
            this.circuitBreakerCooldown = options.circuitBreakerCooldown || 60000; // ms
            this.circuitOpenUntil = null;
        }

        getNextRetryDelay() {
            const now = Date.now();

            // Check max retries
            if (this.maxRetryCount !== null && this.retryCount >= this.maxRetryCount) {
                return null;
            }

            // Circuit breaker logic
            if (this.circuitOpenUntil && now < this.circuitOpenUntil) {
                const delay = this.circuitOpenUntil - now;
                return this.capAndJitter(delay, true);
            } else if (this.circuitOpenUntil && now >= this.circuitOpenUntil) {
                this.circuitOpenUntil = null;
            }

            // Prune old failures outside the window
            this.failures = this.failures.filter(t => (now - t) <= this.circuitBreakerWindow);
            this.failures.push(now);

            // Check circuit breaker threshold
            if (this.failures.length >= this.circuitBreakerThreshold) {
                this.circuitOpenUntil = now + this.circuitBreakerCooldown;
                const delay = this.circuitBreakerCooldown;
                this.failures = [];
                console.debug('[PPSNR.RetryPolicy] circuit breaker activated for', Math.ceil(this.circuitBreakerCooldown / 1000), 'seconds');
                return this.capAndJitter(delay, true);
            }

            // Exponential backoff: baseDelay * (multiplier ^ retryCount)
            const baseDelay = this.initialBackoff;
            const exp = Math.pow(this.multiplier, Math.max(0, this.retryCount));
            const delay = baseDelay * exp;

            this.retryCount++;
            return this.capAndJitter(delay);
        }

        capAndJitter(delay, forceAsIs = false) {
            // Cap at max
            let capped = Math.min(delay, this.maxBackoff);

            if (forceAsIs || this.jitterRatio <= 0) {
                return capped;
            }

            // Add jitter: +/- jitterRatio%
            const jitterRangeMs = capped * this.jitterRatio;
            const delta = (Math.random() * 2 - 1) * jitterRangeMs;
            const jitteredMs = Math.max(0, capped + delta);
            return Math.round(jitteredMs);
        }

        reset() {
            this.retryCount = 0;
            this.failures = [];
            this.circuitOpenUntil = null;
        }
    }

    // Export to global namespace for use across the application
    window.PPSNR = window.PPSNR || {};
    window.PPSNR.RetryPolicy = PPSNRRetryPolicy;
    window.PPSNR.createRetryPolicy = function(options) {
        return new PPSNRRetryPolicy(options);
    };

    console.debug('[PPSNR.RetryPolicy] Retry policy module loaded');
})();
