/**
 * Background service worker: stores tracked products, compares prices from content script,
 * shows notifications when price is at or below target, updates badge.
 * Debug: In about:debugging → This Firefox → Inspect (background script) → Console; logs prefixed [PDF bg].
 */

const DEBUG = false;
const log = (...args) => { if (DEBUG) console.log('[PDF bg]', ...args); };

const STORAGE_KEY = 'productDealFinder_tracked';
const STORAGE_OPTIONS = 'productDealFinder_options';
const STORAGE_SECRET = 'productDealFinder_relaySecret'; // stored local-only, never synced
const MAX_PRICE_HISTORY = 10;

// Supported retailer hostnames — URLs must match to be tracked or imported.
const ALLOWED_HOSTS = new Set([
  'www.jbhifi.com.au',
  'jbhifi.com.au',
  'www.amazon.com.au',
  'amazon.com.au',
  'www.thegoodguys.com.au',
  'thegoodguys.com.au',
  'www.officeworks.com.au',
  'officeworks.com.au',
  'www.harveynorman.com.au',
  'harveynorman.com.au'
]);

const DEFAULT_OPTIONS = {
  notificationsEnabled: true,
  currency: 'AUD',
  emailAlertEnabled: false,
  emailAlertTo: '',
  emailViaDesktopApp: false,
  desktopAppUrl: 'http://127.0.0.1:8765',
  desktopAppSecret: '' // placeholder — actual value always loaded from local storage
};

// Returns true only for https URLs on supported retailer domains.
function isValidProductUrl(url) {
  try {
    const u = new URL(url);
    return u.protocol === 'https:' && ALLOWED_HOSTS.has(u.hostname.toLowerCase());
  } catch {
    return false;
  }
}

// Returns true only for http://127.0.0.1:<port> or http://localhost:<port>.
function isValidRelayUrl(urlStr) {
  try {
    const u = new URL(urlStr);
    if (u.protocol !== 'http:') return false;
    const host = u.hostname.toLowerCase();
    return host === '127.0.0.1' || host === 'localhost';
  } catch {
    return false;
  }
}

const notificationIdToUrl = new Map();

function generateId() {
  return Date.now().toString(36) + Math.random().toString(36).slice(2);
}

async function getTracked() {
  try {
    const o = await browser.storage.sync.get(STORAGE_KEY);
    if (o[STORAGE_KEY] && Array.isArray(o[STORAGE_KEY])) return ensurePriceHistory(o[STORAGE_KEY]);
  } catch (_) {}
  const o = await browser.storage.local.get(STORAGE_KEY);
  const items = o[STORAGE_KEY] || [];
  if (items.length > 0) {
    try {
      await browser.storage.sync.set({ [STORAGE_KEY]: items });
    } catch (_) {}
  }
  return ensurePriceHistory(items);
}

function ensurePriceHistory(items) {
  return items.map(i => ({ ...i, priceHistory: i.priceHistory || [] }));
}

async function setTracked(items) {
  const normalized = ensurePriceHistory(items);
  try {
    await browser.storage.sync.set({ [STORAGE_KEY]: normalized });
    return;
  } catch (_) {}
  await browser.storage.local.set({ [STORAGE_KEY]: normalized });
}

// Options are stored in sync storage EXCEPT desktopAppSecret, which is local-only.
async function getOptions() {
  let opts = { ...DEFAULT_OPTIONS };
  try {
    const o = await browser.storage.sync.get(STORAGE_OPTIONS);
    if (o[STORAGE_OPTIONS]) opts = { ...opts, ...o[STORAGE_OPTIONS] };
  } catch (_) {
    const o = await browser.storage.local.get(STORAGE_OPTIONS);
    if (o[STORAGE_OPTIONS]) opts = { ...opts, ...o[STORAGE_OPTIONS] };
  }
  // Secret is always loaded from local storage only (never synced to cloud).
  const localSecretStore = await browser.storage.local.get(STORAGE_SECRET);
  opts.desktopAppSecret = localSecretStore[STORAGE_SECRET] || '';
  return opts;
}

async function setOptions(opts) {
  const { desktopAppSecret, ...syncableOpts } = opts;
  // Persist secret locally only.
  await browser.storage.local.set({ [STORAGE_SECRET]: desktopAppSecret || '' });
  // Persist all other options to sync.
  try {
    await browser.storage.sync.set({ [STORAGE_OPTIONS]: syncableOpts });
    return;
  } catch (_) {}
  await browser.storage.local.set({ [STORAGE_OPTIONS]: syncableOpts });
}

function updateBadge(count) {
  if (count === 0) {
    browser.action.setBadgeText({ text: '' });
  } else {
    browser.action.setBadgeText({ text: String(count) });
    browser.action.setBadgeBackgroundColor({ color: '#0071e3' });
  }
}

async function refreshBadge() {
  const items = await getTracked();
  updateBadge(items.length);
}

browser.runtime.onInstalled.addListener(() => {
  refreshBadge();
});

browser.runtime.onStartup.addListener(() => {
  refreshBadge();
});

