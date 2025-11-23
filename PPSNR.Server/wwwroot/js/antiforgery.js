// Helper for browser-side POSTs that include the antiforgery header and cookies.
// Exposes window.ppsnr.postWithAntiforgery(url, method = 'POST', body = null)
// Returns the response text (caller can JSON.parse if needed).
(function(){
  window.ppsnr = window.ppsnr || {};
  async function getToken() {
    const r = await fetch('/api/antiforgery/token', {
      method: 'GET',
      credentials: 'include', // ensure antiforgery cookie is stored/sent
      headers: { 'Accept': 'application/json' }
    });
    if (!r.ok) throw new Error('Failed to obtain antiforgery token');
    return await r.json(); // { token, headerName }
  }

  window.ppsnr.postWithAntiforgery = async function(url, method = 'POST', body = null) {
    const { token, headerName } = await getToken();
    const headers = { 'Accept': 'application/json' };
    if (body != null) {
      headers['Content-Type'] = 'application/json';
    }
    headers[headerName || 'RequestVerificationToken'] = token;

    const r = await fetch(url, {
      method,
      credentials: 'include', // send cookies
      headers,
      body: body
    });
    // If unauthorized, redirect to login preserving the current location as returnUrl
    if (r.status === 401) {
      try {
        const returnUrl = encodeURIComponent(window.location.pathname + window.location.search + window.location.hash);
        window.location.assign(`/auth/login?returnUrl=${returnUrl}`);
      } catch (_) { /* no-op */ }
      throw new Error('Unauthorized');
    }
    const text = await r.text();
    if (!r.ok) {
      throw new Error(text || r.statusText);
    }
    return text;
  }
})();
