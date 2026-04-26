package com.filecourier.network

import android.util.Log
import com.filecourier.network.models.TransferRequestHeader
import com.filecourier.network.models.TransferResponseHeader
import com.google.gson.Gson
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.io.DataInputStream
import java.io.DataOutputStream
import java.io.File
import java.io.FileInputStream
import java.net.ServerSocket
import java.net.Socket
import kotlinx.coroutines.isActive
import java.security.MessageDigest

class TransferManager {
    private val gson = Gson()

    private fun Int.toLE(): Int = Integer.reverseBytes(this)
    private fun Int.fromLE(): Int = Integer.reverseBytes(this)

    suspend fun sendPayload(
        host: String,
        port: Int,
        header: TransferRequestHeader,
        filesToTransfer: List<File>,
        onProgress: (fileName: String, bytesSent: Long, totalBytes: Long) -> Unit,
        onFileComplete: (fileName: String) -> Unit,
        onFileFailed: (fileName: String) -> Unit,
    ): Boolean = withContext(Dispatchers.IO) {
        var socket: Socket? = null
        try {
            socket = Socket(host, port)
            val output = DataOutputStream(socket.getOutputStream())
            val input = DataInputStream(socket.getInputStream())

            // 1. Send Header
            val headerJson = gson.toJson(header)
            val headerBytes = headerJson.toByteArray(Charsets.UTF_8)
            output.writeInt(headerBytes.size.toLE())
            output.write(headerBytes)
            output.flush()

            // 2. Wait for Response Header
            val responseLen = input.readInt().fromLE()
            val responseBytes = ByteArray(responseLen)
            input.readFully(responseBytes)
            val responseJson = String(responseBytes, Charsets.UTF_8)
            val response = gson.fromJson(responseJson, TransferResponseHeader::class.java)

            if (response.Status != 0) { // 0 = Accepted
                Log.d("TransferManager", "Transfer rejected: ${response.Reason}")
                return@withContext false
            }

            // 3. Send Files
            val filesList = header.Files
            if ((filesList != null) && filesToTransfer.isNotEmpty()) {
                val buffer = ByteArray(8192)
                val digest = MessageDigest.getInstance("SHA-256")
                
                for (i in filesList.indices) {
                    val fileInfo = filesList[i]
                    val localFile = filesToTransfer[i]
                    
                    if (!localFile.exists() || !localFile.canRead()) {
                        Log.e("TransferManager", "File not readable: ${localFile.name}")
                        output.writeInt((-1).toLE()) // Signal read failure for this file
                        onFileFailed(fileInfo.FileName)
                        continue
                    }

                    var bytesSent = 0L
                    FileInputStream(localFile).use { fis ->
                        if (fileInfo.ByteOffset > 0) {
                            fis.skip(fileInfo.ByteOffset)
                            bytesSent = fileInfo.ByteOffset
                        }

                        while (isActive) {
                            val bytesRead = fis.read(buffer)
                            if (bytesRead == -1) {
                                // Unlike Windows which uses -1 for error and EOF is just end of loop,
                                // we need to signal that this file is done?
                                // Actually Windows receiver loops until fileReceived < fileInfo.FileSize.
                                // It doesn't expect an EOF signal per file, just the chunks.
                                break
                            }
                            
                            val chunk = if (bytesRead == buffer.size) buffer else buffer.copyOfRange(0, bytesRead)
                            val hash = digest.digest(chunk)
                            
                            output.writeInt(bytesRead.toLE())
                            output.write(hash)
                            output.write(chunk)
                            output.flush()
                            
                            bytesSent += bytesRead
                            onProgress(fileInfo.FileName, bytesSent, fileInfo.FileSize)
                        }
                    }
                    onFileComplete(fileInfo.FileName)
                }
            }
            Log.d("TransferManager", "Transfer session completed successfully")
            return@withContext true
        } catch (e: Exception) {
            Log.e("TransferManager", "Failed to send payload", e)
            return@withContext false
        } finally {
            socket?.close()
        }
    }

