# Phase 1 Test Plan (Windows P2P Core)

## Objectives
Validate the underlying networking mechanisms required to send and receive files point-to-point via IP addresses on a local network.

## Focus Areas

### 1. Payload Serialization
Before a file is sent over TCP, it must first be serialized with its metadata so that the receiving end knows what it is receiving and how to parse the byte stream.
*   **Test Case 1**: A `TransferRequest` (FileName, FileSize, SenderId) serialization generates a valid JSON payload.
*   **Test Case 2**: The protocol correctly encodes the overall payload as `[Header Length (4 bytes)] + [JSON Header Bytes] + [Raw File Bytes]`.
*   **Test Case 3**: The receiver correctly parses the 4-byte header length and deserializes the JSON metadata back into a `TransferRequest`.

### 2. TCP Client & Server (Mocked Core)
*   **Test Case 4**: A simulated `FileSender` connects directly to a localhost TCP port and manages to send the payload byte-array without throwing errors.
*   **Test Case 5**: A simulated `FileReceiver` running on a localhost port successfully accepts a connection, reads the data stream, and saves the resulting bytes to a destination stream matching the exact size of the input file.

## Method
*   We use **xUnit** as the testing framework.
*   We use **Moq** (optionally) to mock the actual File System so tests don't clutter the developer's disk. 
*   Tests are fully isolated and do not require actual real-world PCs. They operate over the system's `127.0.0.1` Loopback interface.
