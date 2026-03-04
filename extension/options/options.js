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
  const exportBtn = document.getElementById('exportBtn');
  const importFile = document.getElementById('importFile');
  const importReplace = document.getElementById('importReplace');
  const importExportStatus = document.getElementById('importExportStatus');

  function setImportExportStatus(text) {
    importExportStatus.textContent = text || '';
  }

  browser.runtime.sendMessage({ type: 'GET_OPTIONS' }).then(opts => {
    if (opts) {
      notificationsEl.checked = opts.notificationsEnabled !== false;
      if (opts.currency) currencyEl.value = opts.currency;
      emailAlertEnabledEl.checked = opts.emailAlertEnabled === true;
      emailAlertToEl.value = opts.emailAlertTo || '';
      emailViaDesktopAppEl.checked = opts.emailViaDesktopApp === true;
      desktopAppUrlEl.value = opts.desktopAppUrl || 'http://127.0.0.1:8765';
      desktopAppSecretEl.value = opts.desktopAppSecret || '';
    }
  }).catch(() => {});

  saveBtn.addEventListener('click', async () => {
    const payload = {
      notificationsEnabled: notificationsEl.checked,
      currency: currencyEl.value,
      emailAlertEnabled: emailAlertEnabledEl.checked,
      emailAlertTo: emailAlertToEl.value.trim(),
      emailViaDesktopApp: emailViaDesktopAppEl.checked,
      desktopAppUrl: desktopAppUrlEl.value.trim() || 'http://127.0.0.1:8765',
      desktopAppSecret: desktopAppSecretEl.value.trim() || ''
    };
    const result = await browser.runtime.sendMessage({ type: 'SET_OPTIONS', payload });
    saveStatus.textContent = result && result.ok ? 'Saved.' : 'Failed to save.';
  });

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
