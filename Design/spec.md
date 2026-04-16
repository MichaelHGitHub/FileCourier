# FileCourier - Product Specification

## 1. Product Overview
FileCourier is a cross-platform application designed for fast, seamless file transfers over a Local Area Network (LAN). The initial target platform is Windows, with future support planned for macOS, Android, and iOS. The app works peer-to-peer (P2P), requiring no internet connection or external servers, ensuring privacy and maximizing transfer speeds.

## 2. Technical Architecture Suggestion
Since you prefer maintaining different source code suited natively for each platform, here are the best native solutions:

*   **Windows**: 
    *   **C# with WinUI 3 (Windows App SDK)**: The modern standard for native Windows desktop apps. It offers Fluent Design aesthetics out-of-the-box and excellent access to low-level TCP/UDP socket programming.
*   **macOS**:
    *   **Swift with SwiftUI**: Apple's modern UI framework. It gives you the best performance and integrates seamlessly with Apple's `Network` framework for local Bonjour (mDNS) discovery.
*   **iOS**:
    *   **Swift with SwiftUI**: By using SwiftUI, you can share the vast majority of your source code (both UI and networking) with your macOS app while remaining completely native.
*   **Android**:
    *   **Kotlin with Jetpack Compose**: The modern, native standard for Android development. Kotlin is powerful for background UDP/TCP services, and Compose is perfect for reactive UI (like progress bars).

*   **Core Shared Protocol (Crucial)**:
    *   Even though the apps are built in different languages, they **must** all speak the exact same language over the network.
    *   **Discovery**: UDP Broadcast / Multicast DNS (mDNS/Bonjour) must be implemented identically across all platforms so they can "see" each other.
    *   **Transfer**: Direct TCP socket connections handling a standardized payload (e.g., a 4-byte message length, followed by a JSON header, followed by raw file bytes).

## 3. Core Features
1.  **Zero-Config Discovery**: Instantly see other devices running the app on the same Wi-Fi/LAN without entering IP addresses.
2.  **Cross-Platform File Transfer**: Send any file type or size, bounded only by local network speeds.
3.  **Trust Management**: Remember frequently used devices to bypass manual approval prompts.
4.  **Transfer Progress**: Real-time visualization of transfer speed, ETA, and progress bar.
5.  **Multi-Language Localization (i18n)**: The GUI will be fully translatable, adapting automatically based on system preferences by extracting text into native resource files (e.g., `.resw` on Windows, `Localizable.strings` on macOS/iOS, and `strings.xml` on Android).

## 4. Detailed User Flows / Scenarios

### Scenario 1: Sending a File
*   **Action**: Device A opens the FileCourier app. The main UI displays a radar or list of currently **online devices** on the local network.
*   **Selection**: User selects the target recipient (Device B) from the list.
*   **File Chooser**: User clicks the "Choose File" button (or drags and drops a file into the app window) to select the file(s) to send.
*   **Initiation**: User clicks the "Send" button.
*   **State Handling**:
    *   If Device B has previously trusted Device A ("Always agree" list), the transfer begins immediately.
    *   Otherwise, Device A's UI displays a *“Waiting for Device B to accept…”* loading state.
*   **Active Transfer**: Once approved, a progress bar appears showing transfer speed (MB/s), time remaining, and completion percentage.
*   **Completion**: A success notification is shown when the file transfer finishes.

### Scenario 2: Receiving a File
*   **Standby**: Device B is running the app (can be minimized to system tray).
*   **Incoming Request**: Device B detects an incoming transfer request from Device A.
*   **System Notification / Popup**: A dialog appears on Device B: *"Device A is trying to send you [Filename.ext] (Size). "*
    *   The dialog displays the **default save destination** (e.g., `C:\Users\User\Downloads\FileCourier`) and an edit button to change the path before accepting.
*   **Decision Options**:
    *   **Deny**: Rejects the transfer. Device A is notified of the rejection.
    *   **Agree Once**: Accepts this specific transfer. Does not save Device A to the trusted list.
    *   **Always Agree**: Accepts this transfer AND adds Device A's unique ID to a trusted list. Future transfers from Device A will start automatically without prompting.
*   **Active Transfer**: Upon agreement, the transfer executes, and Device B sees an incoming progress bar. When finished, a "Reveal in Explorer" button becomes available.

### Scenario 3: Managing Trusted Devices (Settings)
*   User can navigate to a "Trusted Devices" or "Settings" tab.
*   User sees a list of all devices previously marked as "Always agree".
*   User can revoke permissions (remove devices from the auto-accept list).
*   User can change the global default folder for saved incoming files.

## 5. Technical Data Models (High-Level)

### System Device
*   `DeviceId` (UUID - unique per app installation)
*   `DeviceName` (String, e.g., "John's Windows PC")
*   `IPAddress` (String)
*   `Port` (Integer)
*   `OS` (String - Windows/Mac/Android/iOS)

### Transfer Request Payload
*   `SenderId` (UUID)
*   `FileName` (String)
*   `FileSize` (Long)
*   `Checksum` (String/SHA256 - optional, for verifying integrity)

### Trusted Device Record
*   `TrustedDeviceId` (UUID)
*   `DateAdded` (Datetime)

## 6. Implementation Phases (Roadmap)
*   **Phase 1**: Windows to Windows core P2P connection (IP based). Command-line or basic rough UI.
*   **Phase 2**: Implement UDP Discovery so IP addresses are no longer needed.
*   **Phase 3**: Build out the polished UI (File Chooser, Progress Bars, Settings).
*   **Phase 4**: Permission management mapping ("Always Agree" database via local JSON/SQLite).
*   **Phase 5**: Cross-platform expansion (macOS/Mobile targets).

## 7. System Architecture
For detailed information regarding the internal repository structures, platforms, and native codebase hierarchies, please see the separate design document: [architecture.md](./architecture.md).
