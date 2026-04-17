# FileCourier - System Architecture

## Global Source Code Hierarchy

```text
FileCourier/
├── Design/                    # Product specs, mockups
├── src/                       # Main source code directory
│   ├── platform-windows/      # C# / .NET Workspace
│   │   ├── FileCourier.Core/  # Class Library: UDP / TCP logic, ViewModels, TrustStore
│   │   └── FileCourier.WinUI/ # Front-end presentation application
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
Sent as part of the TCP data stream before the file chunks begin. The payload can contain an array of `Files`, a `TextPayload` string (for clipboard/text sharing), or both.
`[HeaderLength: 4 Bytes (Int32)]` + `[Header JSON Bytes]` + `[File Data Stream]`

*Note on File Data Stream:* To ensure integrity and allow for fine-grained resumption, the raw file data is broken into smaller chunks (e.g., 4MB). Each chunk is prefixed with its size and a checksum, allowing the receiver to verify and acknowledge each chunk individually. If a chunk's checksum fails, only that chunk is re-transmitted. (If the transfer is purely a `TextPayload` and `Files` is empty/null, the `[File Data Stream]` is omitted).

```json
{
  "SchemaVersion": 2,
  "SenderId": "123e4567-e89b-12d3-a456-426614174000",
  "IsEncrypted": true,
  "TextPayload": "Here is that link you wanted: https://example.com",
  "Files": [
    {
      "FileName": "vacation.jpg",
      "RelativePath": "Photos/vacation.jpg",
      "FileSize": 1048576,
      "ByteOffset": 0
    },
    {
      "FileName": "report.pdf",
      "RelativePath": "Documents/report.pdf",
      "FileSize": 2048000,
      "ByteOffset": 500000
    }
  ]
}
```

### Transfer Cancellation (TCP)
To cancel a transfer mid-stream, the terminating peer gracefully closes the TCP connection. The receiving peer handles the disconnection by keeping all files that were fully received prior to the connection loss, and discarding any currently downloading, partially written file.

### Transfer Response Header (TCP)
Sent by the receiver back to the sender before any raw bytes are streamed, establishing a handshake. If rejected, the TCP socket is closed.

```json
{
  "SchemaVersion": 1,
  "Status": "Accepted", 
  "Reason": "" 
}
```

### Encryption Implementation (TCP)
If `"IsEncrypted": true` is specified, the devices perform an **Elliptic-Curve Diffie-Hellman (ECDH)** key exchange over the TCP socket *before* sending the `Transfer Request Header` to establish a shared secret. All subsequent data (headers and file chunks) is symmetrically encrypted using **AES-256-GCM**.