browser.contextMenus.create({
  id: 'track-this-product',
  title: 'Track this product with Product Deal Finder',
  contexts: ['page'],
  documentUrlPatterns: [
    'https://www.jbhifi.com.au/*',
    'https://www.amazon.com.au/*',
    'https://www.thegoodguys.com.au/*',
    'https://www.officeworks.com.au/*',
    'https://www.harveynorman.com.au/*'
  ]
});

browser.contextMenus.onClicked.addListener(async (info, tab) => {
  if (info.menuItemId !== 'track-this-product' || !tab || !tab.id) return;
  try {
    const payload = await browser.tabs.sendMessage(tab.id, { type: 'GET_PAGE_DATA' });
    if (!payload || !payload.url) return;
    const result = await addTracked({
      url: payload.url,
      threshold: 0,
      currency: 'AUD',
      productName: payload.productName,
      retailerName: payload.retailerName
    });
    if (result && result.ok) {
      await browser.notifications.create({
        type: 'basic',
        title: 'Product Deal Finder',
        message: 'Product added. Open the extension and set your target price.'
      });
    }
  } catch (_) {}
});

browser.notifications.onClicked.addListener(notificationId => {
  const url = notificationIdToUrl.get(notificationId);
  if (url && isValidProductUrl(url)) browser.tabs.create({ url });
});

browser.runtime.onMessage.addListener((message, _sender, sendResponse) => {
  log('message', message.type, message.type === 'ADD_TRACKED' ? message.payload?.url : '');
  if (message.type === 'PAGE_DATA') {
    handlePageData(message.payload).then(sendResponse).catch(() => sendResponse({ ok: false }));
    return true;
  }
  if (message.type === 'GET_TRACKED') {
    getTracked().then(sendResponse).catch(() => sendResponse([]));
    return true;
  }
  if (message.type === 'ADD_TRACKED') {
    addTracked(message.payload).then(sendResponse).catch(() => sendResponse({ ok: false }));
    return true;
  }
  if (message.type === 'REMOVE_TRACKED') {
    removeTracked(message.id).then(sendResponse).catch(() => sendResponse({ ok: false }));
    return true;
  }
  if (message.type === 'GET_OPTIONS') {
    getOptions().then(sendResponse).catch(() => sendResponse(DEFAULT_OPTIONS));
    return true;
  }
  if (message.type === 'SET_OPTIONS') {
    setOptions(message.payload).then(() => sendResponse({ ok: true })).catch(() => sendResponse({ ok: false }));
    return true;
  }
  if (message.type === 'UPDATE_TRACKED') {
    updateTracked(message.id, message.payload).then(sendResponse).catch(() => sendResponse({ ok: false }));
    return true;
  }
  if (message.type === 'EXPORT_TRACKED') {
    getTracked().then(items => sendResponse({ ok: true, items })).catch(() => sendResponse({ ok: false, items: [] }));
    return true;
  }
  if (message.type === 'IMPORT_TRACKED') {
    importTracked(message.items, message.replace).then(sendResponse).catch(() => sendResponse({ ok: false }));
    return true;
  }
  if (message.type === 'AUTO_PAIR') {
    autoPair(message.desktopAppUrl).then(sendResponse).catch(() => sendResponse({ ok: false, error: 'Unexpected error' }));
    return true;
  }
  sendResponse({ ok: false });
  return false;
});

async function addTracked({ url, threshold, currency, productName, retailerName }) {
  if (!isValidProductUrl(url)) {
    return { ok: false, reason: 'unsupported_url' };
  }
  const items = await getTracked();
  if (items.some(i => i.url === url)) return { ok: false, reason: 'already_tracked' };
  const entry = {
    id: generateId(),
    url,
    threshold: Number(threshold),
    currency: currency || 'AUD',
    productName: productName || url,
    retailerName: retailerName || '',
    addedAt: Date.now(),
    lastPrice: null,
    lastCheckedAt: null,
    priceHistory: []
  };
  items.push(entry);
  await setTracked(items);
  await refreshBadge();
  return { ok: true, id: entry.id };
}

async function updateTracked(id, payload) {
  const items = await getTracked();
  const entry = items.find(i => i.id === id);
  if (!entry) return { ok: false };
  if (payload.threshold !== undefined) entry.threshold = Number(payload.threshold);
  if (payload.currency !== undefined) entry.currency = payload.currency;
  await setTracked(items);
  return { ok: true };
}

async function importTracked(importedItems, replace) {
  let items = replace ? [] : await getTracked();
  const seen = new Set(items.map(i => i.url));
  for (const it of importedItems) {
    const url = it.url || it.link;
    // Reject URLs that are not https on a supported retailer.
    if (!url || seen.has(url) || !isValidProductUrl(url)) continue;
    seen.add(url);
    items.push({
      id: generateId(),
      url,
      threshold: Number(it.threshold) || 0,
      currency: it.currency || 'AUD',
      productName: it.productName || it.name || url,
      retailerName: it.retailerName || '',
      addedAt: Date.now(),
      lastPrice: it.lastPrice != null ? Number(it.lastPrice) : null,
      lastCheckedAt: it.lastCheckedAt || null,
      priceHistory: Array.isArray(it.priceHistory) ? it.priceHistory.slice(0, MAX_PRICE_HISTORY) : []
    });
  }
  await setTracked(items);
  await refreshBadge();
  return { ok: true, count: items.length };
}

