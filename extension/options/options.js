(function () {
  const notificationsEl = document.getElementById('notificationsEnabled');
  const currencyEl = document.getElementById('currency');
  const emailAlertEnabledEl = document.getElementById('emailAlertEnabled');
  const emailAlertToEl = document.getElementById('emailAlertTo');
  const emailViaDesktopAppEl = document.getElementById('emailViaDesktopApp');
  const desktopAppUrlEl = document.getElementById('desktopAppUrl');
  const desktopAppSecretEl = document.getElementById('desktopAppSecret');
  const saveBtn = document.getElementById('saveBtn');
  const saveStatus = document.getElementById('saveStatus');
  const autoConnectBtn = document.getElementById('autoConnectBtn');
  const autoConnectStatus = document.getElementById('autoConnectStatus');
  const exportBtn = document.getElementById('exportBtn');
  const importFile = document.getElementById('importFile');
  const importReplace = document.getElementById('importReplace');
  const importExportStatus = document.getElementById('importExportStatus');

  function setAutoConnectStatus(text, isError) {
    autoConnectStatus.textContent = text || '';
    autoConnectStatus.classList.toggle('error', !!isError);
  }

  function setImportExportStatus(text) {
    importExportStatus.textContent = text || '';
  }

  // Basic email format validation.
  function isValidEmail(value) {
    return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
  }

  browser.runtime.sendMessage({ type: 'GET_OPTIONS' }).then(opts => {
    if (opts) {
      notificationsEl.checked = opts.notificationsEnabled !== false;
      if (opts.currency) currencyEl.value = opts.currency;
      emailAlertEnabledEl.checked = opts.emailAlertEnabled === true;
      emailAlertToEl.value = opts.emailAlertTo || '';
      emailViaDesktopAppEl.checked = opts.emailViaDesktopApp === true;
      desktopAppUrlEl.value = opts.desktopAppUrl || 'http://127.0.0.1:8765';
      // desktopAppSecret comes from local storage via background.js getOptions.
      desktopAppSecretEl.value = opts.desktopAppSecret || '';
    }
  }).catch(() => {});

  saveBtn.addEventListener('click', async () => {
    // Validate email alert address if enabled.
    const emailAlertEnabled = emailAlertEnabledEl.checked;
    const emailAlertTo = emailAlertToEl.value.trim();
    if (emailAlertEnabled && emailAlertTo && !isValidEmail(emailAlertTo)) {
      saveStatus.textContent = 'Please enter a valid email address.';
      return;
    }

    const payload = {
      notificationsEnabled: notificationsEl.checked,
      currency: currencyEl.value,
      emailAlertEnabled,
      emailAlertTo,
      emailViaDesktopApp: emailViaDesktopAppEl.checked,
      desktopAppUrl: desktopAppUrlEl.value.trim() || 'http://127.0.0.1:8765',
      desktopAppSecret: desktopAppSecretEl.value.trim() || ''
    };
    const result = await browser.runtime.sendMessage({ type: 'SET_OPTIONS', payload });
    saveStatus.textContent = result && result.ok ? 'Saved.' : 'Failed to save.';
  });

  // Auto-connect: ask the background to fetch the secret from the desktop app /pair endpoint.
  if (autoConnectBtn) {
    autoConnectBtn.addEventListener('click', async () => {
      const desktopAppUrl = desktopAppUrlEl.value.trim() || 'http://127.0.0.1:8765';
      autoConnectBtn.disabled = true;
      autoConnectBtn.textContent = 'Connecting…';
      setAutoConnectStatus('');
      try {
        const result = await browser.runtime.sendMessage({ type: 'AUTO_PAIR', desktopAppUrl });
        if (result && result.ok) {
          desktopAppSecretEl.value = result.secret;
          setAutoConnectStatus('Connected! Click Save to apply.');
        } else {
          setAutoConnectStatus('Auto-connect failed: ' + (result?.error || 'Make sure the desktop app is running with "Enable extension relay" checked.'), true);
        }
      } catch (e) {
        setAutoConnectStatus('Auto-connect error: ' + (e.message || 'unknown'), true);
      } finally {
        autoConnectBtn.disabled = false;
        autoConnectBtn.textContent = 'Auto-connect';
      }
    });
  }

  exportBtn.addEventListener('click', async () => {
    const res = await browser.runtime.sendMessage({ type: 'EXPORT_TRACKED' });
    if (!res || !res.ok || !res.items) {
      setImportExportStatus('Export failed.');
      return;
    }
    const blob = new Blob([JSON.stringify(res.items, null, 2)], { type: 'application/json' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = 'product-deal-finder-tracked-' + new Date().toISOString().slice(0, 10) + '.json';
    a.click();
    URL.revokeObjectURL(a.href);
    setImportExportStatus('Exported ' + res.items.length + ' item(s).');
  });

  importFile.addEventListener('change', async (e) => {
    const file = e.target.files[0];
    if (!file) return;
    setImportExportStatus('…');
    try {
      const text = await file.text();
      const items = JSON.parse(text);
      if (!Array.isArray(items)) throw new Error('JSON must be an array');
      const result = await browser.runtime.sendMessage({
        type: 'IMPORT_TRACKED',
        items,
        replace: importReplace.checked
      });
      if (result && result.ok) {
        setImportExportStatus('Imported. Total tracked: ' + result.count + '.');
      } else {
        setImportExportStatus('Import failed.');
      }
    } catch (err) {
      setImportExportStatus('Invalid file: ' + (err.message || 'unknown'));
    }
    importFile.value = '';
  });
})();
