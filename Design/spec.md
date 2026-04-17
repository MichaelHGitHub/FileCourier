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
2.  **Cross-Platform File & Text Transfer**: Send any file type, folder (preserving directory structure), size, or simple text/clipboard snippets, bounded only by local network speeds.
3.  **Pause & Resume**: Automatically or manually pause transfers, and resume them later from the exact byte where they left off.
4.  **Transfer Bandwidth Throttling**: Optionally cap transfer speeds (e.g., 10 MB/s) to avoid choking the local network. Default is unlimited.
5.  **Trust Management**: Remember frequently used devices to bypass manual approval prompts.
6.  **Transfer History Log**: Keep an ongoing local record of all sent and received files for future reference.
7.  **Transfer Progress**: Real-time visualization of transfer speed, ETA, and progress bar.
8.  **Multi-Language Localization (i18n)**: The GUI will be fully translatable, adapting automatically based on system preferences by extracting text into native resource files (e.g., `.resw` on Windows, `Localizable.strings` on macOS/iOS, and `strings.xml` on Android).
9.  **Secure Encryption**: Users can optionally toggle on end-to-end encryption for individual transfers, ensuring their data is safe even on open public Wi-Fi networks.
10. **Manual IP Fallback**: Connect directly to a device using its IP address if automatic network discovery fails.

## 4. Detailed User Flows / Scenarios

### Scenario 0: First Run / Installation
*   **Action**: The user launches the app for the very first time after installation.
*   **System Prompt**: Because the app relies on local network communication (UDP for discovery, TCP for transfers), the operating system (e.g., Windows Defender Firewall) may block it by default.
*   **In-App Guidance**: The app displays a clear, user-friendly prompt explaining *why* network access is required ("FileCourier needs network access to discover devices and transfer files on your local network").
*   **Resolution**: The user clicks "Allow" on the system firewall prompt, ensuring the app can broadcast and listen on the required ports.

### Scenario 1: Sending a File
*   **Action**: Device A opens the FileCourier app. The main UI displays a sleek, standard list of currently **online devices**. A clear, persistent message is displayed on this screen: *"Both sender and receiver must be connected to the same Local Area Network (Wi-Fi or LAN) to transfer files."* A "Refresh" button is also provided to manually trigger a re-scan of the local network if a device doesn't appear immediately. Behind the scenes, the app listens for UDP "heartbeats" every few seconds; if a device closes the app or drops offline, it is automatically removed from this list. If the target device still does not appear (e.g., due to router restrictions), a **"Connect Manually"** button allows the user to directly input the receiver's IP address.
*   **Selection**: User selects the target recipient (Device B) from the list.
*   **Payload Selection & Options**: User clicks the "Choose File" button (or drags and drops) to select file(s)/folder(s) to send. Alternatively, the user can paste text or clipboard content into a dedicated "Send Text" box. The UI presents an "Encrypt Transfer" checkbox if they are on an untrusted network.
*   **Initiation**: User clicks the "Send" button.
*   **State Handling**:
    *   If Device B has previously trusted Device A ("Always agree" list), the transfer begins immediately.
    *   Otherwise, Device A's UI displays a *“Waiting for Device B to accept…”* loading state.
*   **Active Transfer**: Once approved, a progress bar appears showing transfer speed (MB/s), time remaining, and completion percentage. "Pause", "Resume", and "Cancel" buttons are available.
*   **Cancellation & Resumption**: If a transfer is cancelled or network is lost, fully transferred files are kept. The incomplete file remains partially written, allowing the user to resume the transfer later (starting from the last written byte).
*   **Completion**: A success notification is shown when the file transfer finishes.