    suspend fun startListening(
        port: Int,
        onIncomingRequest: suspend (header: TransferRequestHeader) -> Boolean,
        onFileChunkReceived: suspend (fileName: String, chunk: ByteArray) -> Unit,
        onFileComplete: suspend (fileName: String) -> Unit,
        onTransferComplete: suspend (header: TransferRequestHeader) -> Unit = {},
    ) = withContext(Dispatchers.IO) {
        var serverSocket: ServerSocket? = null
        try {
            serverSocket = ServerSocket(port)
            Log.d("TransferManager", "Listening for incoming TCP transfers on port $port")

            while (isActive) {
                val clientSocket = serverSocket.accept()
                // Launch a new coroutine for each incoming connection to avoid blocking
                launch {
                    handleIncomingClient(clientSocket, onIncomingRequest, onFileChunkReceived, onFileComplete, onTransferComplete)
                }
            }
        } catch (e: Exception) {
            Log.e("TransferManager", "Error in ServerSocket", e)
        } finally {
            serverSocket?.close()
        }
    }

    private suspend fun handleIncomingClient(
        socket: Socket,
        onIncomingRequest: suspend (header: TransferRequestHeader) -> Boolean,
        onFileChunkReceived: suspend (fileName: String, chunk: ByteArray) -> Unit,
        onFileComplete: suspend (fileName: String) -> Unit,
        onTransferComplete: suspend (header: TransferRequestHeader) -> Unit,
    ) = withContext(Dispatchers.IO) {
        try {
            val input = DataInputStream(socket.getInputStream())
            val output = DataOutputStream(socket.getOutputStream())

            // 1. Read Header
            val headerLen = input.readInt().fromLE()
            val headerBytes = ByteArray(headerLen)
            input.readFully(headerBytes)
            val headerJson = String(headerBytes, Charsets.UTF_8)
            Log.d("TransferManager", "Received Header: $headerJson")
            val header = gson.fromJson(headerJson, TransferRequestHeader::class.java)

            // 2. Ask UI/ViewModel to accept/reject
            val accepted = onIncomingRequest(header)
            
            val response = TransferResponseHeader(
                Status = if (accepted) 0 else 1, // 0 = Accepted, 1 = Rejected
            )
            val responseJson = gson.toJson(response)
            val respBytes = responseJson.toByteArray(Charsets.UTF_8)
            output.writeInt(respBytes.size.toLE())
            output.write(respBytes)
            output.flush()

            if (!accepted) {
                socket.close()
                return@withContext
            }

            // 3. Receive Files
            header.Files?.forEach { fileInfo ->
                var fileFailed = false
                var receivedForThisFile = 0L
                while (receivedForThisFile < fileInfo.FileSize) {
                    val chunkLen = input.readInt().fromLE()
                    if (chunkLen == -1) {
                        Log.w("TransferManager", "Sender signaled read failure for ${fileInfo.FileName}")
                        fileFailed = true
                        break
                    }
                    
                    val hash = ByteArray(32)
                    input.readFully(hash)
                    
                    val chunkData = ByteArray(chunkLen)
                    input.readFully(chunkData)
                    
                    // Integrity check
                    val actualHash = MessageDigest.getInstance("SHA-256").digest(chunkData)
                    if (!actualHash.contentEquals(hash)) {
                        Log.e("TransferManager", "Checksum mismatch for ${fileInfo.FileName}")
                    }

                    onFileChunkReceived(fileInfo.FileName, chunkData)
                    receivedForThisFile += chunkLen
                }
                if (!fileFailed) {
                    onFileComplete(fileInfo.FileName)
                }
            }
            onTransferComplete(header)
        } catch (e: Exception) {
            Log.e("TransferManager", "Error handling incoming client", e)
        } finally {
            socket.close()
        }
    }
}

