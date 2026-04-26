package com.filecourier.app.data

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import kotlinx.coroutines.flow.Flow

@Dao
interface HistoryDao {
    @Query("SELECT * FROM transfer_history WHERE direction = :direction ORDER BY timestamp DESC")
    fun getHistoryByDirection(direction: String): Flow<List<TransferHistoryRecord>>

    @Query("SELECT * FROM transfer_history ORDER BY timestamp DESC")
    fun getAllHistory(): Flow<List<TransferHistoryRecord>>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertOrUpdate(record: TransferHistoryRecord)

    @Query("DELETE FROM transfer_history WHERE transferId = :id")
    suspend fun deleteRecord(id: String)

    @Query("DELETE FROM transfer_history WHERE direction = :direction")
    suspend fun clearHistory(direction: String)
    
    @Query("UPDATE transfer_history SET status = :status WHERE transferId = :id")
    suspend fun updateStatus(id: String, status: String)

    @Query("UPDATE transfer_history SET itemPath = :path WHERE transferId = :id")
    suspend fun updateItemPath(id: String, path: String)
}
