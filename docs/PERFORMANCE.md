# Performance governance

This release validates only deterministic performance governance: package size, dependency inventory, and native allowlist metadata. Startup timing, idle memory, representative-tool loading, and any other gate that needs control of a real WinUI window are explicitly out of scope for v1.7.0 and are not release conditions.

Heavy dependencies, including OpenCvSharp, must remain feature-triggered and must not be initialized by application startup, `MainWindow`, or `HomePage`; this is enforced by source-level dependency governance rather than a UI-driven measurement gate.
