# Product Deal Finder – Firefox extension

Browser extension that tracks product prices on Australian retailers and notifies you when the price is at or below your target. Same retailers and price logic as the [desktop app](../src/) in this repo.

## Supported retailers

- JB Hi-Fi
- Officeworks
- Amazon Australia
- The Good Guys
- Harvey Norman

## How it works

1. **Track a product:** Open a product page on one of the supported sites, then either:
   - Click the extension icon → enter your target price (AUD) → **Track this page**, or
   - Right‑click the page → **Track this product with Product Deal Finder** (then open the extension and set your target price).
2. **Price is checked when you visit:** Each time you load a product page that you’re tracking, the extension reads the price from the page and compares it to your target.
3. **Notification:** If the current price is at or below your target, you get a browser notification. Click it to open the product page.

**Features:** Tracked list and options sync across Firefox installs (where you’re signed in). Popup shows last 5 prices per product (history). You can edit the target price per product, and import/export your list (Options). **Email alerts (free):** (1) **Draft email** — in Options enable “Open draft email when price reaches target” and set your email; when a price drops, a draft opens and you click Send. (2) **Automatic email via desktop app** — run the Product Deal Finder **Windows desktop app**, enable “Extension relay” in its settings, then in the extension Options enable “Send price alerts to desktop app”; the extension POSTs alerts to the app, which emails you via your SMTP. Fully automatic, $0. Data is stored in the browser (and optionally the desktop app) only; no cloud server.

## Load in Firefox (development)

1. Open Firefox and go to `about:debugging`.
2. Click **This Firefox** → **Load Temporary Add-on…**.
3. Select the `manifest.json` file inside the `extension` folder.
4. The extension stays loaded until you restart Firefox. Reload the add-on after changing code.

## Debugging (price not showing)

1. Open the **product page** (e.g. JB Hi-Fi) in a normal tab.
2. Press **F12** to open Developer Tools → open the **Console** tab.
3. In the console filter box, type `PDF` so you only see logs from the extension’s content script.
4. **Reload the product page** (F5 or Ctrl+R). You should see lines like:
   - `[PDF] content script loaded www.jbhifi.com.au selectors: ...`
   - `[PDF] runExtract #1 ...`
   - For each selector: either `selector matched: ... → text: 1577` or `selector no match or empty: ...`
   - `[PDF] extract result: { price: 1577, ... }` or `price: null`
5. **What to check:**
   - If you see `selector no match or empty` for both selectors on every run, the price element isn’t in the page when we look (e.g. different DOM, or inside an iframe). Try running in the console: `document.querySelector('[data-testid="ticket-price"]')` — if that returns `null`, the selector doesn’t match the current page.
   - If you see `selector matched` and `text: 1577` but `extract result: { price: null }`, the regex isn’t matching the text (we can fix the parser).
   - If you never see `MutationObserver: element appeared`, the price was in the DOM from the start; if it appears only after a few seconds, the delayed runs should still pick it up.
6. **Optional:** Run `document.querySelectorAll('[class*="PriceTag"], [data-testid="ticket-price"]')` in the console to list all matching elements and their `textContent`.

## Build / run from command line (optional)

If you have [web-ext](https://extensionworkshop.com/documentation/develop/web-ext-command-reference/) installed:

```bash
cd extension
web-ext run
```

## Options

Right-click the extension icon → **Manage Extension** → **Options**, or open the extension’s preferences from `about:addons`. You can:

- Enable or disable price-drop **browser notifications**.
- **Email alerts (free):** (1) **Draft email** — “Open draft email when price reaches target” + your email; a pre-filled draft opens and you click Send. (2) **Automatic via desktop app** — enable “Send price alerts to desktop app”, set URL (e.g. `http://127.0.0.1:8765`); the Windows desktop app must be running with “Extension relay” enabled and the same optional secret token.
- Set the default currency for display (AUD, USD, NZD).

## Structure

- `manifest.json` – Extension manifest (Manifest V3).
- `background.js` – Service worker: storage, badge, notifications, add/remove tracked items.
- `content.js` – Injected on retailer product pages: extracts price and product name, sends to background.
- `popup/` – Popup UI: list tracked products, “Track this page”, remove, open link.
- `options/` – Options page: notifications on/off, currency.

Price selectors match the desktop app (e.g. JB Hi-Fi: `[class*="PriceTag_actualPrice"]`, Officeworks: `div[class*="UnitPrice"]`).
