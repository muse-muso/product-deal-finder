(function () {
  const DEBUG = false;
  const log = (...args) => { if (DEBUG) console.log('[PDF popup]', ...args); };

  const trackedList = document.getElementById('trackedList');
  const emptyList = document.getElementById('emptyList');
  const thresholdInput = document.getElementById('threshold');
  const trackPageBtn = document.getElementById('trackPageBtn');
  const trackStatus = document.getElementById('trackStatus');

  function setStatus(text, isError) {
    trackStatus.textContent = text || '';
    trackStatus.classList.toggle('error', !!isError);
  }

  function formatPrice(currency, value) {
    return value != null ? `${currency} ${Number(value).toFixed(2)}` : '—';
  }

  function collapseAllExcept(li) {
    const items = trackedList.querySelectorAll('li.expanded');
    items.forEach(el => {
      if (el !== li) el.classList.remove('expanded');
    });
  }

  async function loadTracked() {
    log('GET_TRACKED request');
    const items = await browser.runtime.sendMessage({ type: 'GET_TRACKED' });
    log('GET_TRACKED response', items?.length ?? 0, 'items');
    trackedList.innerHTML = '';
    emptyList.style.display = items.length ? 'none' : 'block';
    for (const item of items) {
      const li = document.createElement('li');
      li.dataset.id = item.id;

      // Compact summary (clickable)
      const summary = document.createElement('button');
      summary.type = 'button';
      summary.className = 'tracked-item-summary';
      summary.setAttribute('aria-expanded', 'false');
      const summaryContent = document.createElement('div');
      summaryContent.className = 'tracked-item-summary-content';
      const summaryName = document.createElement('div');
      summaryName.className = 'tracked-item-summary-name';
      summaryName.textContent = item.productName || item.url;
      const summaryMeta = document.createElement('div');
      summaryMeta.className = 'tracked-item-summary-meta';
      const lastPrice = formatPrice(item.currency, item.lastPrice);
      const targetLabel = item.threshold <= 0 ? 'Set target' : `${item.currency} ${item.threshold}`;
      summaryMeta.textContent = `${item.retailerName || 'Product'} · Last: ${lastPrice} → Target: ${targetLabel}`;
      summaryContent.appendChild(summaryName);
      summaryContent.appendChild(summaryMeta);
      const chevron = document.createElement('span');
      chevron.className = 'tracked-item-summary-chevron';
      chevron.setAttribute('aria-hidden', 'true');
      chevron.innerHTML = '<svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor" xmlns="http://www.w3.org/2000/svg"><path d="M6 4l4 4-4 4V4z"/></svg>';
      summary.appendChild(summaryContent);
      summary.appendChild(chevron);

      summary.addEventListener('click', () => {
        const isExpanded = li.classList.toggle('expanded');
        summary.setAttribute('aria-expanded', isExpanded);
        if (isExpanded) collapseAllExcept(li);
      });

      li.appendChild(summary);

      // Expandable details panel
      const details = document.createElement('div');
      details.className = 'tracked-item-details';

      const name = document.createElement('div');
      name.className = 'item-name';
      name.textContent = item.productName || item.url;
      const meta = document.createElement('div');
      meta.className = 'item-meta';
      meta.textContent = `${item.retailerName || 'Product'} · Last: ${lastPrice} · Target: ${targetLabel}`;
      details.appendChild(name);
      details.appendChild(meta);

      const history = (item.priceHistory || []).slice(-5);
      if (history.length > 0) {
        const historyEl = document.createElement('div');
        historyEl.className = 'item-history';
        historyEl.textContent = 'History: ' + history.map(h => formatPrice(item.currency, h.price)).join(' → ');
        details.appendChild(historyEl);
      }

      const row = document.createElement('div');
      row.className = 'item-target-row';
      const targetInput = document.createElement('input');
      targetInput.type = 'number';
      targetInput.min = '0';
      targetInput.step = '0.01';
      targetInput.placeholder = 'Target price';
      targetInput.value = item.threshold > 0 ? item.threshold : '';
      targetInput.className = 'target-input';
      const setTargetBtn = document.createElement('button');
      setTargetBtn.className = 'btn small primary';
      setTargetBtn.textContent = item.threshold > 0 ? 'Update target' : 'Set target';
      setTargetBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        const v = parseFloat(targetInput.value);
        if (!Number.isFinite(v) || v <= 0) return;
        browser.runtime.sendMessage({ type: 'UPDATE_TRACKED', id: item.id, payload: { threshold: v } }).then(() => loadTracked());
      });
      row.appendChild(targetInput);
      row.appendChild(setTargetBtn);
      details.appendChild(row);

      const actions = document.createElement('div');
      actions.className = 'item-actions';
      const openBtn = document.createElement('button');
      openBtn.type = 'button';
      openBtn.className = 'btn small link';
      openBtn.textContent = 'Open';
      openBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        // Only open https URLs on known retailer domains (matches background.js ALLOWED_HOSTS).
        if (/^https:\/\//.test(item.url)) {
          browser.tabs.create({ url: item.url });
        }
      });
      const removeBtn = document.createElement('button');
      removeBtn.type = 'button';
      removeBtn.className = 'btn small danger';
      removeBtn.textContent = 'Remove';
      removeBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        browser.runtime.sendMessage({ type: 'REMOVE_TRACKED', id: item.id }).then(() => loadTracked());
      });
      actions.appendChild(openBtn);
      actions.appendChild(removeBtn);
      details.appendChild(actions);

      li.appendChild(details);
      trackedList.appendChild(li);
    }
  }

  trackPageBtn.addEventListener('click', async () => {
    log('Track page clicked');
    const threshold = parseFloat(thresholdInput.value);
    if (!Number.isFinite(threshold) || threshold <= 0) {
      setStatus('Enter a valid target price.', true);
      return;
    }
    setStatus('…');
    try {
      const [tab] = await browser.tabs.query({ active: true, currentWindow: true });
      log('Active tab', tab ? { id: tab.id, url: tab.url } : null);
      if (!tab || !tab.id) {
        setStatus('Could not get current tab.', true);
        return;
      }
      let payload;
      try {
        payload = await browser.tabs.sendMessage(tab.id, { type: 'GET_PAGE_DATA' });
        log('GET_PAGE_DATA response', payload);
      } catch (e) {
        log('GET_PAGE_DATA error', e);
        setStatus('Not a supported product page. Open a product page on JB Hi-Fi, Officeworks, Amazon AU, The Good Guys, or Harvey Norman.', true);
        return;
      }
      if (!payload || !payload.url) {
        setStatus('Could not read page data.', true);
        return;
      }
      log('Sending ADD_TRACKED', { url: payload.url, threshold });
      const result = await browser.runtime.sendMessage({
        type: 'ADD_TRACKED',
        payload: {
          url: payload.url,
          threshold,
          currency: 'AUD',
          productName: payload.productName,
          retailerName: payload.retailerName
        }
      });
      if (result && result.ok) {
        setStatus("Added. You'll get a notification when the price is at or below " + threshold + " AUD.");
        thresholdInput.value = '';
        loadTracked();
      } else if (result && result.reason === 'already_tracked') {
        setStatus('This page is already tracked.', true);
      } else {
        setStatus('Failed to add.', true);
      }
    } catch (e) {
      log('Track page error', e);
      setStatus('Error: ' + (e.message || 'unknown'), true);
    }
  });

  const settingsBtn = document.getElementById('settingsBtn');
  if (settingsBtn) {
    log('Settings button found, attaching listener');
    settingsBtn.addEventListener('click', () => {
      log('Settings clicked, opening options page');
      browser.runtime.openOptionsPage();
    });
  } else {
    log('Settings button not found');
  }

  log('Loading tracked list');
  loadTracked().catch(err => {
    log('loadTracked error', err);
    setStatus('Error loading list: ' + (err.message || err), true);
  });
})();
