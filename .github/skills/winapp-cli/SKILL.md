---
name: winapp-cli
description: 'Windows App Development CLI (winapp) for building, packaging, running, testing, and deploying Windows applications. Use when asked to initialize Windows app projects, create MSIX packages, run packaged apps, automate UI, generate AppxManifest.xml, manage development certificates, add package identity for debugging, sign packages, or access Windows SDK build tools. Supports .NET, C++, Electron, Rust, Tauri, and cross-platform frameworks targeting Windows.'
---

# Windows App Development CLI

The Windows App Development CLI (`winapp`) is a command-line interface for managing Windows SDKs, MSIX packaging, running packaged apps, UI automation, generating app identity, manifests, certificates, and using build tools with any app framework. It bridges the gap between cross-platform development and Windows-native capabilities.

Current release guidance in this skill is based on **winapp CLI v0.3.0**.

## When to Use This Skill

Use this skill when you need to:

- Initialize a Windows app project with SDK setup, manifests, and certificates
- Create MSIX packages from application directories
- Pack and run a folder as a packaged app with `winapp run`
- Work with .NET projects directly, including `.csproj`-based setup
- Generate or manage AppxManifest.xml files
- Use manifest placeholders and qualified manifest names
- Create external catalogs
- Create and install development certificates for signing
- Inspect certificate metadata and export public `.cer` files
- Add package identity for debugging Windows APIs
- Sign MSIX packages or executables
- Automate Windows app UI with Microsoft UI Automation (UIA)
- Access Windows SDK build tools from any framework
- Build Windows apps using cross-platform frameworks (Electron, Rust, Tauri, Qt)
- Invoke Microsoft Store Developer CLI workflows through winapp's `store` subcommand
- Set up CI/CD pipelines for Windows app deployment
- Access Windows APIs that require package identity (notifications, Windows AI, shell integration)

## Prerequisites

