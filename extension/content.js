/**
 * Content script: runs on retailer product pages. Extracts price and product info,
 * sends to background for comparison with tracked targets. Uses same selectors as desktop app.
 * Debug: open DevTools (F12) → Console on the product page; look for logs starting with [PDF].
 */

(function () {
  const DEBUG = false; // set to true and reload to see [PDF] logs in Console (F12)
  const log = (...args) => { if (DEBUG) console.log('[PDF]', ...args); };

  const RETAILER_CONFIG = {
    'www.jbhifi.com.au': {
      code: 'JB_HIFI',
      name: 'JB Hi-Fi',
      selectors: '[class*="PriceTag_actualPrice"], [data-testid="ticket-price"]'
    },
    'www.officeworks.com.au': {
      code: 'OFFICEWORKS',
      name: 'Officeworks',
      selectors: 'div[class*="UnitPrice"]'
    },
    'www.amazon.com.au': {
      code: 'AMAZON_AU',
      name: 'Amazon Australia',
      selectors: '#priceblock_ourprice, #priceblock_dealprice, span.a-price span.a-offscreen, .a-price .a-offscreen'
    },
    'www.thegoodguys.com.au': {
      code: 'THE_GOOD_GUYS',
      name: 'The Good Guys',
      selectors: '.price, [data-testid="product-price"], [itemprop="price"]'
    },
    'www.harveynorman.com.au': {
      code: 'HARVEY_NORMAN',
      name: 'Harvey Norman',
      selectors: '.price, [data-testid="product-price"], [itemprop="price"]'
    }
  };

  const PRICE_REGEX = /\$?\s*(\d{1,3}(?:[,\s]\d{3})*(?:\.\d{2})|\d+(?:\.\d{2})?)/;

  function getRetailerFromHost(host) {
    return RETAILER_CONFIG[host] || null;
  }

  function getTextFromSelector(doc, selector) {
    const combined = selector.split(',').map(s => s.trim());
    for (const sel of combined) {
      try {
        const el = doc.querySelector(sel);
        if (el && el.textContent) {
          const text = el.textContent.trim();
          log('selector matched:', sel, '→ text:', text);
          return text;
        }
        log('selector no match or empty:', sel);
      } catch (e) {
        log('selector error:', sel, e.message);
      }
    }
    return null;
  }

  function getBodyText(doc) {
    const body = doc.body;
    return body ? body.innerText.trim() : null;
  }

  function parsePrice(text) {
    if (!text) return null;
    const m = text.match(PRICE_REGEX);
    if (!m) return null;
    const num = m[1].replace(/,/g, '').replace(/\s/g, '');
    const val = parseFloat(num);
    return isNaN(val) ? null : val;
  }

  function getProductName(doc) {
    const ogTitle = doc.querySelector('meta[property="og:title"]');
    if (ogTitle && ogTitle.getAttribute('content')) return ogTitle.getAttribute('content').trim();
    if (doc.title) return doc.title.trim();
    return null;
  }

  function extractPriceAndName(doc, hostConfig) {
    const selectors = hostConfig ? hostConfig.selectors : null;
    let text = selectors ? getTextFromSelector(doc, selectors) : null;
    if (!text) {
      log('no text from selectors, trying body');
      text = getBodyText(doc);
      if (text) log('body snippet:', text.substring(0, 150));
    }
    const price = parsePrice(text);
    const productName = getProductName(doc);
    log('extract result:', { price, productName: productName ? productName.slice(0, 50) : null, textLen: text ? text.length : 0 });
    return { price, productName, rawText: text ? text.substring(0, 200) : null };
  }

  function getPagePayload() {
    const host = window.location.hostname;
    const retailer = getRetailerFromHost(host);
    const { price, productName, rawText } = extractPriceAndName(document, retailer);
    const url = window.location.href;
    return {
      url,
      host,
      retailerCode: retailer ? retailer.code : 'GENERIC',
      retailerName: retailer ? retailer.name : 'Unknown',
      price,
      productName: productName || document.title || url,
      rawText
    };
  }

  function sendPageData() {
    browser.runtime.sendMessage({ type: 'PAGE_DATA', payload: getPagePayload() }).catch(() => {});
  }

  browser.runtime.onMessage.addListener((msg, _sender, sendResponse) => {
    if (msg.type === 'GET_PAGE_DATA') {
      sendResponse(getPagePayload());
    }
    return false;
  });

  // Run once, then retry on a timer and when the price element appears (JB Hi-Fi etc. often load price via JS).
  let runCount = 0;
  function runExtract() {
    runCount += 1;
    log('runExtract #' + runCount, new Date().toISOString());
    sendPageData();
  }

  const host = window.location.hostname;
  const config = getRetailerFromHost(host);
  log('content script loaded', host, 'selectors:', config ? config.selectors : 'none');

  runExtract();
  [1500, 3500].forEach(ms => setTimeout(() => { log('retry at', ms + 'ms'); runExtract(); }, ms));

  // When a node matching the price selector appears (e.g. React hydration), extract once.
  const selectorList = config ? config.selectors : null;
  if (selectorList && document.body) {
    const selectors = selectorList.split(',').map(s => s.trim());
    const observer = new MutationObserver(() => {
      for (const sel of selectors) {
        try {
          if (document.querySelector(sel)) {
            log('MutationObserver: element appeared', sel);
            observer.disconnect();
            runExtract();
            return;
          }
        } catch (_) {}
      }
    });
    observer.observe(document.body, { childList: true, subtree: true });
    setTimeout(() => { observer.disconnect(); log('observer stopped after 8s'); }, 8000);
  }
})();
