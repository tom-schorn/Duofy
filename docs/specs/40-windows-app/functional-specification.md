# Functional specification: Windows app on Tom's PC and Android app on Tom's phone

Format: 2

## Summary

The team builds the current main as a signed MSIX test package for Windows and as an Android release APK, installs both on the human's devices and writes down how to repeat it. Nothing in the app's behaviour changes; only build settings and a guide are added where they are missing.

## Customer requirements

[customer-requirements.md](customer-requirements.md), R1 to R5. R6 is out of scope.

## Components

None.

## Solution

- **Windows**: build `Mobile/Duofy` for `net10.0-windows10.0.19041.0` in Release with `DuofyStorePackage=true` (MSIX). A self-made code-signing test certificate (subject matching the package publisher of the local test identity) signs the package. The certificate's private key stays outside the repository (in the user's certificate store or the worker's temporary folder); only the public `.cer` and a script may be committed, never a `.pfx` or password.
- **Install**: the human imports the `.cer` into "Trusted People" of the local machine (administrator step, done by the human), then installs the `.msix`; it appears in the start menu as Duofy or Plagevo as the app's name setting says.
- **Server address**: the app is pointed at the live Plagevo server through its existing server address setting; no address is hard-coded in a new place.
- **Android**: release build of `net10.0-android` signed with the existing upload or release key as configured; the APK is installed on the human's phone (by `adb install` when the phone is connected, otherwise the human installs the file himself).
- **Guide**: `docs/app-installieren.md` in German, step by step for both apps, including how to uninstall and how to renew the test certificate.
- Builds run one after the other, never at the same time.

## Behaviour

- R1: The MSIX is signed by the test certificate; installing it adds one start menu entry; uninstalling removes app and entry. An unsigned or wrongly signed package is not installed.
- R2: On first start the app asks for or uses the server address of Plagevo; sign-in with the human's account opens his plan. Without network the app shows its existing offline message.
- R3: The APK installs over an earlier Duofy installation when it is signed with the same key; if the key differs, the guide says that the old app must be removed first (data is on the server).
- R4: The version shown in the app's licence or about page names the commit of main it was built from, or the guide records it.
- R5: The guide names every command and every manual step in order.

## Data and interfaces

New files: `docs/app-installieren.md`, a script under `Mobile/` to create the test certificate and sign the package, optionally the public `.cer`. Changed: build properties in `Mobile/Duofy/Duofy.csproj` only where the MSIX build or the release build fails without them.

External input: no

## Acceptance criteria

- [ ] A1 (R1): Given the release build, when `Get-AuthenticodeSignature` checks the produced `.msix`, then the status is `Valid` against the trusted test certificate and the signer is that certificate.
- [ ] A2 (R1): Given the installed package, when `Get-AppxPackage` lists packages, then exactly one Duofy package with the built version is listed; after `Remove-AppxPackage` it is no longer listed.
- [ ] A3 (R2): Given the installed Windows app, when it is started, then its process runs for at least 30 seconds without a crash entry in the Windows event log, and a request from the app's API client to the Plagevo health endpoint returns success.
- [ ] A4 (R3): Given the Android release build, when `apksigner verify` checks the APK, then the verification passes; when `adb shell pm list packages` runs on the connected phone after installing, then `de.tomschorn.duofy` is listed.
- [ ] A5 (R4): Given both builds, when their source commit is compared with `main`, then it is the head of main at build time (recorded in the report and the guide).
- [ ] A6 (R5): Given `docs/app-installieren.md`, when it is checked, then it contains sections for Windows build, certificate, install, uninstall, Android build and Android install.
- [ ] A7 (R1): Given the repository after the change, when it is searched for `.pfx` files and certificate passwords, then none are found.
- [ ] A8 (R1, R3): Given the full test suite, when `dotnet test Duofy.slnx` runs after the change, then it is as green as before the change.

## Tracing

| Requirement | Criterion | Test |
| --- | --- | --- |
| R1 | A1, A2, A7, A8 | Get-AuthenticodeSignature (Valid), Get-AppxPackage / Remove-AppxPackage, search for .pfx, dotnet test Duofy.slnx, StoreFilesTests (manifest keeps its placeholders) |
| R2 | A3 | App process ran 40 s with Responding=True, no error entry in the Application log, GET /api/v1/health returned 200 Healthy |
| R3 | A4, A8 | apksigner verify, adb install -r (Success), adb shell pm list packages, dotnet test Duofy.slnx |
| R4 | A5 | Build commit c57676a = head of local main (recorded in docs/app-installieren.md) |
| R5 | A6 | docs/app-installieren.md has sections 1 to 6 for build, certificate, install, uninstall, Android build and Android install |

## Non-goals

- No submission to the Microsoft Store or Google Play, no Partner Center values in the manifest.
- No change to app behaviour, interface or backend; findings in the interface go to the parking lot.
- No automatic updates, no installer other than MSIX, no iOS or Mac.
- No code-signing certificate from a certificate authority.

## Risks

- The MSIX build may fail on settings that were never exercised since #82: contained by fixing only build properties, anything else is reported as a blocker.
- Memory: a Windows and an Android build at the same time can starve the PC; contained by building one after the other with no other heavy runs.
- A different Android signing key forces removing the old app first; data stays on the server.

## Open questions

None.

## Approval

Approved by the human on 2026-11-04, after the kickoff of 2026-11-04 (packaging: MSIX with a test certificate).
