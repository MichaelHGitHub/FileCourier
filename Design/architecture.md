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
