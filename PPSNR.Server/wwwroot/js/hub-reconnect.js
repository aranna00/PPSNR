// ============================================================================
// Hub Reconnect Helper - Automatic reconnection for SignalR hubs
// Used by View.razor and other components that need hub reconnection
// ============================================================================

window.PPSNR = window.PPSNR || {};

/**
 * Gets or creates the retry policy for hub reconnection
 * @returns {PPSNRRetryPolicy} The retry policy instance
 */
window.PPSNR.getHubRetryPolicy = function() {
    if (!window.PPSNR._hubRetryPolicy) {
        // Ensure createRetryPolicy is available
        if (window.PPSNR.createRetryPolicy) {
            window.PPSNR._hubRetryPolicy = window.PPSNR.createRetryPolicy();
            console.debug('[PPSNR.HubReconnect] Created new hub retry policy');
        } else {
            console.warn('[PPSNR.HubReconnect] retry-policy.js not loaded, creating fallback policy');
            // Create a minimal fallback
            window.PPSNR._hubRetryPolicy = {
                retryCount: 0,
                failures: [],
                getNextRetryDelay: function() {
                    // Simple exponential backoff: 500ms, 1s, 2s, 4s, 8s, 16s, then stop
                    if (this.retryCount >= 6) return null;
                    const delay = Math.min(500 * Math.pow(2, this.retryCount), 30000);
                    this.retryCount++;
                    return Math.round(delay + (Math.random() * 0.4 - 0.2) * delay); // Add jitter
                },
                reset: function() {
                    this.retryCount = 0;
                    this.failures = [];
                }
            };
        }
    }
    return window.PPSNR._hubRetryPolicy;
};

/**
 * Helper method for C# interop to get next retry delay
 * @returns {number|null} The delay in milliseconds or null if exhausted
 */
window.PPSNR.getNextRetryDelayHelper = function() {
    const policy = window.PPSNR.getHubRetryPolicy();
    return policy.getNextRetryDelay();
};

/**
 * Helper method for C# interop to reset the policy
 */
window.PPSNR.resetRetryPolicy = function() {
    const policy = window.PPSNR.getHubRetryPolicy();
    policy.reset();
    console.debug('[PPSNR.HubReconnect] Retry policy reset');
};

/**
 * Dispatches a Blazor reconnect modal state change so we can reuse the built-in UI
 */
window.PPSNR.triggerReconnectModalState = function(state, extraDetail = {}) {
    const modal = document.getElementById("components-reconnect-modal");
    if (!modal) {
        console.debug('[PPSNR.HubReconnect] triggerReconnectModalState skipped (modal missing)');
        return;
    }

    const stateClasses = [
        'components-reconnect-show',
        'components-reconnect-retrying',
        'components-reconnect-failed',
        'components-reconnect-resume-failed',
        'components-reconnect-paused'
    ];
    modal.classList.remove(...stateClasses);

    if (!window.PPSNR._manualReconnectOverlayState) {
        window.PPSNR._manualReconnectOverlayState = { forced: false };
    }

    switch (state) {
        case 'show':
            modal.classList.add('components-reconnect-show');
            break;
        case 'retrying':
            modal.classList.add('components-reconnect-retrying');
            break;
        case 'failed':
            modal.classList.add('components-reconnect-failed');
            break;
        case 'resume-failed':
            modal.classList.add('components-reconnect-resume-failed');
            break;
        case 'paused':
            modal.classList.add('components-reconnect-paused');
            break;
        default:
            break;
    }

    try {
        modal.dispatchEvent(new CustomEvent('components-reconnect-state-changed', {
            bubbles: true,
            detail: { state, ...extraDetail }
        }));
    } catch (err) {
        console.warn('[PPSNR.HubReconnect] Failed to dispatch reconnect state:', err);
    }
};

/**
 * Shows the reconnect modal manually (for hub disconnects, not Blazor circuit failures)
 */
window.showReconnectModal = function() {
    window.PPSNR.triggerReconnectModalState('show');
};

/**
 * Waits for the Blazor reconnect UI to finish
 * @param {number} maxWaitMs - Maximum time to wait (default 120000ms = 2 minutes)
 * @returns {Promise<boolean>} true if waiting completed, false if timeout
 */
window.PPSNR.waitForBlazorReconnect = async function(maxWaitMs = 120000) {
    const startTime = Date.now();
    while (Date.now() - startTime < maxWaitMs) {
        try {
            const isReconnecting = window.PPSNR.isReconnecting?.();
            if (!isReconnecting) {
                return true;
            }
        } catch (e) {
            // Blazor interop might fail during transition
        }
        await new Promise(resolve => setTimeout(resolve, 1000));
    }
    return false;
};

/**
 * Releases the forced state of the reconnect overlay, allowing it to close
 */
window.PPSNR.releaseReconnectOverlay = function() {
    if (window.PPSNR._manualReconnectOverlayState) {
        window.PPSNR._manualReconnectOverlayState.forced = false;
    }
};

window.PPSNR.suppressReconnectModal = false;

/**
 * Checks if the server is reachable by polling the root URL
 * @param {string} [origin=window.location.origin] - The origin to check
 * @returns {Promise<boolean>} true if reachable, false if not
 */
window.PPSNR.isServerReachable = async function(origin = window.location.origin) {
    try {
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), 3000);
        try {
            const res = await fetch(origin + '/', {
                method: 'GET',
                signal: controller.signal,
                cache: 'no-store'
            });
            clearTimeout(timeoutId);
            return res && res.ok;
        } catch (err) {
            clearTimeout(timeoutId);
            return false;
        }
    } catch {
        return false;
    }
};

/**
 * Explicitly sets the forced state of the reconnect overlay
 * @param {boolean} forced - True to force the overlay to show, false to allow it to hide
 */
window.PPSNR.setReconnectOverlayForced = function(forced) {
    window.PPSNR._manualReconnectOverlayState = window.PPSNR._manualReconnectOverlayState || { forced: false };
    window.PPSNR._manualReconnectOverlayState.forced = !!forced;
    if (!forced) {
        // allow modal to hide if it was waiting
        try {
            const modal = document.getElementById("components-reconnect-modal");
            if (modal && modal.open) {
                modal.close();
            }
        } catch { /* ignore */ }
    }
};

console.debug('[PPSNR.HubReconnect] Helper loaded');
