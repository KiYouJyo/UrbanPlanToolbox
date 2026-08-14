# Performance governance

For v1.7.0, only deterministic governance is validated: package size, dependency inventory, and the native allowlist. Startup timing, idle memory, representative-tool loading, and every gate that requires controlling a real WinUI window are explicitly out of scope and are not release conditions. Heavy dependencies such as OpenCvSharp are initialized only when their feature is used; this is validated through source-level dependency governance rather than UI-driven measurement.
