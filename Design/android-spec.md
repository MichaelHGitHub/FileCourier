# FileCourier - Android Specification Addendum

This document serves as a platform-specific addendum to the global FileCourier `spec.md` and `architecture.md`. It outlines the feature deviations, UI adaptations, and OS-specific constraints required to implement the Android Minimum Viable Product (MVP).

## 1. Feature Deviations from Global Spec

To ensure a streamlined, native mobile experience and meet the one-day MVP timeline, the Android version deviates from the global Windows specification in the following ways:

### Features Kept for Parity
*   **Trust Management ("Always Agree"):** Included. Users can bypass manual approval prompts for trusted devices.
*   **Manual IP Fallback:** Included. Users can manually connect via IP if UDP discovery is blocked by the local network.
*   **Fault-Tolerant Batch Transfers:** Included. The system gracefully skips corrupted or locked files without dropping the entire transfer session.

### Features Cut for Android MVP
*   **Bandwidth Throttling:** Removed. Not essential for the mobile MVP.
*   **System Tray / Background Daemon:** Removed. Android handles background tasks differently (see *OS Constraints*).
*   **Registry/File-based OS integrations:** Removed. Features like "Start with Windows" and desktop context menus do not apply.

## 2. Android-Specific UI Adaptations

*   **Unified History View:** Instead of the dual-tabbed interface (Sent vs Received) used on desktop, Android utilizes a single, unified scrolling list. Users can filter this list using a dropdown menu (`All`, `Sent`, `Received`).
*   **Status Bar Notifications:** Instead of system tray pop-ups, active transfers and completion events are displayed using standard Android status bar notifications.
*   **Share-Sheet Integration:** FileCourier registers as a target in the Android OS Share menu. Users can select photos/files in other apps (e.g., Gallery) and tap "Share -> FileCourier" to queue them for sending.
*   **Clipboard Integration:** Built-in support for reading from and writing to the Android clipboard to facilitate the text-payload sharing feature.
*   **Screen Lock Prevention:** The app requests a `WakeLock` / `FLAG_KEEP_SCREEN_ON` during active file transfers to prevent the device from going to sleep and interrupting the socket connection.

## 3. Technical Constraints & Architecture

The Android app is built using **Kotlin** and **Jetpack Compose**, targeting Android 14 (API Level 34).

### Networking & Permissions
*   **Zero-Config Discovery:** To ensure UDP broadcast packets are successfully sent and received across the local Wi-Fi, the app must acquire a `WifiManager.MulticastLock`. 
*   **Runtime Permissions:** 
    *   `NEARBY_WIFI_DEVICES`: Required on modern Android to discover local network peers.
    *   `POST_NOTIFICATIONS`: Required to display the foreground transfer status.

### Background Execution
*   **Foreground Service:** Active file transfers are hosted in an Android Foreground Service tied to a persistent notification. This prevents the OS from killing the transfer if the user minimizes the app. Once a transfer completes, the service shuts down (no persistent daemon).
*   **WorkManager:** Used for scheduling and resuming pending transfers if the app drops connection and needs to reconnect asynchronously.

### Storage Model
*   **Scoped Storage:** Received files are saved using Android's Scoped Storage (Storage Access Framework or MediaStore) to comply with modern Android file system security, typically defaulting to the public `Downloads` directory.

## 4. Module Structure
As defined in the global architecture, the Android project resides in `src/platform-android/` and is divided into:
*   `:app` - Jetpack Compose UI, ViewModels, and UI-state management.
*   `:core-network` - The shared Kotlin port of the UDP discovery and TCP chunked-transfer protocol.
