# Spicetify Marketplace Installer

### [Turkish Version](https://github.com/s0rp/SpicetifyMarketPlaceInstaller/blob/main/tr.md)

This is a command-line utility built in C# to automate the installation of [Spicetify](https://github.com/spicetify/cli) & [Spicetify Marketplace](https://github.com/spicetify/marketplace) along with the Spicetify CLI itself if it's not already installed. It aims to provide a smoother setup experience, especially for users who might encounter issues with manual installation steps (also --bypass-admin flag).

## What it Does

The installer performs the following actions:

1.  **Checks for Spicetify CLI**: Verifies if Spicetify CLI is installed and accessible.
2.  **Installs Spicetify CLI (if needed)**: If Spicetify CLI is not found, it downloads and runs the official PowerShell installation script. It automatically handles prompts during this installation.
3.  **Determines Spicetify Paths**: Finds the necessary Spicetify `userdata` directory, with a fallback mechanism.
4.  **Downloads Marketplace**: Fetches the latest release of Spicetify Marketplace (`marketplace.zip`) from GitHub.
5.  **Installs Marketplace**:
    *   Clears any previous Marketplace installation in `CustomApps/marketplace` and `Themes/marketplace`.
    *   Extracts the downloaded files into the `CustomApps/marketplace` directory.
    *   Intelligently handles common archive extraction structures (e.g., moving files from a nested `marketplace-dist` folder).
    *   Downloads and installs the `marketplace` placeholder theme (`color.ini`) required for Marketplace theme functionality into `Themes/marketplace`.
6.  **Configures Spicetify**:
    *   Sets necessary Spicetify configurations (`inject_css=1`, `replace_colors=1`, `custom_apps=marketplace`).
    *   If an existing theme is active, it prompts whether to switch to the 'marketplace' theme (recommended).
    *   Applies the changes by running `spicetify backup` and `spicetify apply`.
7.  **Logging**: Creates a detailed `log.txt` file in the same directory as the executable, recording all steps and any errors encountered.
8.  **User Options**: Supports command-line arguments for advanced control and interactive prompts for verification.

## Features

*   **Automated Spicetify CLI Installation**: No need to manually install Spicetify CLI first.
*   **Latest Marketplace Version**: Always downloads the newest release of Marketplace.
*   **Force Reinstall Option**: Allows for a clean slate reinstallation if issues occur, including cleaning Spicetify data folders.
*   **Admin Bypass Support**: Can utilize Spicetify's `--bypass-admin` flag automatically if the installer is run with admin rights or the flag is provided.
*   **Localization**: Console output in English or Turkish.
*   **Detailed Logging**: Comprehensive `log.txt` for troubleshooting.

## Prerequisites

*   **Windows Operating System**: The installer is primarily designed for Windows (due to Spicetify's nature and PowerShell usage).
*   **.NET Runtime**: To run the compiled `.exe` file, you'll need a compatible .NET Runtime installed (.NET 5.0 or later, depending on compilation).
*   **PowerShell**: Required for the Spicetify CLI installation script. Usually available by default on Windows.
*   **Internet Connection**: To download Spicetify CLI and Marketplace.

## How to Use

1.  **Download**: Obtain the compiled `.exe` file of this installer.
2.  **Run**: Execute the `.exe` file from a command prompt or by double-clicking it.
    ```bash
    SpicetifyMarketplaceInstaller.exe
    ```

### Command-Line Arguments:

*   `-f` or `--forcereinstall`:
    *   Performs a "force reinstall". This will:
        1.  Attempt to clean existing Spicetify data (runs `spicetify restore` and deletes common Spicetify folders like `%APPDATA%/spicetify`, `%LOCALAPPDATA%/spicetify`, `~/.spicetify`).
        2.  Proceed with a fresh installation of Spicetify CLI (if needed) and Marketplace.
    *   Useful if a previous installation attempt failed or if Marketplace is not working correctly.

*   `--bypass-admin` (or aliases `-a`, `-b`):
    *   If this flag is provided, OR if the installer is run with Administrator privileges, it will automatically pass the `--bypass-admin` flag to all `spicetify` commands it executes.
    *   This can help resolve permission-related issues when Spicetify tries to modify Spotify files.

**Example Usage:**

*   Standard installation:
    ```bash
    SpicetifyMarketplaceInstaller.exe
    ```
*   Force reinstall:
    ```bash
    SpicetifyMarketplaceInstaller.exe -f
    ```
*   Run with admin bypass (if you are not already running the installer as an admin but want Spicetify to use its bypass):
    ```bash
    SpicetifyMarketplaceInstaller.exe --bypass-admin
    ```
    (Note: If you run `SpicetifyMarketplaceInstaller.exe` as an Administrator, the `--bypass-admin` flag for Spicetify commands will be used automatically, even without explicitly providing the installer's `--bypass-admin` flag.)

## After Installation

1.  **Restart Spotify**: For all changes to take effect, you **MUST** completely restart Spotify. This means quitting it from the system tray (if it's running there) and then reopening it.
2.  **Check Marketplace**: Look for the "Marketplace" tab in the left sidebar of Spotify.

## Troubleshooting

*   **Check `log.txt`**: This file is created in the same directory as `SpicetifyMarketplaceInstaller.exe`. It contains detailed information about each step and any errors encountered. This is the first place to look if something goes wrong.
*   **Spotify Not Restarted Correctly**: Ensure Spotify was fully closed (quit from system tray) and reopened.
*   **Permissions**: If Spicetify commands fail, try running the installer as an Administrator. The installer will automatically try to use Spicetify's `--bypass-admin` functionality in this case.
*   **Antivirus/Firewall**: Ensure your security software is not blocking the installer, its network connections (for downloads), or PowerShell execution.
*   **Force Reinstall**: If issues persist, try running the installer with the `-f` or `--forcereinstall` flag.
