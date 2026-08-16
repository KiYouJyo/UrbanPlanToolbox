[简体中文](RELEASE-NOTES-v1.8.2.md) | [日本語](RELEASE-NOTES-v1.8.2.ja.md) | English

# UrbanPlanToolbox v1.8.2 WebDAV cloud archive

- Extends Settings > Data management with WebDAV cloud archives while keeping local storage authoritative; loss of network access does not block local projects or tools.
- Reuses the existing `.uptbackup` format. Backups are generated and validated locally before upload, then uploaded to a temporary remote file and finalized with `MOVE` so interrupted uploads are not presented as complete archives.
- Adds WebDAV connection setup and testing, manual cloud archive creation, archive history, safe cloud restore, and deletion of an explicitly selected remote archive.
- Cloud restore keeps the existing manifest, SHA-256, format-version, pre-import safety backup, and rollback checks.
- Stores the WebDAV password with Windows Credential Locker and does not write it to `settings.json`, `.uptbackup`, or logs. HTTP connections show a warning recommending HTTPS.
- Clear all local data removes the local WebDAV profile and credential but does not automatically delete remote archives.
- Places the existing Export backup, Import backup, and Clear all local data buttons in one horizontal row to reduce vertical space in the Data management card.
