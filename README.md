# ⚠️ Warning: AI-developed fork

This fork contains commits developed with AI assistance. Review the changes before relying on them outside a personal environment.

This is a personal fork of `shemanaev/jellyfin-plugin-media-cleaner`. It is not intended as criticism of the original developer or their work. I made this fork because I rely on this plugin to manage limited storage space, and the upstream project currently has unresolved issues that affect my setup.

The main reasons for this fork are:

* A critical compatibility issue in current Jellyfin versions. Upstream PR #102 documents that Jellyfin 10.11.x removed `IUserManager.Users`, causing the played media cleanup task to crash at runtime with `MissingMethodException`. This fork includes compatibility handling for current Jellyfin.
* Missing Leaving Soon functionality. Upstream PR #91, opened on 19 December 2025, adds a Leaving Soon collection for issue #68 and issue #86, but it has not been merged upstream.
* A playback safety fix from upstream PR #104, which protects media that are currently being watched from being treated as deletion candidates in the rolling watched mode.

I am concerned that upstream may now be inactive: upstream `master` was last pushed on 19 December 2025, while the PRs above remain open as of 30 May 2026. This fork exists to keep the plugin usable for my own storage-management needs.

Enhancements in this fork include:

* Deletion of Arr-managed media through Radarr and Sonarr where possible, rather than direct Jellyfin deletion. This gives a cleaner end-to-end process, including Arr-side file deletion and import exclusion or monitoring behaviour.
* Updated Leaving Soon support that keeps the existing Jellyfin collection for normal clients and adds a read-only admin dashboard view.
* Dry-run handling that avoids mutating the Leaving Soon collection.
* Compatibility and safety fixes from the open upstream PRs noted above.
* Configurable date formatting, for the Troubleshooting log - defaults to `YYYYMMDD`.

<table>
  <tr>
    <td width="33%" align="center">
      <img src="https://github.com/user-attachments/assets/f68422c4-1870-4d24-aa25-de3fe7a8705a" alt="Screenshot 1" height="260">
    </td>
    <td width="33%" align="center">
      <img src="https://github.com/user-attachments/assets/d759791e-cc60-4e40-9a0e-4418d5cd6e01" alt="Screenshot 2" height="260">
    </td>
    <td width="33%" align="center">
      <img src="https://github.com/user-attachments/assets/9c9ecf9b-31a9-4318-8757-ee1f3ca86e46" alt="Screenshot 3" height="260">
    </td>
  </tr>
  <tr>
    <td width="33%" align="center">
      <img src="https://github.com/user-attachments/assets/5668382d-f9ea-41fe-820d-ca28fc4950db" alt="Screenshot 4" height="260">
    </td>
    <td width="33%" align="center">
      <img src="https://github.com/user-attachments/assets/f83f8b5e-68d7-4b63-85d7-32255e967a9a" alt="Screenshot 5" height="260">
    </td>
    <td width="33%" align="center">
      <img src="https://github.com/user-attachments/assets/130b66ba-a60e-4351-80bb-a209862547c8" alt="Screenshot 6" height="260">
    </td>
  </tr>
  <tr>
    <td colspan="3" align="center">
      <img src="https://github.com/user-attachments/assets/b7c2864f-7a5b-4bb5-b859-04361b61247d" alt="Screenshot 7" height="260">
    </td>
  </tr>
</table>

<img width="1913" height="905" alt="image" src="https://github.com/user-attachments/assets/a479fe36-f466-41b2-96bb-f59e6f527594" />

<div style="page-break-after: always;"></div>

# Media Cleaner for Jellyfin

Automatically delete played media files after specified amount of time. Works for movies and series.

## Installation

Add this plugin repository URL in Jellyfin:

```text
https://raw.githubusercontent.com/sym0nd0/jellyfin-plugin-media-cleaner/master/manifest.json
```

1. Open Jellyfin Dashboard.
2. Go to `Plugins`.
3. Go to `Repositories`.
4. Add the repository URL above.
5. Save.
6. Go to `Catalogue`.
7. Find `Media Cleaner`.
8. Install it.
9. Restart Jellyfin if prompted.

Raise issues for this fork in this repository. Pull requests are welcome.

<br>

## Configuration

Configuration is pretty straightforward at plugin's page.
Here's not so obvious things:

* Media will be considered for deletion after fully played by at least ONE user. To keep things you want use favourites with corresponding settings.
* Favourite episodes aren't kept when "*Delete after played*" set to season/series.
* All actions taken will be displayed at the Alerts in Dashboard.

For the correct operation of the "*Delete not played items*" function, there are two possible configuration options for "*Date added behaviour for new content*" in Jellyfin. Each option has its own drawbacks, depending on your setup:

  1. Leave it at the default value ("*Use file creation date*")
      - recently downloaded files can be deleted on the first cleanup run if your software (Radarr, download client, etc...) modifies the file creation date (like sets it from metadata of download or something).
  3. Change it to "*Use date scanned into the library*"
      - when using software that can update files at any time (Radarr, etc...), it is possible that the file will be updated after being played, and thus the creation date in Jellyfin will also be updated. It will not be deleted because the creation date will be later than the watch date.

<img width="1174" height="724" alt="image" src="https://github.com/user-attachments/assets/9368c966-880c-47c8-8184-d9fa9184d946" />

## Debugging

Define `JellyfinHome` environment variable pointing to Jellyfin distribution to be able to run debug configuration.
