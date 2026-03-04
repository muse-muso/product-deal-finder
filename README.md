product-deal-finder
====================

This repo contains two ways to track product prices on Australian retailers:

- **Desktop app** (`src/`) – Windows WPF application. Uses Playwright to scan product pages on a schedule and sends **email** alerts when the price reaches your target. See below for build and run.
- **Firefox extension** (`extension/`) – Browser extension. Tracks products when you **visit** product pages and shows **browser notifications** when the price is at or below your target. See [extension/README.md](extension/README.md) for how to load and use it.

Both use the same retailers (JB Hi-Fi, Officeworks, Amazon AU, The Good Guys, Harvey Norman) and the same price selectors where applicable.

---

Windows desktop application (in `src/`)
---------------------------------------

Windows desktop app to track product prices across the selected Australian retailers and send you email alerts when your desired price is reached. All data and logic run locally on your machine; no external paid services are required.

Supported retailers (launch)
----------------------------

- `jbhifi.com.au`
- `amazon.com.au`
- `thegoodguys.com.au`
- `officeworks.com.au`
- `harveynorman.com.au`
- Generic sites using user‑provided CSS selectors

The app uses a headless Chromium browser (via Playwright) to load product pages, extract prices, and evaluate your price thresholds. When a threshold is met, it sends you an email with a direct link to the retailer’s product page.

Prerequisites
-------------

1. **.NET SDK**
   - Install **.NET 8 SDK or later** for Windows x64 from `https://dotnet.microsoft.com/download`.

2. **Playwright browsers**
   - The app uses Playwright’s .NET driver, which expects browsers installed by the *project’s* install script. From the repo `src` folder, **build** the solution, then run (one‑time):
     ```powershell
     cd "C:\Users\<YOUR_USER>\Documents\github\product-deal-finder\src"
     & "C:\Program Files\dotnet\dotnet.exe" build ProductDealFinder.sln
     powershell -ExecutionPolicy Bypass -File ".\ProductDealFinder\bin\Debug\net8.0-windows\playwright.ps1" install
     ```
   - This downloads the Chromium (and related) binaries that the driver expects. If you see *“Executable doesn't exist at … chromium_headless_shell…”*, run the same `playwright.ps1 install` command again (see **Troubleshooting**).

3. **SMTP email account**
   - Any provider that supports SMTP with username + password (or app password).
   - Common options:
     - **Gmail**
       - Server: `smtp.gmail.com`
       - Port: `587`
       - TLS: enabled
       - Username: your full Gmail address
       - Password: **App password** (recommended; requires 2‑step verification).
     - **Outlook.com / Microsoft 365**
       - Server: `smtp.office365.com`
       - Port: `587`
       - TLS: enabled
       - Username: your full Outlook/Microsoft 365 address
       - Password: your account password or an app password, depending on your org’s settings.
       - **Note:** Many Microsoft 365 tenants have **SMTP AUTH disabled** for mailboxes. If you get *"535 … SmtpClientAuthentication is disabled"*, this app cannot send via that account until an admin enables SMTP AUTH for your mailbox, or you use a different provider (e.g. **Gmail** with an app password). See **Troubleshooting** below.

Building the application
------------------------

All commands below are for **PowerShell**. When the path to an executable contains spaces (e.g. `C:\Program Files\dotnet\dotnet.exe`), you must use the **call operator** `&` before the quoted path; otherwise PowerShell treats the string as an expression and does not run the program.

1. Open a terminal and go to the `src` folder:
   ```powershell
   cd "C:\Users\<YOUR_USER>\Documents\github\product-deal-finder\src"
   ```

2. Build the solution:
   ```powershell
   & "C:\Program Files\dotnet\dotnet.exe" build ProductDealFinder.sln
   ```

3. Run the WPF application:
   ```powershell
   & "C:\Program Files\dotnet\dotnet.exe" run --project "ProductDealFinder\ProductDealFinder.csproj"
   ```

   The main window (`Product Deal Finder`) should appear. A background scan service will also start automatically inside the app.

How to check logs
-----------------

The app does not write log files by default. To see errors and log output when the app crashes or misbehaves, run it from **PowerShell** so the console stays open and shows the full exception and any log messages:

```powershell
cd "C:\Users\<YOUR_USER>\Documents\github\product-deal-finder\src"
& "C:\Program Files\dotnet\dotnet.exe" run --project "ProductDealFinder\ProductDealFinder.csproj"
```

- **Crashes:** When an unhandled exception occurs, the full stack trace and message (e.g. *"null at ordinal 5"*) appear in this console before the window closes.
- **Normal runs:** You can leave the console open while using the app; scan and relay logs will appear there at **Information** level.

If you prefer to run the app by double‑clicking or from Start, use the command above once when something goes wrong to capture the error.