- Windows 10 or later
- winapp CLI installed via one of these methods:
  - **MSIX installer (recommended)**: Download `winappcli_x64.msix` or `winappcli_arm64.msix` from [GitHub Releases](https://github.com/microsoft/WinAppCli/releases/latest)
  - **Standalone binaries**: Download `winappcli-x64.zip` or `winappcli-arm64.zip`, extract, and add `winapp.exe` to PATH
  - **NPM** (for Electron/NodeJS): Install the release-provided package, for example `npm install microsoft-winappcli.tgz`
  - **GitHub Actions/Azure DevOps**: Use [setup-WinAppCli](https://github.com/microsoft/setup-WinAppCli) action

## Core Capabilities

### 1. Project Initialization (`winapp init`)

Initialize a directory with required assets (manifest, certificates, libraries) for building a modern Windows app. Supports SDK installation modes: `stable`, `preview`, `experimental`, or `none`.

### 2. MSIX Packaging (`winapp pack`)

Create MSIX packages from prepared directories with optional signing, certificate generation, and self-contained deployment bundling.

### 3. Package Identity for Debugging (`winapp create-debug-identity`)

Add temporary package identity to executables for debugging Windows APIs that require identity (notifications, Windows AI, shell integration) without full packaging.

### 4. Pack and Run (`winapp run`)

Pack a folder and run the application as a packaged app in one command. Use this when you need to quickly launch packaged output with an AppxManifest without manually creating and installing an MSIX.

### 5. UI Automation (`winapp ui`)

Automate and inspect Windows application UI using Microsoft UI Automation (UIA). Key commands include `winapp ui list-windows`, `winapp ui inspect`, and `winapp ui click`.

### 6. Manifest Management (`winapp manifest`)

Generate AppxManifest.xml files and update image assets from source images, automatically creating all required sizes and aspect ratios. `manifest update-assets` supports SVG input and converts vector images into bitmap assets. Current releases also support manifest placeholders and qualified names in AppxManifest files.

### 7. Certificate Management (`winapp cert`)

Generate development certificates and install them to the local machine store for signing packages. Use `winapp cert info` to inspect PFX metadata, and use JSON output or `.cer` export options when integrating certificate workflows with automation.

### 8. Package Signing (`winapp sign`)

Sign MSIX packages and executables with PFX certificates, with optional timestamp server support.

### 9. SDK Build Tools Access (`winapp tool`)

Run Windows SDK build tools with properly configured paths from any framework or build system.

### 10. .NET Project Support

`winapp init` can configure .NET projects directly. When run against a `.csproj`, current versions update the project file with required packages/settings instead of creating `winapp.yaml`.

### 11. Store CLI Integration (`winapp store`)

Run Microsoft Store Developer CLI commands through winapp's `store` subcommand for integrated Store publishing workflows. For deeper Store-specific workflows, use the `msstore-cli` skill.

### 12. External Catalogs (`winapp create-external-catalog`)

Create an external catalog for package asset management when that packaging scenario is needed.

### 13. NodeJS/Electron API Surface

The npm package exposes generated typed JavaScript/TypeScript functions for winapp commands, such as `init`, `packageApp`, and `certGenerate`, so NodeJS/Electron tooling can call winapp without shelling out to raw command strings.

## Usage Examples

### Example 1: Initialize and Package a Windows App

```bash
# Initialize workspace with defaults
winapp init

# Build your application (framework-specific)
# ...

# Create signed MSIX package
winapp pack ./build-output --generate-cert --output MyApp.msix
```

### Example 2: Generate and Inspect a Certificate

```powershell
# Current winapp versions do not generate a certificate from init.
winapp cert generate --output .\devcert.pfx --password password --export-cer
winapp cert info .\devcert.pfx --password password --json
```

### Example 3: Debug with Package Identity

```bash
# Add debug identity to executable for testing Windows APIs
winapp create-debug-identity ./bin/MyApp.exe

# Run your app - it now has package identity
./bin/MyApp.exe
```

### Example 4: Pack and Run a Windows App

```powershell
# Pack and run a build output folder as a packaged app
winapp run .\bin\Debug\net10.0-windows10.0.26100.0\win-x64 --manifest .\appxmanifest.xml
```

### Example 5: UI Automation Smoke Test

```powershell
# Discover visible windows
winapp ui list-windows

# Inspect a window or element
winapp ui inspect

# Click a target UI element
winapp ui click
```

### Example 6: Generate Assets from SVG

```powershell
winapp manifest update-assets .\app-logo.svg
```

### Example 7: NodeJS/Electron Typed API

```typescript
import { init, packageApp, certGenerate } from '@microsoft/winappcli';

await init();
await certGenerate({ output: './devcert.pfx' });
await packageApp({ input: './out', output: './MyApp.msix' });
```

### Example 8: CI/CD Pipeline Setup

```yaml
# GitHub Actions example
- name: Setup winapp CLI
  uses: microsoft/setup-WinAppCli@v1

- name: Initialize and Package
  run: |
    winapp init --no-prompt
    winapp pack ./build-output --output MyApp.msix
```

### Example 9: Electron App Integration

```bash
# Install via npm
npm install microsoft-winappcli.tgz --save-dev

# Initialize and add debug identity for Electron
npx winapp init
npx winapp node add-electron-debug-identity

# Package for distribution
npx winapp pack ./out --output MyElectronApp.msix
```

## Guidelines

1. **Run `winapp init` first** - Always initialize your project before using other commands to ensure SDK setup, manifest, and certificates are configured.
2. **Re-run `create-debug-identity` after manifest changes** - Package identity must be recreated whenever AppxManifest.xml is modified.
3. **Use `--no-prompt` for CI/CD** - Prevents interactive prompts in automated pipelines by using default values.
4. **Use `winapp restore` for shared projects** - Recreates the exact environment state defined in `winapp.yaml` across machines.
5. **Generate assets from a single image** - Use `winapp manifest update-assets` with one logo to generate all required icon sizes.
6. **Use `winapp run` for quick packaged-app launches** - Prefer this over manually packing, installing, and launching when validating local build output.
7. **Use `winapp ui` for Windows UI automation** - Use `ui list-windows`, `ui inspect`, and `ui click` for smoke tests or interactive app validation.
8. **Generate certificates explicitly** - Since v0.2.0, `winapp init` no longer creates a development signing certificate. Add a `winapp cert generate` step when signing is needed.
9. **Expect NuGet global cache usage** - Since v0.2.0, downloaded NuGet packages go to the NuGet global cache instead of `%USERPROFILE%\.winapp\packages`.
10. **Treat .NET projects differently** - When `winapp init` detects a `.csproj`, it updates project files and does not create `winapp.yaml`.
11. **Keep signing material outside package input** - Packaging warns when `.pfx` files are found in the input folder; avoid shipping certificates inside app package contents.
12. **Use typed npm APIs when available** - Prefer generated JS/TS exports over hand-built command strings in Electron/NodeJS automation.

## Common Patterns

### Pattern: Initialize New Project

```bash
cd my-project
winapp init
# Creates/configures manifest and SDK settings.
# For .NET projects, updates the .csproj instead of creating winapp.yaml.
# Generate certificates explicitly with winapp cert generate.
```

### Pattern: Generate a Development Certificate

```powershell
winapp cert generate --output .\devcert.pfx --password password --export-cer --json
winapp cert info .\devcert.pfx --password password
```

### Pattern: Package with Existing Certificate

```bash
winapp pack ./build-output --cert ./mycert.pfx --cert-password secret --output MyApp.msix
```

### Pattern: Self-Contained Deployment

```bash
# Bundle Windows App SDK runtime with the package
winapp pack ./my-app --self-contained --generate-cert
```

### Pattern: Generate App Assets from SVG

```powershell
winapp manifest update-assets .\logo.svg
```

### Pattern: Pack and Run Local Output

```powershell
winapp run .\publish-output --manifest .\AppxManifest.xml
```

### Pattern: UI Automation

```powershell
winapp ui list-windows
winapp ui inspect
winapp ui click
```

### Pattern: Store CLI Integration

```powershell
winapp store --help
```

Use this for quick access to Store-oriented commands through winapp. For full Store submission workflows, prefer the `msstore-cli` skill.

### Pattern: External Catalog

```powershell
winapp create-external-catalog --help
```

### Pattern: Update Package Versions

```bash
# Update to latest stable SDKs
winapp update

# Or update to preview SDKs
winapp update --setup-sdks preview
```

## Limitations

- Windows 10 or later required (Windows-only CLI)
- Package identity debugging requires re-running `create-debug-identity` after any manifest changes
- `winapp init` does not generate certificates in current releases; call `winapp cert generate` explicitly
- `.NET` projects skip `winapp.yaml` and are configured through the project file
- Self-contained deployment increases package size by bundling the Windows App SDK runtime
- Development certificates are for testing only; production requires trusted certificates
- Some Windows APIs require specific capability declarations in the manifest
- winapp CLI is in public preview and subject to change

## Windows APIs Enabled by Package Identity

Package identity unlocks access to powerful Windows APIs:

| API Category | Examples |
| ------------ | -------- |
| **Notifications** | Interactive native notifications, notification management |
| **Windows AI** | On-device LLM, text/image AI APIs (Phi Silica, Windows ML) |
| **Shell Integration** | Explorer, Taskbar, Share sheet integration |
| **Protocol Handlers** | Custom URI schemes (`yourapp://`) |
| **Device Access** | Camera, microphone, location (with consent) |
| **Background Tasks** | Run when app is closed |
| **File Associations** | Open file types with your app |

## Troubleshooting

| Issue | Solution |
| ----- | -------- |
| Certificate not trusted | Run `winapp cert install <cert-path>` to install to local machine store |
| Package identity not working | Run `winapp create-debug-identity` after any manifest changes |
| SDK not found | Run `winapp restore` or `winapp update` to ensure SDKs are installed |
| Signing fails | Verify certificate password and ensure cert is not expired |
| `winapp init` did not create a certificate | This is expected since v0.2.0; run `winapp cert generate` explicitly |
| Expected `%USERPROFILE%\.winapp\packages` cache content | Current versions use the NuGet global package cache |
| Missing `winapp.yaml` in a .NET project | Expected for `.csproj` projects; winapp configures the project file directly |
| `.pfx` warning during packaging | Move signing certificates outside the package input folder |
| `get-winapp-path` local cache missing | In v0.3.0 the CLI falls back to the global cache; update winapp if this still fails |

## References

- [GitHub Repository](https://github.com/microsoft/WinAppCli)
- [v0.3.0 Release Notes](https://github.com/microsoft/winappCli/releases/tag/v0.3.0)
- [v0.2.1 Release Notes](https://github.com/microsoft/winappCli/releases/tag/v0.2.1)
- [v0.2.0 Release Notes](https://github.com/microsoft/winappCli/releases/tag/v0.2.0)
- [Full CLI Documentation](https://github.com/microsoft/WinAppCli/blob/main/docs/usage.md)
- [Sample Applications](https://github.com/microsoft/WinAppCli/tree/main/samples)
- [Windows App SDK](https://learn.microsoft.com/windows/apps/windows-app-sdk/)
- [MSIX Packaging Overview](https://learn.microsoft.com/windows/msix/overview)
- [Package Identity Overview](https://learn.microsoft.com/windows/apps/desktop/modernize/package-identity-overview)
