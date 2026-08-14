# Updater freeze contract

After v1.7.0 validation, runtime updater behavior is frozen. `UpdateViewModel` owns the application-scoped update session; `AboutPage` attaches on `Loaded`, detaches on `Unloaded`, and owns no update cancellation token.

GitHub retains its verified discovery, checksum, signature, deployment, and restart path. Store `Completed` always maps to `RestartRequired`. Store is freeze-ready, not fully frozen, until a real Store N to v1.7.0 update succeeds.