First‑run behaviour and data storage
------------------------------------

- On first run, the app will:
  - Create a local SQLite database at:
    - `C:\Users\<YOU>\AppData\Local\ProductDealFinder\product-deal-finder.db`
  - Seed the **Retailers** table with:
    - JB Hi‑Fi, Amazon Australia, The Good Guys, Officeworks, Harvey Norman, and a `Generic` entry.
- All your:
  - Products, product targets, thresholds, and scrape history are stored in this local DB.
  - SMTP settings (server, port, from email, username, scan interval) are stored in the DB.
  - **SMTP password is *not* stored in the DB**; instead, it is kept in **Windows Credential Manager** under a fixed key.

Configuring email and scan interval
-----------------------------------

1. **Open the app** (see “Building the application” above).

2. In the left panel **“Email / SMTP Settings”**, fill in:

   - **SMTP Host**
     - Example (Gmail): `smtp.gmail.com`
     - Example (Outlook/Office 365): `smtp.office365.com`
   - **SMTP Port**
     - Usually `587` for both Gmail and Outlook (STARTTLS).
   - **Use TLS/SSL**
     - Keep this **checked** for secure SMTP.
   - **From Email**
     - The email address alerts are sent *from* (your SMTP account).
   - **From Display Name (optional)**
     - Friendly name visible in your inbox, e.g. `Product Deal Finder`.
   - **Default Mailbox Name (optional)**
     - A label for this sending account (e.g. `Gmail`, `Work`) for your reference.
   - **Default Email Address**
     - Where to send alerts (price alerts and “product added” confirmations). Leave blank to use **From Email** as the recipient.
   - **SMTP Username**
     - Often the same as **From Email** (full address).
   - **SMTP Password**
     - For **Gmail**: create an **App password** (recommended) and use that.
     - For **Outlook/Office 365**: use your password or app password as required.
   - **Scan Interval (minutes)**
     - How often the background scanner should run and check prices.
     - Default is `60` (once an hour). You can safely set `30`, `15`, etc., but avoid very low intervals to reduce load on retailer sites.
   - **Extension relay (Firefox → email)**
     - If you use the **Firefox extension** (see `extension/` in this repo), you can enable **“Enable extension relay”** and set the **Relay port** (default `8765`). The app will listen on `http://127.0.0.1:8765` for price alerts from the extension and send them by email using your SMTP. Optional **Secret token**: set the same value in the extension Options so only your extension can use the relay.

3. Click **“Save Email &amp; Scan Settings”**.

   - The app will:
     - Validate your inputs.
     - Save SMTP host/port/username + scan interval into the local DB.
     - Store the SMTP password securely in **Windows Credential Manager** under the key:
       - `ProductDealFinder_SMTP`
   - Status updates will be shown in the **Status / Last Action** box on the right panel.

Adding a product and price threshold
------------------------------------

1. In the right panel **“Add Product &amp; Target”**, fill in:

   - **Product Name**
     - Any label that makes sense to you (e.g. `Sony WH-1000XM5 Headphones`).
   - **Model Number (optional)**
     - For your own reference / organization; does not affect scraping.
   - **Retailer**
     - Select one of the pre‑seeded retailers from the drop‑down:
       - `JB Hi-Fi`
       - `Amazon Australia`
       - `The Good Guys`
       - `Officeworks`
       - `Harvey Norman`
       - `Generic` (for custom sites + selectors)
   - **Product Page URL**
     - Paste the full URL of the product page on the retailer’s website.
   - **Optional CSS Price Selector Override**
     - Advanced option.
     - If the default adapter cannot find the price, you can enter a specific CSS selector that points to the price element.
       - Example: `span.price`, `span.a-offscreen`, `[data-testid='product-price']`.
   - **Price Threshold (AUD)**
     - The price at or below which you want to receive a notification (e.g. `299.00`).

2. Click **“Save Product &amp; Threshold”**.

   - The app will:
     - Create a **Product**.
     - Create a **ProductTarget** linking the product to the selected retailer and page URL.
     - Create a **PriceThreshold** (AUD, “at or below” comparison) for that target.
   - The status box will display the new **target ID**, which is useful if you inspect the database manually.

How scanning and notifications work
-----------------------------------

