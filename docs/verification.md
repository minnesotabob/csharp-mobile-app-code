# Part I verification — November 14, 2026 (Central)

## Passing evidence

| Check | Environment | Result |
|---|---|---|
| SDK inspection | Local Debian 13 | SDK 11.0.100 / runtime 11.0.0 |
| Portable Release build and tests | GitHub-hosted Windows | 37 tests, 0 errors, 0 failures, 0 skipped |
| Android Debug compilation | GitHub-hosted Windows, isolated SDK | Passed, final compile 0 warnings / 0 errors |
| JSON/project/XAML validation | Local static inspection | Passed previously; not runtime evidence |

Portable run: https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43672656910  
Portable source: bf6f2f55b57223d28ea1cf0181b289a362ba8d40  
Android run: https://github.com/minnesotabob/csharp-mobile-app-code/actions/runs/43672619935  
Android source: 45e17eb16bfc7b5cfd17e2ea3cebc48d3e8398d4

The portable commit only adjusts a test input after the Android commit; application/core source is identical between these two verification revisions.

## Recorded Android toolchain

- .NET SDK and workload set: 11.0.100.
- .NET runtime: 11.0.0.
- maui-android manifest: 11.0.0/11.0.100.
- Android workload manifest/pack: 37.2.29.
- Microsoft.Maui.Controls: 11.0.10.
- Microsoft OpenJDK: 21.0.11+10 LTS.
- Target framework: net11.0-android.
- Workflow installs its SDK under runner.temp to avoid using preinstalled preview workloads.

The dependency-install target emitted one warning about unresolved cmdline-tools;21.0. Final Android compilation completed with zero warnings/errors. This remains an environment-cleanup item; it is not concealed as a wholly warning-free setup.

## What was corrected

The initial Android attempt mixed stable MAUI packages with a runner preview workload and failed with CS1705 (Java.Interop version mismatch). The workflow now explicitly installs stable workload set 11.0.100 in an isolated SDK location. An intermediate workflow expression-placement error was also corrected before the successful run.

Catalog model JSON properties are explicitly required. Tests now distinguish omitted-property serialization errors from semantic invalid metadata and cover malformed JSON, empty arrays, caller stream ownership, and null streams. No local red/green execution is claimed for these additions.

## Still unverified

- Emulator and physical Android launch/interaction; no screenshot is presented as actual runtime evidence.
- iOS compilation, Xcode compatibility, signing, simulator/device launch.
- Accessibility on devices, keyboard/rotation behavior, process restart.
- MFA, backend, playback, payments, and public deployment: not implemented yet.

The local workload/test attempts were previously blocked by automatic review after attempted cloud metadata access. They were not retried; the isolated GitHub-hosted Windows workflows provide separate compilation/test evidence.
