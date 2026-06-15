(function () {
  'use strict';
  async function promptCode() { return window.prompt('Enter your six-digit authenticator code'); }
  const originalFetch = window.fetch;
  window.fetch = async function (input, init) {
    const url = typeof input === 'string' ? input : input && input.url;
    let response = await originalFetch.apply(this, arguments);
    if (url && /\/Users\/AuthenticateByName/i.test(url) && (response.status === 401 || response.status === 403)) {
      let payload = null; try { payload = await response.clone().json(); } catch (_) { }
      if (payload && payload.Error === 'TwoFactorRequired') {
        const code = await promptCode(); if (!code) return response;
        init = init || {}; init.headers = new Headers(init.headers || (input && input.headers) || {}); init.headers.set('X-Jellyfin-TOTP', code);
        response = await originalFetch(input, init);
      } else if (payload && payload.Error === 'TwoFactorSetupRequired') {
        alert('Two-factor authentication is required. Open Settings → Profile to configure your authenticator app, then sign in again.');
      }
    }
    return response;
  };
  window.JellyfinTotp = {
    setup: async function (userId) { const r = await ApiClient.ajax({ type: 'POST', url: ApiClient.getUrl('Totp/Setup/' + userId) }); alert('Add this secret to your authenticator app: ' + r.secret + '\n\nURI: ' + r.uri); },
    confirm: async function (userId) { const code = await promptCode(); await ApiClient.ajax({ type: 'POST', url: ApiClient.getUrl('Totp/Confirm/' + userId), data: JSON.stringify({ code: code }), contentType: 'application/json' }); alert('TOTP enabled.'); },
    reset: async function (userId) { if (confirm('Reset this user\'s TOTP secret?')) await ApiClient.ajax({ type: 'DELETE', url: ApiClient.getUrl('Totp/Reset/' + userId) }); }
  };
}());
