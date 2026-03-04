product-deal-finder
====================

Windows desktop application to track product prices across selected Australian retailers and send you email alerts when your desired price is reached. All data and logic run locally on your machine; no external paid services are required.

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
   - Install the Playwright CLI as a global tool and download browsers (one‑time):
     ```powershell
     dotnet tool install --global Microsoft.Playwright.CLI
     ```
   - Open a new PowerShell window and run (from anywhere):
     ```powershell
     playwright install chromium firefox
     ```
   - This downloads Chromium and Firefox binaries Playwright will use locally.

3. **SMTP email account**
   - Any provider that supports SMTP with username + password (or app password).
   - Common options:
     - **Gmail**
       - Server: `smtp.gmail.com`
       - Port: `587`
       - TLS: enabled
       - Username: your full Gmail address
       - Password: **App password** (recommended; requires 2‑step verification).
     - **Outlook.com / Office 365**
       - Server: `smtp.office365.com`
       - Port: `587`
       - TLS: enabled
       - Username: your full Outlook/Office 365 address
       - Password: your account password or an app password, depending on your org’s settings.

Building the application
------------------------

1. Open a terminal and go to the `src` folder:
   ```powershell
   cd "C:\Users\<YOUR_USER>\Documents\github\product-deal-finder\src"
   ```

2. Build the solution:
   ```powershell
   "C:\Program Files\dotnet\dotnet.exe" build ProductDealFinder.sln
   ```

3. Run the WPF application:
   ```powershell
   "C:\Program Files\dotnet\dotnet.exe" run --project "ProductDealFinder\ProductDealFinder.csproj"
   ```

   The main window (`Product Deal Finder`) should appear. A background scan service will also start automatically inside the app.

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
     - The email address alerts will be sent from (and to).
   - **From Display Name (optional)**
     - Friendly name visible in your inbox, e.g. `Product Deal Finder`.
   - **SMTP Username**
     - Often the same as **From Email** (full address).
   - **SMTP Password**
     - For **Gmail**: create an **App password** (recommended) and use that.
     - For **Outlook/Office 365**: use your password or app password as required.
   - **Scan Interval (minutes)**
     - How often the background scanner should run and check prices.
     - Default is `60` (once an hour). You can safely set `30`, `15`, etc., but avoid very low intervals to reduce load on retailer sites.

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

- **App won’t start / build errors**
  - Ensure `.NET 8 SDK` is installed and `dotnet --version` prints at least `8.x`.
  - Rebuild from the `src` directory:
    ```powershell
    cd "C:\Users\<YOUR_USER>\Documents\github\product-deal-finder\src"
    "C:\Program Files\dotnet\dotnet.exe" build ProductDealFinder.sln
    ```

- **Playwright errors about missing browsers**
  - Re‑run:
    ```powershell
    playwright install chromium firefox
    ```

- **No emails received**
  - Double‑check:
    - SMTP host, port, TLS, and username.
    - Password in the **Email / SMTP Settings** panel (re‑enter and save).
    - For Gmail, confirm you are using an **app password** (not your normal password).
  - Check the **Status / Last Action** box for errors while saving settings or adding products.

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