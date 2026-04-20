# FileCourier - System Architecture

## Global Source Code Hierarchy

```text
FileCourier/
├── Design/                    # Product specs, mockups
├── src/                       # Main source code directory
│   ├── platform-windows/      # C# / .NET Workspace
│   │   ├── FileCourier.Core/  # Class Library: UDP / TCP logic, ViewModels, TrustStore
│   │   └── FileCourier.WinUI/ # Front-end (WinUI 3 unpackaged, custom Program.cs)
│   ├── platform-apple/        # Swift / Xcode Workspace
│   │   ├── SharedCore/        # Swift Package: Local networking logic
│   │   ├── FileCourier-macOS/ # SwiftUI frontend for Mac
│   │   └── FileCourier-iOS/   # SwiftUI frontend for iPhone
│   └── platform-android/      # Kotlin / Android Studio Workspace
│       ├── core-network/      # Kotlin module handling sockets
│       └── app/               # Jetpack Compose UI
├── tests/                     # Global Test directory 
│   ├── FileCourier.Core.Tests/# C# unit tests for Windows/Core logic
│   ├── apple-tests/           # XCTest suites for Swift
│   └── android-tests/         # JUnit suites for Kotlin
└── docs/                      # General API / protocol documentation
```

## 1. Local Persistence & History
The application uses a local SQLite database (`TransferHistoryStore`) to track file transactions. To ensure data integrity and a clean user experience:
*   **Per-File Records**: In a batch transfer, each file is assigned its own unique `TransferId` (UUID) in the database. This allows for individual status tracking, retries, and "Reveal in Explorer" actions.
*   **Directional Isolation**: The store supports filtering and clearing by `TransferDirection` (Sent vs Received). This allows the UI to maintain independent history tabs and clear them separately.
*   **Persistence Rules**: 
    *   **Sent Files**: All sent files are recorded, regardless of success, to allow for retries.
    *   **Received Files**: To prevent clutter and ensure privacy, only files that are successfully received and written to disk are recorded in the history database. Failed or aborted incoming transfers are not persisted.

## 1.5 Developer Information
*   **Developer**: PandaSoft
*   **Version**: 1.0.0

## 2. Network Protocol Schemas

### Discovery Payload (UDP)
Broadcast over UDP to announce presence. Devices send a "Heartbeat" payload every 3 seconds to remain visible to peers. If the app is closed gracefully, it broadcasts with `"IsGoodbye": true` so peers can remove it immediately. To ensure visibility on complex networks, this payload must be broadcast across **all** active local network interfaces (e.g., Wi-Fi, Ethernet, VPN adapters). If UDP broadcasts are entirely blocked by the network (e.g., due to AP Isolation), the discovery phase can be bypassed by manually entering the target device's IP address and Port to proceed directly to the TCP Handshake.

```json
{
  "SchemaVersion": 1,
  "DeviceId": "123e4567-e89b-12d3-a456-426614174000",
  "DeviceName": "Alice's MacBook",
  "OS": "macOS",
  "TcpPort": 45000,
  "IsGoodbye": false
}
```

### Transfer Request Header (TCP)
Sent by the sender at the start of every session. 

**Device Identification**:
To ensure a consistent identity across sessions, each installation generates a unique `DeviceId` (GUID) on first run, which is persisted in `settings.json`. This ID is used for both UDP discovery and TCP handshakes.

```json
{
  "SchemaVersion": 2,
  "SenderId": "uuid-v4-string",
  "SenderName": "Device Name (e.g. My-PC)",
  "TextPayload": "Optional message content",
  "Files": [
    {
      "FileName": "photo.jpg",
      "RelativePath": "photos/photo.jpg",
      "FileSize": 1048576,
      "ByteOffset": 0
    }
  ]
}
```

### Transfer Cancellation (TCP)
To cancel a transfer mid-stream, the terminating peer gracefully closes the TCP connection. The receiving peer handles the disconnection by keeping all files that were fully received prior to the connection loss, and discarding any currently downloading, partially written file.

### Individual File Failure Handling (TCP)
To ensure robustness during multi-file batches, the TCP protocol includes a granular event system. 
*   **Event Reporting**: The core networking service fires `FileTransferCompleted` and `FileTransferFailed` events for every individual file in a batch. This allows the UI to maintain a real-time **Status Column** for each item in the selection list.
*   **Sender Failure**: If the sender fails to read a file (e.g. file is locked), it sends a `-1` chunk length signal. The receiver detects this and skips to the next file. The sender UI marks that specific file as **Failed** and adds it to history for future retry.
*   **Receiver Failure**: If the receiver encounters a disk error, it marks the file as **Failed**. Per history rules, failed incoming files are **not** saved to the permanent history log to ensure the list only contains valid local files.
*   **Real-time UI Sync**: The `HistoryViewModel` implements `IDisposable` and subscribes to the global `TcpTransferService` events (`TransferCompleted`, `TransferFailed`). This allows the history list to refresh automatically in the background as soon as a network session terminates.

### Transfer Response Header (TCP)
Sent by the receiver back to the sender before any raw bytes are streamed, establishing a handshake. 

**Asynchronous Handshake Implementation**:
Unlike traditional synchronous events, the `IncomingTransferRequested` event is now asynchronous. The networking thread creates a `TaskCompletionSource<bool>` and awaits a signal from the UI.
*   The UI (typically a global `ReceiverViewModel` singleton in the `MainWindow`) prompts the user via a `ContentDialog`.
*   The handshake pauses for up to **60 seconds** to allow for user interaction.
*   If accepted, the receiver returns an `Accepted` status; otherwise, it returns `Rejected` and closes the socket.

```json
{
  "SchemaVersion": 1,
  "Status": "Accepted", 
  "Reason": "" 
}
```

