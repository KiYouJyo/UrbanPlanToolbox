# Updater freeze contract

The runtime updater is behavior-frozen after v1.7.0 validation. `UpdateViewModel` is the application-scoped update session; `AboutPage` attaches on `Loaded`, detaches on `Unloaded`, and never owns an update cancellation token.

GitHub keeps its verified discovery, SHA-256, signature, deployment, and restart path. Store keeps `Completed` mapped to `RestartRequired`; `Completed` never means that the application has no remaining user action. Navigation must preserve checking, download progress, localized notes, target version, source, failure, and final restart state without starting a second operation.

The GitHub updater is validated and frozen. The Store updater is freeze-ready, not fully frozen, until a real Store baseline N to v1.7.0 update proves the same navigation behavior.
