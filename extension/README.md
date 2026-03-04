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

**Features:** Tracked list and options sync across Firefox installs (where you’re signed in). Popup shows last 5 prices per product (history). You can edit the target price per product, and import/export your list (Options). Data is stored in the browser only; no server.

## Load in Firefox (development)

1. Open Firefox and go to `about:debugging`.
2. Click **This Firefox** → **Load Temporary Add-on…**.
3. Select the `manifest.json` file inside the `extension` folder.
4. The extension stays loaded until you restart Firefox. Reload the add-on after changing code.

## Build / run from command line (optional)

If you have [web-ext](https://extensionworkshop.com/documentation/develop/web-ext-command-reference/) installed:

```bash
cd extension
web-ext run
```

## Options

Right-click the extension icon → **Manage Extension** → **Options**, or open the extension’s preferences from `about:addons`. You can:

- Enable or disable price-drop notifications.
- Set the default currency for display (AUD, USD, NZD).

## Structure

- `manifest.json` – Extension manifest (Manifest V3).
- `background.js` – Service worker: storage, badge, notifications, add/remove tracked items.
- `content.js` – Injected on retailer product pages: extracts price and product name, sends to background.
- `popup/` – Popup UI: list tracked products, “Track this page”, remove, open link.
- `options/` – Options page: notifications on/off, currency.

Price selectors match the desktop app (e.g. JB Hi-Fi: `[class*="PriceTag_actualPrice"]`, Officeworks: `div[class*="UnitPrice"]`).
