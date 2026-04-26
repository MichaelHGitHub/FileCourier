package com.filecourier.app.data

import androidx.room.Entity
import androidx.room.PrimaryKey

@Entity(tableName = "transfer_history")
data class TransferHistoryRecord(
    @PrimaryKey val transferId: String,
    val counterpartyId: String,
    val counterpartyName: String,
    val direction: String, // "Sent" or "Received"
    val itemName: String,
    val itemPath: String,
    val totalFiles: Int,
    val totalSize: Long,
    val timestamp: Long,
    val status: String // "InProgress", "Completed", "Cancelled", "Failed"
)