### Scenario 2: Receiving a File
*   **Standby**: Device B is running the app (can be minimized to system tray).
*   **Incoming Request**: Device B detects an incoming transfer request from Device A.
*   **Pre-flight Checks**: Device B calculates the total size of the incoming batch and verifies that there is enough available disk space in the destination drive. If space is insufficient, the transfer is automatically rejected, and Device A is notified with an "Insufficient storage space on receiver" error.
*   **System Notification / Popup**: A dialog appears on Device B: *"Device A is trying to send you [Number] files (Total Size)."* (If a single file, it will display *"Device A is trying to send you [Filename.ext] (Size)."*)
    *   **Text/Clipboard Handling**: If the incoming request is purely text, the dialog simply displays the text content with a convenient **"Copy to Clipboard"** button, and does not require choosing a save destination.
    *   **File Handling**: For files, the dialog displays the **default save destination** (e.g., `C:\Users\User\Downloads\FileCourier`) and an edit button to change the path before accepting.
*   **Decision Options**:
    *   **Deny**: Rejects the transfer. Device A is notified of the rejection.
    *   **Agree Once**: Accepts this specific transfer. Does not save Device A to the trusted list.
    *   **Always Agree**: Accepts this transfer AND adds Device A's unique ID to a trusted list. Future transfers from Device A will start automatically without prompting.
*   **Collision Handling**: If a file with the exact same name already exists in the destination folder, the app refers to a global setting. By default, it will automatically append a numbering suffix (e.g., `vacation (1).jpg`) to keep both. Users can also change this preference to "Overwrite Existing" or "Skip File".
*   **Active Transfer**: Upon agreement, the transfer executes, and Device B sees an incoming progress bar with "Pause", "Resume", and "Cancel" buttons. When finished, a "Reveal in Explorer" button becomes available.
*   **Cancellation & Resumption**: If cancelled mid-transfer, completed files from the batch are retained. The partial file is kept so the transfer can be resumed later from the point of interruption.

### Scenario 3: Settings & History
*   **Transfer History**: User can view a log of all previously completed transfers, with timestamps and file paths.
*   **Trusted Devices**: User sees a list of all devices marked as "Always agree" and can revoke permissions.
*   **File Conflicts**: User can change the global collision handling behavior (Keep Both, Overwrite, Skip).
*   **Bandwidth Throttling**: User can set a maximum transfer speed limit.
*   **Default Folder**: User can change the global default folder for saved incoming files.

## 5. Technical Data Models (High-Level)

### System Device
*   `DeviceId` (UUID - unique per app installation)
*   `DeviceName` (String, e.g., "John's Windows PC")
*   `IPAddress` (String)
*   `Port` (Integer)
*   `OS` (String - Windows/Mac/Android/iOS)

### Transfer Request Payload
*   `SenderId` (UUID)
*   `IsEncrypted` (Boolean)
*   `TextPayload` (String - optional, for sending clipboard text or links)
*   `Files` (Array of Objects - optional if only sending text)
    *   `FileName` (String)
    *   `RelativePath` (String - maintains directory structure)
    *   `FileSize` (Long)
    *   `ByteOffset` (Long - used for resuming transfers)
    *   `Checksum` (String/SHA256 - optional, for verifying integrity)

### Local Data Storage (SQLite / JSON)
*   **Trusted Device Record**:
    *   `TrustedDeviceId` (UUID)
    *   `DateAdded` (Datetime)
*   **Transfer History Record**:
    *   `TransferId` (UUID)
    *   `CounterpartyId` (UUID)
    *   `Direction` (String: "Sent" or "Received")
    *   `TotalFiles` (Integer)
    *   `TotalSize` (Long)
    *   `Timestamp` (Datetime)
    *   `Status` (String: "Completed", "Cancelled", "Failed")

## 6. Implementation Phases (Roadmap)
*   **Phase 1**: Windows to Windows core P2P connection (IP based). Command-line or basic rough UI.
*   **Phase 2**: Implement UDP Discovery so IP addresses are no longer needed.
*   **Phase 3**: Build out the polished UI (File Chooser, Progress Bars, Settings).
*   **Phase 4**: Permission management mapping ("Always Agree" database via local JSON/SQLite).
*   **Phase 5**: Cross-platform expansion (macOS/Mobile targets).

## 7. System Architecture
For detailed information regarding the internal repository structures, platforms, and native codebase hierarchies, please see the separate design document: [architecture.md](./architecture.md).
