(function () {
  const notificationsEl = document.getElementById('notificationsEnabled');
  const currencyEl = document.getElementById('currency');
  const saveBtn = document.getElementById('saveBtn');
  const saveStatus = document.getElementById('saveStatus');

  browser.runtime.sendMessage({ type: 'GET_OPTIONS' }).then(opts => {
    if (opts) {
      notificationsEl.checked = opts.notificationsEnabled !== false;
      if (opts.currency) currencyEl.value = opts.currency;
    }
  }).catch(() => {});

  saveBtn.addEventListener('click', async () => {
    const payload = {
      notificationsEnabled: notificationsEl.checked,
      currency: currencyEl.value
    };
    const result = await browser.runtime.sendMessage({ type: 'SET_OPTIONS', payload });
    saveStatus.textContent = result && result.ok ? 'Saved.' : 'Failed to save.';
  });
})();
