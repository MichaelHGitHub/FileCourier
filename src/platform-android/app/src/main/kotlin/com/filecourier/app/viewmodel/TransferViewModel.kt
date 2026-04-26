package com.filecourier.app.viewmodel

import android.app.Application
import android.util.Log
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.filecourier.app.data.AppDatabase
import com.filecourier.app.data.SettingsRepository
import com.filecourier.app.data.TransferHistoryRecord
import com.filecourier.network.TransferManager
import com.filecourier.network.models.TransferRequestHeader
import android.content.ClipboardManager
import android.content.Context
import android.net.Uri
import android.os.Environment
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.io.FileOutputStream
import java.io.File
import java.util.UUID
import com.filecourier.app.service.TransferForegroundService

sealed class IncomingRequestState {
    object Idle : IncomingRequestState()
    data class Pending(
        val header: TransferRequestHeader,
        val isAutoAccepted: Boolean,
        val onRespond: (Boolean) -> Unit,
    ) : IncomingRequestState()
}

class TransferViewModel(application: Application) : AndroidViewModel(application) {
    private val transferManager = TransferManager()
    private val settingsRepo = SettingsRepository(application)
    private val historyDao = AppDatabase.getDatabase(application).historyDao()

    private val _incomingRequest = MutableStateFlow<IncomingRequestState>(IncomingRequestState.Idle)
    val incomingRequest: StateFlow<IncomingRequestState> = _incomingRequest.asStateFlow()

    private var currentReceivingFile: File? = null
    private var currentOutputStream: FileOutputStream? = null
    
    // Map to track active transfer IDs by SenderId to handle concurrent requests
    private val activeTransfers = mutableMapOf<String, String>()
    private var activeTransferCount = 0

    private fun incrementActiveTransfers() {
        activeTransferCount++
        if (activeTransferCount == 1) {
            TransferForegroundService.startService(getApplication())
        }
    }

    private fun decrementActiveTransfers() {
        activeTransferCount--
        if (activeTransferCount <= 0) {
            activeTransferCount = 0
            TransferForegroundService.stopService(getApplication())
        }
    }

    init {
        startListeningForTransfers()
    }

    private fun startListeningForTransfers() {
        viewModelScope.launch {
            transferManager.startListening(
                port = 45455,
                onIncomingRequest = { header ->
                    val tid = UUID.randomUUID().toString()
                    activeTransfers[header.SenderId] = tid
                    
                    val files = header.Files.orEmpty()
                    val record = TransferHistoryRecord(
                        transferId = tid,
                        counterpartyId = header.SenderId,
                        counterpartyName = header.SenderName,
                        direction = "Received",
                        itemName = if (files.isNotEmpty()) files.first().FileName else (header.TextPayload ?: "Text Message"),
                        itemPath = "",
                        totalFiles = if (files.isNotEmpty()) files.size else 1,
                        totalSize = if (files.isNotEmpty()) files.sumOf { it.FileSize } else header.TextPayload?.length?.toLong() ?: 0L,
                        timestamp = System.currentTimeMillis(),
                        status = "InProgress",
                    )
                    historyDao.insertOrUpdate(record)

                    // Permission dialog logic
                    val isTrusted = settingsRepo.isDeviceTrusted(header.SenderId)
                    val deferred = CompletableDeferred<Boolean>()
                    
                    // Push to UI to prompt user
                    _incomingRequest.value = IncomingRequestState.Pending(header, isAutoAccepted = isTrusted) { result ->
                        deferred.complete(result)
                    }
                    
                    // Wait for user action (Accept/Allow or Decline/Reject)
                    val accepted = deferred.await()
                    if (accepted) {
                        incrementActiveTransfers()
                    } else {
                        historyDao.updateStatus(tid, "Cancelled")
                    }
                    accepted
                },
                onFileChunkReceived = { fileName, chunk ->
                    withContext(Dispatchers.IO) {
                        try {
                            if (currentReceivingFile?.name != fileName) {
                                currentOutputStream?.close()
                                val savePath = settingsRepo.defaultSaveLocation
                                val downloadDir = if (savePath.isNotEmpty()) {
                                    File(savePath)
                                } else {
                                    File(getApplication<Application>().getExternalFilesDir(Environment.DIRECTORY_DOWNLOADS), "FileCourier")
                                }
                                downloadDir.mkdirs()
                                val file = File(downloadDir, fileName)
                                currentReceivingFile = file
                                currentOutputStream = FileOutputStream(file, true)
                            }
                            currentOutputStream?.write(chunk)
                        } catch (e: Exception) {
                            Log.e("TransferViewModel", "Error writing chunk for $fileName", e)
                        }
                    }
                },
                onFileComplete = { fileName ->
                    Log.d("TransferViewModel", "Completed receiving $fileName")
                    val streamToClose = currentOutputStream
                    currentOutputStream = null
                    currentReceivingFile = null
                    
                    try {
                        streamToClose?.let { 
                            withContext(Dispatchers.IO) {
                                it.close()
                            }
                        }
                    } catch (e: Exception) {
                        Log.e("TransferViewModel", "Error closing stream", e)
                    }
                },
            ) { header ->
                Log.d("TransferViewModel", "Transfer session completed for ${header.SenderName}")
                activeTransfers.remove(header.SenderId)?.let { tid ->
                    viewModelScope.launch {
                        historyDao.updateStatus(tid, "Completed")
                        decrementActiveTransfers()
                    }
                }
            }
        }
    }

