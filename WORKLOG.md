# empifisJsonService2 – work log and plan

Handoff notes so work can continue if a session is lost. Last updated 2026-09-29 (version 2.3.1).

## What the service is

Windows service that accepts receipts as JSON (HTTP on port 5006: `/fullReceipt`, `/fiscalCommand`; or files in `C:\Altera\json\in` → `C:\Altera\json\out`) and prints them on the Empirija fiscal device through the EmpiFisX COM component.

| | |
|---|---|
| Install folder | `C:\Altera\EmpifisJsonAPI` |
| Windows service | `empifisJsonAPI2Service` ("EmpiFis JSON API 2"), LocalSystem, **manual start** (user's choice) |
| Config | `C:\Altera\EmpifisJsonAPI\config.json` (port, file_mode, radison_error, com_timeout_seconds = 60, paths, Cors) |
| Log | `C:\Altera\Log\json2.log`, daily archive `C:\Altera\Log\Archive\json2_YYYY-MM-DD.log`, kept 10 days |
| EmpiFisX | `C:\Altera\VersionX\EmpiFisX.dll`, **32-bit**, apartment-threaded, registered only for 32-bit programs → the service must stay x86 |
| Manual | `empifisJSON.docx`, published as `empifisJSON_<version>.docx`. All corrections accepted (2026-09-29, cover says 2.3.2); edit it directly in Word from now on and update the cover version and table of contents when the content changes |
| Installer | `installer\build-installer.ps1` → `installer\output\EmpifisJsonSetup-<version>.exe` (service + optional ReceiptTester) |

## Build, test, release, deploy

- Build/test: `dotnet build`, `dotnet test tests\empifisJsonService2.Tests` (27 tests, fake fiscal device; needs x86 .NET 10 runtimes, installed on this PC).
- Till package: `dotnet publish empifisJsonService2.csproj -p:PublishProfile=Till` → `bin\publish\till\` (self-contained win-x86, includes `install-and-update.bat`, `run-service.bat`, the manual).
- Release: bump `<Version>` in the csproj, push, then `gh release create vX.Y.Z --target master`. `.github/workflows/release.yml` tests, builds with the latest .NET 10 SDK and attaches `empifisJsonService2-X.Y.Z-win-x86.zip`. `build.yml` runs on every push. Dependabot: weekly NuGet, monthly Actions.
- Deploy: stop the service, copy the package into `C:\Altera\EmpifisJsonAPI` (keep `config.json`), run `install-and-update.bat` as admin (checks EmpiFisX registration, .NET runtime, config; recreates the service with manual start and restart-on-failure).

## Done on 2026-09-29 (2.1.7 → 2.3.1, all on master and GitHub)

**Receipts and printing**
- One device operation at a time (`AcquireDeviceLockAsync`) across HTTP and file mode, including the Radison receipt-number reads.
- Duplicate-receipt protection: fiscal/return receipts read the next receipt number (GetFiscalInfo 2) before; on an error or timeout, if it moved, the receipt printed → answer 0 and don't reset.
- File mode: interrupted `.processing` files are quarantined to `in\unconfirmed` with response code **557** instead of being reprinted; the out folder is no longer wiped (responses older than a day are deleted); responses written via `.tmp` + rename.
- Return receipts: refund info sent once; `RefundReceiptInfo`/`ReturnReceiptInfo` and `DocNo`/`DocumentNo` both accepted.
- Unknown JSON fields are logged as warnings (misspelled names were silently dropped).
- Readable error messages (`ErrorCodes.cs`, `ErrorCodes.Ecr.cs` with 282 Worldline card terminal codes).

**EmpiFis getting stuck (reported at a customer, not reproduced, no logs)**
- EmpiFisX runs on its own STA thread (`ComStaThread.cs`). Timeout → thread abandoned, new EmpiFisX on a new thread (555).
- Still stuck (2 timeouts in a row, reload fails, or creation hangs twice) → log + `Environment.FailFast` → Windows restarts the service (5 s, 30 s, then every 5 min; set by the install script). Tested on the device with `com_timeout_seconds` 0.
- `{"Command":"ReloadEmpiFis"}` on `/fiscalCommand` reloads without printing (replaced `/diag/test-printx-unload-reload`, which printed three X reports).
- Diagnostics: startup device check in the log, calls slower than 10 s logged, service start/stop logged.

**Platform and code**
- .NET 10 (was 8; 8 support ends 2026-11-10), self-contained x86 package, workstation GC, English-only resources.
- Late-bound (`dynamic`) calls for PrintTareDeposit(Void), EndFiscalReceiptPayment, GoodsReturnPayment, EndCacheReceipt are deliberate: IEmpiFisX is dual, a typed call on an older EmpiFisX.dll would crash; late-bound returns 998. Don't regenerate the interop.
- `ReceiptProcessor` refactored (635 → ~290 lines), payment classes merged, 0 build warnings, `IFiscalDevice` interface for tests.
- NLog 6 config fixed (an invalid setting broke archiving).

**Manual** (tracked changes): wrong field names, invalid JSON, credit4 rows, report dates `YYYY.MM.DD` (tested: device rejects `YYYYMMDD`), response format (HTTP wraps the JSON in a string), new sections File Mode, Configuration, Installation and Logs, ReloadEmpiFis, codes 557/998.

**Machine setup (this PC)**: stale services `empifisJson` and `empifisJsonService2` deleted (registry backups in `C:\Altera\service-backup-2026-09-29`); old installs in `C:\Altera\EmpifisJsonAPI-backup-2.1.7-20260929` and `...-backup-2.2.1-20260929`; .NET 10 SDK + x86 runtimes installed; `gh` logged in.

## Decisions (don't reopen)

- The API listens on all network interfaces without authentication – intended.
- A receipt without payment (or with zero total) ends as a cache receipt – keep.
- HTTP responses are string-wrapped JSON (`"{\"ErrorCode\":0,...}"`), file mode plain JSON – keep; POS clients work with it.
- Service start type is manual.
- No `/status` endpoint, no input pre-validation.
- No automatic reload on EmpiFis error codes (500/501/520…) without evidence.

## Tested / not tested

- Tested on this PC's real device, running as the service: X report, non-fiscal, 0.01 fiscal receipt + return, file mode, report date formats, ReloadEmpiFis, timeout reload, self-restart by Windows.
- **Not tested on hardware:** the duplicate-receipt check after a timeout during a real fiscal receipt (covered by unit tests only). A fresh EmpiFisX needs 1–3 s for its first call, so `com_timeout_seconds` must stay well above that.

## Plan / open items

1. **Installer – done 2026-09-29** (`installer\`): `.\installer\build-installer.ps1` publishes the service and ReceiptTester (`C:\ReceiptTester`) and compiles `EmpifisSetup.iss` with Inno Setup 7 into `installer\output\EmpifisJsonSetup-<version>.exe` (~88 MB). Tested on this PC (update of an unzipped install, twice): config.json kept, start type kept, old files removed, folders/permissions/firewall rule, uninstall entry, Start menu shortcuts, service starts. Not tested: a truly fresh PC (Windows Sandbox is disabled here) and uninstall. Decisions: autostart checkbox **off** by default (new installs only; updates keep the existing start type), "start now" off, firewall rule on; default config.json = this PC's (`installer\config.default.json`). The manual is `empifisJSON.docx` in the repo and is published as `empifisJSON_<version>.docx`. Original requirements:
   - Service (always) → `C:\Altera\EmpifisJsonAPI` from the Till publish output, including the latest manual (`empifisJSON_*.docx`) in the same folder.
   - **ReceiptTester → `C:\Altera\ReceiptTester` as an optional component, unticked by default** (it can print Z reports / fiscal receipts on a customer's live till). Source: `publish/win-x86/ReceiptTester.exe` from the ReceiptTester repo.
   - Create `C:\Altera\Log` and `C:\Altera\Log\Archive` if missing; give normal users write access to the log folder (ReceiptTester logs there too).
   - Create the file mode folders from `config.json` (`JsonPathConfig.InFilePath` / `OutFilePath`, defaults `C:\Altera\json\in\`, `C:\Altera\json\out\`) if missing – read the paths from an existing config.json. (The service itself now also creates them at startup, 2.3.2+.)
   - Keep an existing `config.json`; install a default one only if missing.
   - **Must also update PCs where the service was just unzipped (no installer):** stop the running `empifisJsonAPI2Service` before copying, remove leftovers of older framework-dependent builds (`Interop.Empirija.dll`, `web.config`, `runtimes\`, old docx), recreate the service like `install-and-update.bat` (manual start, restart-on-failure, EmpiFisX 32-bit registration check with a clear message).
2. ~~ReceiptTester review~~ **done 2026-09-29** (v0.4 on master and v0.3, commit 81cf8bc): .NET 10, single-file 32-bit exe, fixed non-fiscal deposit field names (the old ErrorCode 23), Log Repeater number parsing on Lithuanian Windows, `ItemUnit`, Stop between receipts, log location, Z-report confirmations, config port, ReloadEmpiFis button, 27 tests. Device finding: **Tare Deposit / Tare Deposit Void work only in fiscal receipts** (a non-fiscal receipt returns ErrorCode 18, ERR_NONFIS_STATE; a fiscal one prints, tested 2026-09-29) – removed from the manual's non-fiscal section, noted in the fiscal table; ReceiptTester 0.6 has them in the fiscal template. ReceiptTester 0.6 also answers ErrorCode 50 (drawer low) in a batch with Money In 50000, and has its own CI (build/test/exe artifact) and Dependabot.
   **Config risks on a new PC** (told the user): POS HTTP timeout must be longer than `com_timeout_seconds` (60) + ~10 s reload, or the POS may give up and resend; Radison customers need `radison_error: on` (default off); a browser-based POS needs `Cors.AllowedOrigins` (not in the default config); another program on port 5006 or the old v1 `empifisJson` service (same port and folders) blocks it – the installer warns about the latter; POS on another PC needs the firewall rule.
   The release workflow also builds and attaches the installer **once the secret `RECEIPTTESTER_TOKEN` exists** (fine-grained token, read access to the private kgiedris/ReceiptTester contents; the user has to create it). Without it: zip only. "Run workflow" on release.yml builds the files as artifacts without a release.
3. Pilot 2.3.2 at the customer where EmpiFis got stuck; afterwards check `json2.log`/archives for "was slow", "timed out", "Ending the process", and the Windows Application log.
4. ~~Accept the manual's tracked changes~~ – done; `Cors.AllowedOrigins` (empty) added to the default config and to this PC's config.json.
5. Tell POS integrators: don't resend automatically on 555/556/557.
6. ~~Dependabot~~ – works; first PRs merged for 2.3.3 (NLog 6.0.3 → 6.2.1 – log rotation re-verified with the simulation, Newtonsoft.Json 13.0.4, test packages, actions/checkout v7, setup-dotnet v6). Before merging an NLog update, re-check the daily archive naming/cleanup.
   ReceiptTester 0.5 (in the 2.3.3 installer): Random batch weights Fiscal/Non-Fiscal 5, Z Report 0.5, others 1, and it always ends with a Z report.
7. Optional: replace the installed build in `C:\Altera\EmpifisJsonAPI` (same code, label `+c09e035`) with the release zip (`+38efed6`).
