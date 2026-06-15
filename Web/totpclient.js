(function () {
  'use strict';

  const injectedAttribute = 'data-jellyfin-totp-injected';

  async function promptCode() {
    return window.prompt('Enter your six-digit authenticator code');
  }

  function createButton(text, className) {
    const button = document.createElement('button');
    button.type = 'button';
    button.setAttribute('is', 'emby-button');
    button.className = className || 'raised button-submit block';
    const span = document.createElement('span');
    span.textContent = text;
    button.appendChild(span);
    return button;
  }

  function createSection(id) {
    const section = document.createElement('div');
    section.id = id;
    section.className = 'verticalSection';
    section.setAttribute(injectedAttribute, 'true');
    return section;
  }

  function getCurrentUserId() {
    if (ApiClient.getCurrentUserId) return ApiClient.getCurrentUserId();
    const credentialProvider = ApiClient._serverInfo || ApiClient.serverInfo;
    return credentialProvider && credentialProvider.UserId;
  }

  function getRouteUserId() {
    const query = new URLSearchParams(window.location.search);
    return query.get('userId') || query.get('id') || query.get('user') || getCurrentUserId();
  }

  function findTextElement(text) {
    const candidates = document.querySelectorAll('label, .fieldDescription, .selectLabel, h2, h3, div');
    return Array.prototype.find.call(candidates, function (element) {
      return (element.textContent || '').trim() === text;
    });
  }

  function findSettingsProfileAnchor() {
    const passwordButton = Array.prototype.find.call(document.querySelectorAll('button, .emby-button'), function (button) {
      return /save password/i.test(button.textContent || '');
    });

    if (!passwordButton) return null;
    return passwordButton.closest('.verticalSection, form, .sectionContent') || passwordButton.parentElement;
  }

  function findAdminUserAnchor() {
    const passwordResetLabel = findTextElement('Password Reset Provider');
    if (!passwordResetLabel) return null;

    const container = passwordResetLabel.closest('.selectContainer, .inputContainer, .fieldDescriptionContainer') || passwordResetLabel.parentElement;
    const description = container && container.querySelector('.fieldDescription');
    return (description || container);
  }

  async function setupTotp(userId) {
    const result = await ApiClient.ajax({ type: 'POST', url: ApiClient.getUrl('Totp/Setup/' + userId) });
    alert('Add this secret to your authenticator app: ' + result.secret + '\n\nURI: ' + result.uri);
  }

  async function confirmTotp(userId) {
    const code = await promptCode();
    if (!code) return;

    await ApiClient.ajax({
      type: 'POST',
      url: ApiClient.getUrl('Totp/Confirm/' + userId),
      data: JSON.stringify({ code: code }),
      contentType: 'application/json'
    });
    alert('TOTP enabled.');
  }

  async function resetTotp(userId) {
    if (!confirm('Reset this user\'s TOTP secret?')) return;

    await ApiClient.ajax({ type: 'DELETE', url: ApiClient.getUrl('Totp/Reset/' + userId) });
    alert('TOTP has been reset for this user.');
  }

  function injectProfileSetup() {
    if (document.getElementById('jellyfinTotpProfileSetup')) return;

    const anchor = findSettingsProfileAnchor();
    const userId = getCurrentUserId();
    if (!anchor || !userId) return;

    const section = createSection('jellyfinTotpProfileSetup');
    section.innerHTML = '<h2>TOTP Two-Factor Authentication</h2><p class="fieldDescription">Configure an authenticator app to protect your account with a six-digit verification code.</p>';

    const setupButton = createButton('Set up TOTP', 'raised button-submit block');
    setupButton.addEventListener('click', async function () { await setupTotp(userId); });
    section.appendChild(setupButton);

    const confirmButton = createButton('Confirm TOTP Code', 'raised block');
    confirmButton.addEventListener('click', async function () { await confirmTotp(userId); });
    section.appendChild(confirmButton);

    anchor.insertAdjacentElement('afterend', section);
  }

  function injectAdminReset() {
    if (document.getElementById('jellyfinTotpAdminReset')) return;

    const anchor = findAdminUserAnchor();
    const userId = getRouteUserId();
    if (!anchor || !userId) return;

    const section = createSection('jellyfinTotpAdminReset');
    const button = createButton('Reset TOTP for this user', 'raised button-cancel block');
    button.addEventListener('click', async function () { await resetTotp(userId); });
    section.appendChild(button);

    anchor.insertAdjacentElement('afterend', section);
  }

  function injectTotpUi() {
    injectProfileSetup();
    injectAdminReset();
  }

  const originalFetch = window.fetch;
  window.fetch = async function (input, init) {
    const url = typeof input === 'string' ? input : input && input.url;
    let response = await originalFetch.apply(this, arguments);
    if (url && /\/Users\/AuthenticateByName/i.test(url) && (response.status === 401 || response.status === 403)) {
      let payload = null;
      try { payload = await response.clone().json(); } catch (_) { }
      if (payload && payload.Error === 'TwoFactorRequired') {
        const code = await promptCode();
        if (!code) return response;
        init = init || {};
        init.headers = new Headers(init.headers || (input && input.headers) || {});
        init.headers.set('X-Jellyfin-TOTP', code);
        response = await originalFetch(input, init);
      } else if (payload && payload.Error === 'TwoFactorSetupRequired') {
        alert('Two-factor authentication is required. Open Settings → Profile to configure your authenticator app, then sign in again.');
      }
    }
    return response;
  };

  window.JellyfinTotp = {
    setup: setupTotp,
    confirm: confirmTotp,
    reset: resetTotp,
    inject: injectTotpUi
  };

  document.addEventListener('viewshow', injectTotpUi);
  document.addEventListener('pageshow', injectTotpUi);
  document.addEventListener('DOMContentLoaded', injectTotpUi);
  new MutationObserver(injectTotpUi).observe(document.body || document.documentElement, { childList: true, subtree: true });
}());
