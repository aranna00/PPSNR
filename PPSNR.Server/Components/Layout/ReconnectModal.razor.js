// Initialize window.PPSNR API for C# interop
window.PPSNR = window.PPSNR || {};
window.PPSNR._manualReconnectOverlayState = window.PPSNR._manualReconnectOverlayState || { forced: false };
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
                // Show generic reconnecting message and hide retry counter
                const statusMsg = document.getElementById('ppsnr-reconnect-status');
                const retryCounter = document.getElementById('ppsnr-retry-counter');
                if (statusMsg) statusMsg.style.display = 'block';
                if (retryCounter) retryCounter.style.display = 'none';
                break;
            case "hide":
                if (window.PPSNR._manualReconnectOverlayState?.forced) {
                    console.debug('[ReconnectModal] hide requested but overlay forced, ignoring');
                    return;
                }
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

    // Hide generic reconnecting message and show our custom retry counter
    const statusMsg = document.getElementById('ppsnr-reconnect-status');
    const counter = document.getElementById('ppsnr-retry-counter');
    if (statusMsg) statusMsg.style.display = 'none';
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
    // Also restore generic status message visibility
    const statusMsg = document.getElementById('ppsnr-reconnect-status');
    if (statusMsg) statusMsg.style.display = 'block';
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

// Ensure Promise.withResolvers exists for older Chromium builds (e.g., OBS)
if (typeof Promise !== 'undefined' && typeof Promise.withResolvers !== 'function') {
    Promise.withResolvers = function () {
        let resolve;
        let reject;
        const promise = new Promise((res, rej) => {
            resolve = res;
            reject = rej;
        });
        return { promise, resolve, reject };
    };
    console.debug('[ReconnectModal] Applied Promise.withResolvers polyfill');
}

// Use the shared retry policy from retry-policy.js
// Fallback creation if the module hasn't loaded yet
const _retryPolicy = (window.PPSNR?.createRetryPolicy ? window.PPSNR.createRetryPolicy() : (() => {
    console.warn('[ReconnectModal] retry-policy.js not loaded, creating local fallback');
    // Minimal fallback if the shared module hasn't loaded
    return {
        getNextRetryDelay: () => Math.min(500 * Math.pow(2, Math.min(5, this.attempt || 0)), 30000),
        reset: () => { this.attempt = 0; },
        retryCount: 0,
        attempt: 0
    };
})());