    fun copyToClipboard(text: String) {
        val context = getApplication<Application>()
        val clipboard = context.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
        val clip = android.content.ClipData.newPlainText("FileCourier Message", text)
        clipboard.setPrimaryClip(clip)
    }

    fun respondToRequest(accept: Boolean, alwaysAccept: Boolean = false) {
        val state = _incomingRequest.value
        if (state is IncomingRequestState.Pending) {
            if (alwaysAccept) {
                viewModelScope.launch {
                    settingsRepo.addTrustedDevice(state.header.SenderId, state.header.SenderName)
                }
            }
            state.onRespond(accept)
            _incomingRequest.value = IncomingRequestState.Idle
        }
    }

    fun sendTextPayload(host: String, port: Int, targetId: String, text: String) {
        viewModelScope.launch {
            incrementActiveTransfers()
            val tid = UUID.randomUUID().toString()
            val header = TransferRequestHeader(
                SenderId = settingsRepo.deviceId,
                SenderName = settingsRepo.deviceName,
                TextPayload = text,
            )
            
            val record = TransferHistoryRecord(
                transferId = tid,
                counterpartyId = targetId,
                counterpartyName = "Remote Device", // We should ideally pass the name here
                direction = "Sent",
                itemName = "Text Message",
                itemPath = "",
                totalFiles = 1,
                totalSize = text.length.toLong(),
                timestamp = System.currentTimeMillis(),
                status = "InProgress",
            )
            historyDao.insertOrUpdate(record)

            val success = transferManager.sendPayload(
                host = host,
                port = port,
                header = header,
                filesToTransfer = emptyList(),
                onProgress = { _, _, _ -> },
                onFileComplete = { },
            ) { }
            
            historyDao.updateStatus(tid, if (success) "Completed" else "Failed")
            decrementActiveTransfers()
        }
    }

    fun sendFilePayload(host: String, port: Int, targetId: String, uris: List<Uri>) {
        viewModelScope.launch {
            val context = getApplication<Application>()
            val tempFiles = mutableListOf<File>()
            val tid = UUID.randomUUID().toString()
            
            try {
                incrementActiveTransfers()
                Log.d("TransferViewModel", "Starting file payload send to $host for ${uris.size} files")
                // Copy Uris to temp files because TransferManager expects java.io.File
                uris.forEach { uri ->
                    try {
                        val fileName = getFileNameFromUri(uri) ?: "unknown_file_${UUID.randomUUID()}"
                        val tempFile = File(context.cacheDir, fileName)
                        context.contentResolver.openInputStream(uri)?.use { input ->
                            FileOutputStream(tempFile).use { output ->
                                input.copyTo(output)
                            }
                        }
                        if (tempFile.exists()) {
                            tempFiles.add(tempFile)
                        }
                    } catch (e: Exception) {
                        Log.e("TransferViewModel", "Failed to process URI: $uri", e)
                    }
                }

                if (tempFiles.isEmpty()) {
                    Log.w("TransferViewModel", "No files were successfully processed from URIs")
                    return@launch
                }

                val record = TransferHistoryRecord(
                    transferId = tid,
                    counterpartyId = targetId,
                    counterpartyName = "Remote Device",
                    direction = "Sent",
                    itemName = tempFiles.first().name,
                itemPath = tempFiles.first().absolutePath,
                totalFiles = tempFiles.size,
                totalSize = tempFiles.sumOf { it.length() },
                timestamp = System.currentTimeMillis(),
                status = "InProgress",
            )
            historyDao.insertOrUpdate(record)

                val header = TransferRequestHeader(
                    SenderId = settingsRepo.deviceId,
                    SenderName = settingsRepo.deviceName,
                    Files = tempFiles.map { 
                        com.filecourier.network.models.FileItem(
                            FileName = it.name,
                            RelativePath = it.name,
                            FileSize = it.length(),
                        )
                    },
                )

                Log.d("TransferViewModel", "Triggering sendPayload via TransferManager")
                val success = transferManager.sendPayload(
                    host = host,
                    port = port,
                    header = header,
                    filesToTransfer = tempFiles,
                    onProgress = { fileName, sent, total -> 
                        Log.d("TransferViewModel", "Progress for $fileName: $sent/$total")
                    },
                    onFileComplete = { fileName ->
                        Log.d("TransferViewModel", "Completed: $fileName")
                    },
                ) { fileName ->
                    Log.e("TransferViewModel", "Failed: $fileName")
                }
                
                historyDao.updateStatus(tid, if (success) "Completed" else "Failed")
                Log.d("TransferViewModel", "Transfer result: $success")
            } catch (e: Exception) {
                Log.e("TransferViewModel", "Unexpected error in sendFilePayload", e)
                historyDao.updateStatus(tid, "Failed")
            } finally {
                decrementActiveTransfers()
                // In a production app, we should probably delete temp files after transfer
            }
        }
    }

    private fun getFileNameFromUri(uri: Uri): String? {
        val context = getApplication<Application>()
        var name: String? = null
        try {
            context.contentResolver.query(uri, null, null, null, null)?.use { cursor ->
                if (cursor.moveToFirst()) {
                    val index = cursor.getColumnIndex(android.provider.OpenableColumns.DISPLAY_NAME)
                    if (index != -1) {
                        name = cursor.getString(index)
                    }
                }
            }
        } catch (e: Exception) {
            Log.e("TransferViewModel", "Error querying filename for URI: $uri", e)
        }
        return name ?: uri.path?.substringAfterLast('/')
    }
}