- A background service (`BackgroundScanHostedService`) runs as long as the app is open.
- Every **scan interval**:
  1. The app loads all active `ProductTarget`s that have at least one active `PriceThreshold`.
  2. For each target:
     - It uses **Playwright + Chromium** to open the product page headlessly.
     - Chooses the right adapter based on `RetailerSite.Code`:
       - `JB_HIFI` → `JbHiFiAdapter`
       - `AMAZON_AU` → `AmazonAuAdapter`
       - `THE_GOOD_GUYS` → `TheGoodGuysAdapter`
       - `OFFICEWORKS` → `OfficeworksAdapter`
       - `HARVEY_NORMAN` → `HarveyNormanAdapter`
       - `GENERIC` → `GenericCssSelectorAdapter`
     - The adapter:
       - Attempts to read the price using:
         - Your **CSS override** (if set), or
         - A built‑in default selector per retailer, or
         - As a fallback, scans the full page text for something that looks like a price (e.g. `$1,234.56`).
     - Saves a `ScrapeResult` row with parsed price, raw price text, and timestamp.
  3. For each `PriceThreshold` on that target:
     - Checks if the current price meets the condition:
       - `LessThan` or `LessThanOrEqual`.
     - If **yes**, composes an email and sends it using your SMTP settings via **MailKit**.
- The email includes:
  - Product name and model.
  - Retailer name.
  - Your threshold and the current price (if parsed).
  - A **direct link** to the product page as stored in `ProductTarget.ProductPageUrl`.

Security and privacy
--------------------

- **No analytics / telemetry** are implemented locally besides .NET/Playwright defaults.
- All persistent data (products, targets, thresholds, scrape history, non‑secret settings) are stored in:
  - `C:\Users\<YOU>\AppData\Local\ProductDealFinder\product-deal-finder.db`
- The SMTP **password** is never written to the database or config files:
  - Stored in **Windows Credential Manager** under `ProductDealFinder_SMTP`.
  - Retrieved only when sending an email.
- The app uses **HTTPS** connections to retailer sites (handled by Playwright) and **TLS** for SMTP where enabled.

Troubleshooting
---------------

- **App crashes (window closes immediately or after an action)**
  - **See the actual error:** Run the app from PowerShell so the exception is visible:
    ```powershell
    cd "C:\Users\<YOUR_USER>\Documents\github\product-deal-finder\src"
    & "C:\Program Files\dotnet\dotnet.exe" run --project "ProductDealFinder\ProductDealFinder.csproj"
    ```
    Any unhandled exception will be printed in the console. The app also shows a message box for many errors.
  - **"Null at ordinal 5" (or similar) / app closes immediately after start:** The database was created with an older schema; a column that was added later (e.g. extension relay settings) is `NULL` for your existing row, but the app expected a value. This is now handled in code. If you still see it, run from PowerShell (see **How to check logs** above) to confirm the full error, then either **Option A** (delete the DB file and start fresh) or **Option B** (ensure the app is up to date and run again).
  - **Database out of date:** If you see errors about a missing table (e.g. `ScanErrors`) or SQLite schema, the local DB was created before a code update. Either:
    - **Option A – Reset the database (you will lose saved products and settings):** Close the app, then delete the DB file:
      - `C:\Users\<YOU>\AppData\Local\ProductDealFinder\product-deal-finder.db`
      Start the app again; it will create a new DB and seed retailers.
    - **Option B:** Keep the file; the app now tries to create missing tables (e.g. `ScanErrors`) on startup. Re-run the app after updating the code.
  - **Playwright / browser errors on first run:** Install the browsers (see Prerequisites):
    ```powershell
    dotnet tool install --global Microsoft.Playwright.CLI
    playwright install chromium firefox
    ```

- **App won’t start / build errors**
  - Ensure `.NET 8 SDK` is installed and `dotnet --version` prints at least `8.x`.
  - Rebuild from the `src` directory (use `&` before the path in PowerShell):
    ```powershell
    cd "C:\Users\<YOUR_USER>\Documents\github\product-deal-finder\src"
    & "C:\Program Files\dotnet\dotnet.exe" build ProductDealFinder.sln
    ```

- **“Executable doesn't exist at … chromium_headless_shell…” / Playwright missing browsers**
  - The .NET driver expects browsers installed by the *project’s* script, not only the global CLI. From the `src` folder, **build** then run the install script:
    ```powershell
    cd "C:\Users\<YOUR_USER>\Documents\github\product-deal-finder\src"
    & "C:\Program Files\dotnet\dotnet.exe" build ProductDealFinder.sln
    powershell -ExecutionPolicy Bypass -File ".\ProductDealFinder\bin\Debug\net8.0-windows\playwright.ps1" install
    ```
    Then start the app again. If the script path doesn’t exist, build the solution first so the script is copied to that output folder.

- **"535 … SmtpClientAuthentication is disabled" (Outlook / Microsoft 365)**
  - Your mailbox has **SMTP client authentication** turned off by policy. This app uses username + password SMTP; it does not support Microsoft’s modern OAuth flow.
  - **Options:**
    1. **Use Gmail instead:** Create a Gmail account (or use an existing one), turn on 2‑step verification, create an **App password**, and use `smtp.gmail.com` / port 587 / TLS in the app. This is the most reliable option.
    2. **Ask your admin:** A Microsoft 365 admin can enable SMTP AUTH for your mailbox (or for the tenant). See Microsoft’s docs: <https://aka.ms/smtp_auth_disabled>.
  - Your product and threshold are still saved; only the confirmation email failed to send.

