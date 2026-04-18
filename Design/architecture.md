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
  "IsEncrypted": false,
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

### Encryption Implementation (TCP)
If `"IsEncrypted": true` is specified, the devices perform an **Elliptic-Curve Diffie-Hellman (ECDH)** key exchange over the TCP socket *before* sending the `Transfer Request Header` to establish a shared secret. All subsequent data (headers and file chunks) is symmetrically encrypted using **AES-256-GCM**.