async function removeTracked(id) {
  let items = await getTracked();
  items = items.filter(i => i.id !== id);
  await setTracked(items);
  await refreshBadge();
  return { ok: true };
}

// Auto-pair: fetch the shared secret from the desktop app's /pair endpoint.
async function autoPair(desktopAppUrl) {
  const base = (desktopAppUrl || 'http://127.0.0.1:8765').trim().replace(/\/+$/, '');
  if (!isValidRelayUrl(base)) {
    return { ok: false, error: 'Desktop app URL must be http://127.0.0.1:<port> or http://localhost:<port>' };
  }
  const res = await fetch(base + '/pair', { method: 'GET' });
  if (!res.ok) return { ok: false, error: 'Desktop app returned HTTP ' + res.status };
  const data = await res.json();
  if (!data || !data.ok || !data.secret) return { ok: false, error: 'Invalid response from desktop app' };
  // Persist the secret locally so it is never synced.
  await browser.storage.local.set({ [STORAGE_SECRET]: data.secret });
  return { ok: true, secret: data.secret };
}

async function handlePageData(payload) {
  const { url, price, productName, retailerName } = payload;
  if (price == null) return { ok: true, tracked: false };

  const items = await getTracked();
  const opts = await getOptions();
  const entry = items.find(i => i.url === url || (i.url && url && i.url.split('?')[0] === url.split('?')[0]));
  if (!entry) return { ok: true, tracked: false };

  entry.lastPrice = price;
  entry.lastCheckedAt = Date.now();
  if (productName) entry.productName = productName;
  if (retailerName) entry.retailerName = retailerName;
  if (!entry.priceHistory) entry.priceHistory = [];
  entry.priceHistory.push({ price, at: Date.now() });
  if (entry.priceHistory.length > MAX_PRICE_HISTORY) entry.priceHistory.shift();
  await setTracked(items);

  const atOrBelow = price <= entry.threshold;
  if (atOrBelow && opts.notificationsEnabled) {
    try {
      const notificationId = 'deal-' + entry.id + '-' + Date.now();
      notificationIdToUrl.set(notificationId, entry.url);
      await browser.notifications.create(notificationId, {
        type: 'basic',
        title: 'Price alert',
        message: `${entry.productName} is now ${opts.currency} ${price.toFixed(2)} (your target: ${opts.currency} ${entry.threshold})`
      });
    } catch (_) {}
  }

  // Free email option: open a mailto draft so the user can send the alert themselves.
  if (atOrBelow && opts.emailAlertEnabled && opts.emailAlertTo && opts.emailAlertTo.trim()) {
    try {
      const to = opts.emailAlertTo.trim();
      // Strip CRLF and null chars from all fields used in the mailto URI.
      const cleanStr = s => (s || '').replace(/[\r\n\0]/g, ' ');
      const subject = cleanStr('Price alert: ' + (entry.productName || 'Product'));
      const body = [
        cleanStr(entry.productName || 'Product'),
        'Retailer: ' + cleanStr(entry.retailerName || ''),
        'Current price: ' + opts.currency + ' ' + price.toFixed(2),
        'Your target: ' + opts.currency + ' ' + entry.threshold,
        'Link: ' + entry.url
      ].join('\n');
      const mailto = 'mailto:' + encodeURIComponent(to) +
        '?subject=' + encodeURIComponent(subject) +
        '&body=' + encodeURIComponent(body);
      await browser.tabs.create({ url: mailto });
    } catch (_) {}
  }

  // Automatic email via desktop app relay.
  if (atOrBelow && opts.emailViaDesktopApp && opts.desktopAppUrl && opts.desktopAppUrl.trim()) {
    try {
      const base = opts.desktopAppUrl.trim().replace(/\/+$/, '');
      // Guard: only allow loopback URLs to prevent SSRF.
      if (!isValidRelayUrl(base)) {
        log('Relay URL rejected (not loopback):', base);
      } else {
        const alertUrl = base + '/alert';
        const alertPayload = {
          productName: entry.productName || 'Product',
          retailerName: entry.retailerName || '',
          url: entry.url,
          price: price,
          threshold: entry.threshold,
          currency: opts.currency || 'AUD'
        };
        const headers = { 'Content-Type': 'application/json' };
        if (opts.desktopAppSecret && opts.desktopAppSecret.trim()) {
          headers['X-Extension-Secret'] = opts.desktopAppSecret.trim();
        }
        const res = await fetch(alertUrl, {
          method: 'POST',
          headers,
          body: JSON.stringify(alertPayload)
        });
        if (!res.ok) {
          log('Desktop app relay returned', res.status);
        }
      }
    } catch (e) {
      log('Desktop app relay failed', e);
    }
  }

  return { ok: true, tracked: true, atOrBelow, price, threshold: entry.threshold };
}