- **"535 … Username and Password not accepted" / "BadCredentials" (Gmail)**
  - Gmail no longer accepts your **normal account password** for SMTP. You must use an **App password**.
  - **Steps:**
    1. In your Google Account go to **Security** → **2-Step Verification** and turn it **on** (required for App passwords).
    2. In **Security** → **2-Step Verification** → **App passwords**, create a new App password (choose “Mail” and “Windows Computer” or “Other”).
    3. Copy the 16‑character password (no spaces). In the app, paste it into **SMTP Password**, then click **Save Email &amp; Scan Settings** so it is stored in Credential Manager.
    4. Use your **full Gmail address** as both **From Email** and **SMTP Username**; host `smtp.gmail.com`, port `587`, TLS on.
  - If you already use an App password, re‑enter it and save again (the stored credential may be wrong or expired). See <https://support.google.com/mail/?p=BadCredentials>.

- **No emails received**
  - Double‑check:
    - SMTP host, port, TLS, and username.
    - Password in the **Email / SMTP Settings** panel (re‑enter and save).
    - For Gmail, confirm you are using an **app password** (not your normal password).
  - Check the **Status / Last Action** box for errors while saving settings or adding products.

- **Wrong price in email / alert (e.g. 13 AUD instead of real price)**
  - The scraper uses the **first** number that looks like a price on the page; e.g. “13” can come from “13-inch” in the product name. Use these steps to find and fix it:

  1. **Run the app from the command line** so scrape logs appear in the console:
     ```powershell
     cd "C:\Users\<YOUR_USER>\Documents\github\product-deal-finder\src"
     & "C:\Program Files\dotnet\dotnet.exe" run --project "ProductDealFinder\ProductDealFinder.csproj"
     ```

  2. **Trigger a scan** so logs are written: in the app, click **“Run scan now (manual / test)”** and wait for “Manual scan completed.”

  3. **In the console**, find the log lines for your product (search for the product URL or `TargetId=`). You will see:
     - **Selector** – the CSS selector used (or “(body fallback)”).
     - **Scraped text snippet** – the first ~2500 characters of the text the scraper used.
     - **RawMatch** and **ParsedPrice** – the substring that was treated as the price and the number sent to the email.

  4. **In the snippet**, search for the **ParsedPrice** value (e.g. `13`). See where it appears (e.g. inside “13-inch”). That confirms why the wrong number was chosen.

  5. **Open the product page in your browser** (e.g. Chrome): paste the product URL from the app into the address bar.

  6. **Find the main price element**: right‑click the **correct price** on the page → **Inspect**. In DevTools, the price element will be highlighted. Note its tag and any `class` or `id` (e.g. `span.price`, `.product-price`, `[data-testid="product-price"]`). Build a short CSS selector that matches only that element (e.g. `.price--value` or `span[data-automation="product-price"]`).

  7. **In the app**, set the override and rescan:
     - The **“Optional CSS Price Selector Override”** field is on the main window when **adding** a product. To fix an existing product: on the main window, enter the same product details (name, retailer, URL, threshold) and paste your selector into **“Optional CSS Price Selector Override”**, then click **Save Product &amp; Threshold**. Open **Management Window** → **Products &amp; Thresholds**, delete the old (wrong-price) target for that product if you now have two, then run **“Run scan now”** again. Check the console **ParsedPrice** or the next email to confirm the price is correct.

- **No price parsed / no alerts**
  - The built‑in selectors are best‑effort and may need adjustment as sites change.
  - Try:
    - Inspecting the retailer product page in your browser to find a stable CSS selector for the price element.
    - Entering that selector in **“Optional CSS Price Selector Override”** and saving again.

Uninstall and cleaning up
-------------------------

- To remove the app itself (if packaged as a classic installer), use **Apps & Features** in Windows.
- To delete local data:
  - Delete the SQLite DB:
    - `C:\Users\<YOU>\AppData\Local\ProductDealFinder\product-deal-finder.db`
  - Remove the stored SMTP credential via **Windows Credential Manager**:
    - Open Credential Manager → Windows Credentials → locate `ProductDealFinder_SMTP` → Remove.

Next steps / enhancements
-------------------------

- Add richer UI screens:
  - List existing products, thresholds, and latest prices.
  - Show scan history and last errors.
- Fine‑tune retailer‑specific selectors as their HTML changes.
- Add a `setup.exe` installer and user‑friendly shortcuts for non‑technical users.

For now, this README covers the complete workflow to **build**, **configure**, and **run** the core application end‑to‑end with Gmail or Outlook SMTP. 