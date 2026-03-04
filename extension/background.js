/**
 * Background service worker: stores tracked products, compares prices from content script,
 * shows notifications when price is at or below target, updates badge.
 */

const STORAGE_KEY = 'productDealFinder_tracked';
const STORAGE_OPTIONS = 'productDealFinder_options';

const DEFAULT_OPTIONS = { notificationsEnabled: true, currency: 'AUD' };

const notificationIdToUrl = new Map();

function generateId() {
  return Date.now().toString(36) + Math.random().toString(36).slice(2);
}

async function getTracked() {
  const o = await browser.storage.local.get(STORAGE_KEY);
  return o[STORAGE_KEY] || [];
}

async function setTracked(items) {
  await browser.storage.local.set({ [STORAGE_KEY]: items });
}

async function getOptions() {
  const o = await browser.storage.local.get(STORAGE_OPTIONS);
  return { ...DEFAULT_OPTIONS, ...o[STORAGE_OPTIONS] };
}

async function setOptions(opts) {
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
    lastCheckedAt: null
  };
  items.push(entry);
  await setTracked(items);
  await refreshBadge();
  return { ok: true, id: entry.id };
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
