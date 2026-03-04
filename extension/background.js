/**
 * Background service worker: stores tracked products, compares prices from content script,
 * shows notifications when price is at or below target, updates badge.
 */

const STORAGE_KEY = 'productDealFinder_tracked';
const STORAGE_OPTIONS = 'productDealFinder_options';
const MAX_PRICE_HISTORY = 10;

const DEFAULT_OPTIONS = { notificationsEnabled: true, currency: 'AUD' };

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

async function getOptions() {
  try {
    const o = await browser.storage.sync.get(STORAGE_OPTIONS);
    if (o[STORAGE_OPTIONS]) return { ...DEFAULT_OPTIONS, ...o[STORAGE_OPTIONS] };
  } catch (_) {}
  const o = await browser.storage.local.get(STORAGE_OPTIONS);
  return { ...DEFAULT_OPTIONS, ...o[STORAGE_OPTIONS] };
}

async function setOptions(opts) {
  try {
    await browser.storage.sync.set({ [STORAGE_OPTIONS]: opts });
    return;
  } catch (_) {}
  await browser.storage.local.set({ [STORAGE_OPTIONS]: opts });
}

function updateBadge(count) {
  if (count === 0) {
    browser.action.setBadgeText({ text: '' });
  } else {
    browser.action.setBadgeText({ text: String(count) });
    browser.action.setBadgeBackgroundColor({ color: '#2563eb' });
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
  if (url) browser.tabs.create({ url });
});

browser.runtime.onMessage.addListener((message, _sender, sendResponse) => {
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
  sendResponse({ ok: false });
  return false;
});

async function addTracked({ url, threshold, currency, productName, retailerName }) {
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
    if (!url || seen.has(url)) continue;
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

  return { ok: true, tracked: true, atOrBelow, price, threshold: entry.threshold };
}
