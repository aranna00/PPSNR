// Initialize window.PPSNR API for C# interop
window.PPSNR = window.PPSNR || {};
window.PPSNR.__reconnectState = "hidden";
window.PPSNR.isReconnecting = function() {
    return window.PPSNR.__reconnectState === "show" || window.PPSNR.__reconnectState === "failed" || window.PPSNR.__reconnectState === "rejected";
};

window.PPSNR.getReconnectState = function() {
    return window.PPSNR.__reconnectState;
};

console.debug('[ReconnectModal] loaded, initial state=', window.PPSNR.__reconnectState);

// Set up event listener for reconnect state changes
const reconnectModal = document.getElementById("components-reconnect-modal");
if (reconnectModal) {
    reconnectModal.addEventListener("components-reconnect-state-changed", handleReconnectStateChanged);
}

function handleReconnectStateChanged(event) {
    try {
        console.debug('[ReconnectModal] state changed ->', event.detail.state);

        if (window.PPSNR) {
            window.PPSNR.__reconnectState = event.detail.state;
        }

        const modal = document.getElementById("components-reconnect-modal");
        if (!modal) return;

        switch (event.detail.state) {
            case "show":
                modal.showModal();
                stopFailedRetryPolling();
                break;
            case "hide":
                modal.close();
                stopFailedRetryPolling();
                break;
            case "retrying":
            case "failed":
            case "resume-failed":
                console.debug('[ReconnectModal] reconnect attempt failed - starting automatic retry poller');
                startFailedRetryPolling();
                break;
            case "rejected":
                console.debug('[ReconnectModal] circuit rejected - reloading');
                stopFailedRetryPolling();
                location.reload();
                break;
        }
    } catch (e) {
        console.error('[ReconnectModal] handleReconnectStateChanged error:', e);
    }
}

// ============================================================================
// Custom Reconnection Retry Logic
// ============================================================================

let _retryTimeoutId = null;
let _retryInProgress = false;
let _nextRetryTime = 0;
let _countdownIntervalId = null;

function startFailedRetryPolling() {
    if (_retryTimeoutId !== null) return;

    _retryPolicy.reset();
    console.debug('[ReconnectModal] starting automatic retry with exponential backoff');

    // Show our custom retry counter
    const counter = document.getElementById('ppsnr-retry-counter');
    if (counter) counter.style.display = 'block';

    // Schedule first retry
    scheduleNextRetry();

    // Update countdown display every 100ms
    _countdownIntervalId = setInterval(updateCountdownDisplay, 100);
}

function scheduleNextRetry() {
    const delay = _retryPolicy.getNextRetryDelay();

    if (delay === null) {
        console.debug('[ReconnectModal] retry policy exhausted, giving up');
        return;
    }

    _nextRetryTime = Date.now() + delay;
    console.debug('[ReconnectModal] next retry in', Math.ceil(delay / 1000), 'seconds');

    _retryTimeoutId = setTimeout(async () => {
        if (_retryInProgress) return;
        _retryInProgress = true;

        const attemptNum = _retryPolicy.retryCount;
        const countSpan = document.getElementById('ppsnr-retry-count');
        if (countSpan) countSpan.textContent = attemptNum;

        try {
            console.debug('[ReconnectModal] retry attempt', attemptNum);
            await attemptReconnect();
        } finally {
            _retryInProgress = false;
            _retryTimeoutId = null;
            // Schedule next retry
            scheduleNextRetry();
        }
    }, delay);
}

function updateCountdownDisplay() {
    if (_nextRetryTime <= 0) return;

    const timeUntilNext = Math.ceil((_nextRetryTime - Date.now()) / 1000);
    const nextSpan = document.getElementById('ppsnr-retry-next');
    if (nextSpan) {
        nextSpan.textContent = Math.max(0, timeUntilNext);
    }
}

function stopFailedRetryPolling() {
    if (_retryTimeoutId !== null) {
        clearTimeout(_retryTimeoutId);
        _retryTimeoutId = null;
    }
    if (_countdownIntervalId !== null) {
        clearInterval(_countdownIntervalId);
        _countdownIntervalId = null;
    }
    const counter = document.getElementById('ppsnr-retry-counter');
    if (counter) counter.style.display = 'none';
}

async function attemptReconnect() {
    try {
        console.debug('[ReconnectModal] attempting Blazor.reconnect()');
        const successful = await Blazor.reconnect();

        if (successful) {
            console.debug('[ReconnectModal] Blazor.reconnect() succeeded');
            stopFailedRetryPolling();
            return;
        }

        // Reconnect returned false - circuit may be lost, try resume
        console.debug('[ReconnectModal] Blazor.reconnect() returned false, attempting resumeCircuit()');
        const resumeSuccessful = await Blazor.resumeCircuit();

        if (resumeSuccessful) {
            console.debug('[ReconnectModal] Blazor.resumeCircuit() succeeded');
            stopFailedRetryPolling();
            return;
        }

        // Resume failed - check if server is reachable and reload
        console.debug('[ReconnectModal] resumeCircuit failed, probing server');
        if (await isServerReachable()) {
            console.debug('[ReconnectModal] server is reachable, reloading page for fresh circuit');
            location.reload();
        }
    } catch (err) {
        console.debug('[ReconnectModal] reconnect attempt threw:', err && err.message);
        // Error indicates server unreachable - poller will retry
    }
}

async function isServerReachable() {
    try {
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), 3000);

        try {
            const res = await fetch(window.location.origin + '/', {
                method: 'GET',
                signal: controller.signal,
                cache: 'no-store'
            });
            clearTimeout(timeoutId);
            const isOk = res && res.ok;
            console.debug('[ReconnectModal] server probe:', isOk ? 'reachable (200)' : `unreachable (${res.status})`);
            return isOk;
        } catch (e) {
            clearTimeout(timeoutId);
            console.debug('[ReconnectModal] server probe failed:', e && e.message);
            return false;
        }
    } catch (e) {
        console.debug('[ReconnectModal] server probe error:', e && e.message);
        return false;
    }
}

// ============================================================================
// AdvancedRetryPolicy - Exponential Backoff with Circuit Breaker
// ============================================================================

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
            console.debug('[ReconnectModal] circuit breaker activated for', Math.ceil(this.circuitBreakerCooldown / 1000), 'seconds');
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

// Create global policy instance
const _retryPolicy = new PPSNRRetryPolicy();

