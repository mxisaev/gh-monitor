# GH Monitor (DeployTray)

**Keep an eye on GitHub Actions without keeping GitHub open.**

GH Monitor is a small Windows 10/11 system-tray utility that shows your running GitHub Actions workflows at a glance. It's especially handy when you trigger builds or deployments and move on to something else: a quick look at the taskbar tells you what's happening.

![GH Monitor in the Windows system tray](docs/screenshot.png)

## What it does

- **One icon per active run.** Each queued or running workflow gets its own tray icon, labeled with the first three letters of its run title.
- **Status at a glance.** Gray letters mean queued/waiting; yellow means in progress.
- **Recent success indicator.** The main **GH** icon turns green when a matching workflow has completed successfully within the last five minutes.
- **Connection errors are visible.** The GH icon turns red if GitHub cannot be checked.
- **Monitor multiple repositories.** Add as many `owner/repo` entries as you need.
- **Optional workflow name filter.** Monitor everything or only workflows whose names contain a specific string.
- **Open a run quickly.** Double-click its icon to open the run on GitHub.
- **Quiet background operation.** Runs in the tray, with manual refresh and settings available from the context menu.

Completed runs disappear from the list of active-run icons. The app checks GitHub approximately every 15 seconds.

## Requirements

- Windows 10 or 11
- Windows PowerShell and the built-in .NET Framework (no separate .NET SDK needed)
- [GitHub CLI](https://cli.github.com/) (`gh`) with access to the repositories you want to monitor

## Get started

1. Download or clone this repository to a folder you want to keep.
2. Install GitHub CLI and sign in:

   ```powershell
   winget install --id GitHub.cli --exact
   gh auth login
   ```

   Open a new terminal after installing `gh`.

3. From the project folder, check the prerequisites:

   ```powershell
   .\gh-monitor.cmd --check
   ```

4. Start the tray app:

   ```powershell
   .\gh-monitor.cmd
   ```

   You can also double-click `gh-monitor.cmd` or `Start.vbs` in File Explorer.

5. In the settings window, enter repositories one per line, for example:

   ```text
   your-name/your-app
   your-org/your-service
   ```

   Leave the workflow-name filter empty to watch all workflows, or enter part of a workflow name (case-insensitive). Click **Save**.

The app continues running after the settings window closes. Look for the **GH** icon in the Windows notification area (possibly under the `^` arrow).

## Tray icon colors

| Icon | Meaning |
| --- | --- |
| Gray workflow initials | Queued, waiting, pending, or requested |
| Yellow workflow initials | In progress |
| Green **GH** | At least one matching workflow succeeded in the last five minutes |
| Red **GH** | GitHub check failed or connection error |
| Default **GH** | Monitoring, with no recent success indication |

Right-click the **GH** icon for **Settings**, **Refresh**, and **Exit**. Right-click a workflow icon for **Open workflow**. Double-clicking a workflow icon opens that run in your browser.

## Start with Windows

Press `Win + R`, enter `shell:startup`, and place a shortcut to `Start.vbs` in that folder.

## Configuration and authentication

Settings are saved in `config.json` alongside the project files. This file is excluded from Git. GH Monitor uses your existing GitHub CLI authentication and does **not** save a GitHub token in its own configuration.

For troubleshooting, run:

```powershell
.\gh-monitor.cmd --console
```

The console stays open until you exit the app. You can also verify your GitHub CLI session with `gh auth status`.

## How it works

The app uses Windows PowerShell to load a small C# Windows Forms tray application. It queries GitHub Actions through the GitHub CLI and creates native Windows notification-area icons. No browser tab or background web server is required.

See [README.txt](README.txt) for additional technical notes.
