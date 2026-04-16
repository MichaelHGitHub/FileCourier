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
Broadcast over UDP to announce presence.

```json
{
  "SchemaVersion": 1,
  "DeviceId": "123e4567-e89b-12d3-a456-426614174000",
  "DeviceName": "Alice's MacBook",
  "OS": "macOS",
  "TcpPort": 45000
}
```

### Transfer Request Header (TCP)
Sent as part of the TCP data stream before the raw file chunks begin.
`[HeaderLength: 4 Bytes (Int32)]` + `[Header JSON Bytes]` + `[Raw File Bytes]`

```json
{
  "SchemaVersion": 1,
  "SenderId": "123e4567-e89b-12d3-a456-426614174000",
  "FileName": "vacation.jpg",
  "FileSize": 1048576,
  "IsEncrypted": true
}
```
