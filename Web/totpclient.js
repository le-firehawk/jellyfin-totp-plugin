(function () {
  'use strict';

  const injectedAttribute = 'data-jellyfin-totp-injected';
  const setupState = {};

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
    section.className = 'verticalSection verticalSection-extrabottompadding';
    section.setAttribute(injectedAttribute, 'true');
    return section;
  }

  function currentViewText() {
    return ((document.querySelector('.page:not(.hide), .page:not(.hidden), [data-role="page"]') || document.body).textContent || '').toLowerCase();
  }

  function getCurrentUserId() {
    if (window.ApiClient && ApiClient.getCurrentUserId) return ApiClient.getCurrentUserId();
    const serverInfo = window.ApiClient && (ApiClient._serverInfo || ApiClient.serverInfo);
    return serverInfo && serverInfo.UserId;
  }

  function getRouteUserId() {
    const hashQuery = (window.location.hash.split('?')[1] || '').split('#')[0];
    const query = new URLSearchParams(window.location.search || hashQuery);
    return query.get('userId') || query.get('id') || query.get('user') || getCurrentUserId();
  }

  function findTextElement(pattern) {
    const candidates = document.querySelectorAll('label, .fieldDescription, .selectLabel, h1, h2, h3, h4, div, span');
    return Array.prototype.find.call(candidates, function (element) {
      return pattern.test((element.textContent || '').trim());
    });
  }

  function findSettingsProfileAnchor() {
    const text = currentViewText();
    if (!/profile|password|tfa|two-factor|two factor|authentication/i.test(text)) return null;

    const tfaHeading = findTextElement(/^(tfa|two-factor authentication|two factor authentication)$/i);
    if (tfaHeading) return tfaHeading.closest('.verticalSection, form, .sectionContent, [data-role="content"]') || tfaHeading.parentElement;

    const passwordButton = Array.prototype.find.call(document.querySelectorAll('button, .emby-button'), function (button) {
      return /save password|change password/i.test(button.textContent || '');
    });

    if (passwordButton) return passwordButton.closest('.verticalSection, form, .sectionContent') || passwordButton.parentElement;

    const profileHeading = findTextElement(/^profile$/i);
    return profileHeading && (profileHeading.closest('.verticalSection, form, .sectionContent, [data-role="content"]') || profileHeading.parentElement);
  }

  function findAdminUserAnchor() {
    const text = currentViewText();
    if (!/password reset provider|reset password|policy|user/i.test(text)) return null;

    const passwordResetLabel = findTextElement(/password reset provider/i) || findTextElement(/reset password/i);
    if (!passwordResetLabel) return null;

    const container = passwordResetLabel.closest('.selectContainer, .inputContainer, .fieldDescriptionContainer, .verticalSection') || passwordResetLabel.parentElement;
    const description = container && container.querySelector('.fieldDescription');
    return (description || container);
  }

  async function apiRequest(method, path, data) {
    return ApiClient.ajax({
      type: method,
      url: ApiClient.getUrl(path),
      data: data ? JSON.stringify(data) : undefined,
      contentType: data ? 'application/json' : undefined,
      headers: { Accept: 'application/json' }
    });
  }

  async function getStatus(userId) {
    return apiRequest('GET', 'Totp/Status/' + userId);
  }

  async function setupTotp(userId) {
    return apiRequest('POST', 'Totp/Setup/' + userId);
  }

  async function confirmTotp(userId, code) {
    return apiRequest('POST', 'Totp/Confirm/' + userId, { code: code });
  }

  async function resetTotp(userId) {
    if (!confirm('Reset this user\'s TOTP secret?')) return;

    await apiRequest('DELETE', 'Totp/Reset/' + userId);
    alert('TOTP has been reset for this user. The user can configure a new authenticator app from Settings → Profile → TFA.');
    const section = document.getElementById('jellyfinTotpAdminReset');
    if (section) section.querySelector('.jellyfin-totp-status').textContent = 'TOTP secret reset.';
  }

  function renderSetupDetails(container, setup) {
    setupState.secret = setup.secret;
    setupState.uri = setup.uri;
    container.innerHTML = '';

    const instructions = document.createElement('p');
    instructions.className = 'fieldDescription';
    instructions.textContent = 'Scan this URI with an authenticator app, or reveal and copy the secret key manually. Then enter the current six-digit code below.';
    container.appendChild(instructions);

    const revealButton = createButton('Reveal TOTP secret', 'raised block');
    const secret = document.createElement('pre');
    secret.className = 'fieldDescription';
    secret.style.whiteSpace = 'pre-wrap';
    secret.style.userSelect = 'all';
    secret.hidden = true;
    secret.textContent = 'Secret: ' + setup.secret + '\n\nAuthenticator URI: ' + setup.uri;
    revealButton.addEventListener('click', function () { secret.hidden = !secret.hidden; revealButton.querySelector('span').textContent = secret.hidden ? 'Reveal TOTP secret' : 'Hide TOTP secret'; });
    container.appendChild(revealButton);
    container.appendChild(secret);
  }

  function injectProfileSetup() {
    if (document.getElementById('jellyfinTotpProfileSetup') || !window.ApiClient) return;

    const anchor = findSettingsProfileAnchor();
    const userId = getCurrentUserId();
    if (!anchor || !userId) return;

    const section = createSection('jellyfinTotpProfileSetup');
    section.innerHTML = '<div class="sectionTitleContainer flex align-items-center"><h2 class="sectionTitle">TFA - Authenticator App</h2></div><p class="fieldDescription jellyfin-totp-status">Configure a time-based one-time password (TOTP) authenticator app to protect your account.</p>';

    const details = document.createElement('div');
    details.className = 'jellyfin-totp-details';
    section.appendChild(details);

    const setupButton = createButton('Set up or replace authenticator app', 'raised button-submit block');
    setupButton.addEventListener('click', async function () {
      try {
        const result = await setupTotp(userId);
        renderSetupDetails(details, result);
      } catch (err) { alert('Unable to start TOTP setup: ' + (err.message || err)); }
    });
    section.appendChild(setupButton);

    const codeContainer = document.createElement('div');
    codeContainer.className = 'inputContainer';
    codeContainer.innerHTML = '<input is="emby-input" type="text" inputmode="numeric" autocomplete="one-time-code" maxlength="6" id="jellyfinTotpCode" label="Authenticator code" /><div class="fieldDescription">Enter the six-digit code from your authenticator app to enable TFA.</div>';
    section.appendChild(codeContainer);

    const confirmButton = createButton('Enable TFA', 'raised block');
    confirmButton.addEventListener('click', async function () {
      const input = section.querySelector('#jellyfinTotpCode');
      const code = input && input.value;
      if (!code) { alert('Enter the six-digit authenticator code first.'); return; }
      try {
        await confirmTotp(userId, code);
        section.querySelector('.jellyfin-totp-status').textContent = 'TFA is enabled for this account.';
        details.innerHTML = '';
        alert('TOTP enabled.');
      } catch (err) { alert('Invalid TOTP code. Please try again.'); }
    });
    section.appendChild(confirmButton);

    getStatus(userId).then(function (status) {
      section.querySelector('.jellyfin-totp-status').textContent = status.enabled ? 'TFA is enabled for this account.' : 'TFA is not enabled for this account.';
    }).catch(function () { });

    anchor.insertAdjacentElement('afterend', section);
  }

  function injectAdminReset() {
    if (document.getElementById('jellyfinTotpAdminReset') || !window.ApiClient) return;

    const anchor = findAdminUserAnchor();
    const userId = getRouteUserId();
    if (!anchor || !userId) return;

    const section = createSection('jellyfinTotpAdminReset');
    section.innerHTML = '<div class="sectionTitleContainer flex align-items-center"><h3 class="sectionTitle">TFA - Authenticator App</h3></div><p class="fieldDescription jellyfin-totp-status">Reset this user\'s authenticator app secret if they lose access to their codes.</p>';
    const button = createButton('Reset TOTP for this user', 'raised button-cancel block');
    button.addEventListener('click', async function () { await resetTotp(userId); });
    section.appendChild(button);

    getStatus(userId).then(function (status) {
      section.querySelector('.jellyfin-totp-status').textContent = status.enabled ? 'TFA is enabled for this user.' : 'TFA is not enabled for this user.';
    }).catch(function () { });

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
        alert('Two-factor authentication is required. Open Settings → Profile → TFA to configure your authenticator app, then sign in again.');
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
  window.addEventListener('hashchange', function () { setTimeout(injectTotpUi, 100); });
  setInterval(injectTotpUi, 1000);
  new MutationObserver(injectTotpUi).observe(document.body || document.documentElement, { childList: true, subtree: true });
}());
