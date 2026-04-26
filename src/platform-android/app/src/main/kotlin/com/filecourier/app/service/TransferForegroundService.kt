package com.filecourier.app.service

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.Service
import android.content.Context
import android.content.Intent
import android.content.pm.ServiceInfo
import android.os.Build
import android.os.IBinder
import android.util.Log

class TransferForegroundService : Service() {

    companion object {
        const val ACTION_START_TRANSFER = "com.filecourier.app.action.START_TRANSFER"
        const val ACTION_STOP_TRANSFER = "com.filecourier.app.action.STOP_TRANSFER"

        fun startService(context: Context) {
            val intent = Intent(context, TransferForegroundService::class.java).apply {
                action = ACTION_START_TRANSFER
            }
            context.startForegroundService(intent)
        }

        fun stopService(context: Context) {
            val intent = Intent(context, TransferForegroundService::class.java).apply {
                action = ACTION_STOP_TRANSFER
            }
            context.startService(intent)
        }
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        when (intent?.action) {
            ACTION_START_TRANSFER -> {
                Log.d("TransferService", "Starting foreground service")
                createNotificationChannel()
                
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
                    startForeground(1, createNotification(), ServiceInfo.FOREGROUND_SERVICE_TYPE_DATA_SYNC)
                } else {
                    startForeground(1, createNotification())
                }
            }
            ACTION_STOP_TRANSFER -> {
                Log.d("TransferService", "Stopping foreground service")
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopSelf()
            }
        }
        
        return START_NOT_STICKY
    }

    override fun onBind(intent: Intent?): IBinder? {
        return null
    }

    private fun createNotificationChannel() {
        val channel = NotificationChannel(
            "transfer_channel",
            "File Transfers",
            NotificationManager.IMPORTANCE_LOW,
        )
        val manager = getSystemService(NOTIFICATION_SERVICE) as NotificationManager
        manager.createNotificationChannel(channel)
    }

    private fun createNotification(): Notification {
        val builder = Notification.Builder(this, "transfer_channel")

        return builder
            .setContentTitle("FileCourier Transfer Active")
            .setContentText("Sending files...")
            .setSmallIcon(android.R.drawable.stat_sys_upload)
            .build()
    }
}
