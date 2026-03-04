(function () {
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

  async function loadTracked() {
    const items = await browser.runtime.sendMessage({ type: 'GET_TRACKED' });
    trackedList.innerHTML = '';
    emptyList.style.display = items.length ? 'none' : 'block';
    for (const item of items) {
      const li = document.createElement('li');
      const name = document.createElement('div');
      name.className = 'item-name';
      name.textContent = item.productName || item.url;
      const meta = document.createElement('div');
      meta.className = 'item-meta';
      const lastPrice = formatPrice(item.currency, item.lastPrice);
      const targetLabel = item.threshold <= 0 ? 'Set target' : `${item.currency} ${item.threshold}`;
      meta.textContent = `${item.retailerName || 'Product'} · Last: ${lastPrice} · Target: ${targetLabel}`;
      const history = (item.priceHistory || []).slice(-5);
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
      setTargetBtn.className = 'btn small';
      setTargetBtn.textContent = item.threshold > 0 ? 'Update target' : 'Set target';
      setTargetBtn.addEventListener('click', async () => {
        const v = parseFloat(targetInput.value);
        if (!Number.isFinite(v) || v <= 0) return;
        await browser.runtime.sendMessage({ type: 'UPDATE_TRACKED', id: item.id, payload: { threshold: v } });
        loadTracked();
      });
      row.appendChild(targetInput);
      row.appendChild(setTargetBtn);
      const actions = document.createElement('div');
      actions.className = 'item-actions';
      const openBtn = document.createElement('button');
      openBtn.className = 'btn small link';
      openBtn.textContent = 'Open';
      openBtn.addEventListener('click', () => browser.tabs.create({ url: item.url }));
      const removeBtn = document.createElement('button');
      removeBtn.className = 'btn small danger';
      removeBtn.textContent = 'Remove';
      removeBtn.addEventListener('click', async () => {
        await browser.runtime.sendMessage({ type: 'REMOVE_TRACKED', id: item.id });
        loadTracked();
      });
      actions.appendChild(openBtn);
      actions.appendChild(removeBtn);
      li.appendChild(name);
      li.appendChild(meta);
      if (history.length > 0) {
        const historyEl = document.createElement('div');
        historyEl.className = 'item-history';
        historyEl.textContent = 'History: ' + history.map(h => formatPrice(item.currency, h.price)).join(' → ');
        li.appendChild(historyEl);
      }
      li.appendChild(row);
      li.appendChild(actions);
      trackedList.appendChild(li);
    }
  }

  trackPageBtn.addEventListener('click', async () => {
    const threshold = parseFloat(thresholdInput.value);
    if (!Number.isFinite(threshold) || threshold <= 0) {
      setStatus('Enter a valid target price.', true);
      return;
    }
    setStatus('…');
    try {
      const [tab] = await browser.tabs.query({ active: true, currentWindow: true });
      if (!tab || !tab.id) {
        setStatus('Could not get current tab.', true);
        return;
      }
      let payload;
      try {
        payload = await browser.tabs.sendMessage(tab.id, { type: 'GET_PAGE_DATA' });
      } catch (e) {
        setStatus('Not a supported product page. Open a product page on JB Hi-Fi, Officeworks, Amazon AU, The Good Guys, or Harvey Norman.', true);
        return;
      }
      if (!payload || !payload.url) {
        setStatus('Could not read page data.', true);
        return;
      }
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
        setStatus('Added. You’ll get a notification when the price is at or below ' + threshold + ' AUD.');
        thresholdInput.value = '';
        loadTracked();
      } else if (result && result.reason === 'already_tracked') {
        setStatus('This page is already tracked.', true);
      } else {
        setStatus('Failed to add.', true);
      }
    } catch (e) {
      setStatus('Error: ' + (e.message || 'unknown'), true);
    }
  });

  loadTracked();
})();
