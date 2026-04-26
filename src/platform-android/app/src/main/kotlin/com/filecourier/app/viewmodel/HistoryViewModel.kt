package com.filecourier.app.viewmodel

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.filecourier.app.data.AppDatabase
import com.filecourier.app.data.TransferHistoryRecord
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch

class HistoryViewModel(application: Application) : AndroidViewModel(application) {
    private val historyDao = AppDatabase.getDatabase(application).historyDao()

    val sentHistory: StateFlow<List<TransferHistoryRecord>> = historyDao.getHistoryByDirection("Sent")
        .stateIn(viewModelScope, SharingStarted.Lazily, emptyList())

    val receivedHistory: StateFlow<List<TransferHistoryRecord>> = historyDao.getHistoryByDirection("Received")
        .stateIn(viewModelScope, SharingStarted.Lazily, emptyList())

    fun clearHistory(direction: String) {
        viewModelScope.launch {
            if (direction == "All") {
                historyDao.clearAllHistory()
            } else {
                historyDao.clearHistory(direction)
            }
        }
    }

    fun deleteRecord(transferId: String) {
        viewModelScope.launch {
            historyDao.deleteRecord(transferId)
        }
    }

    suspend fun getRecordById(transferId: String): TransferHistoryRecord? {
        return historyDao.getRecordById(transferId)
    }
}
