package com.filecourier.app.data

import android.content.Context
import android.content.SharedPreferences
import androidx.core.content.edit
import java.util.UUID

class SettingsRepository(context: Context) {
    private val prefs: SharedPreferences = context.getSharedPreferences("filecourier_settings", Context.MODE_PRIVATE)

    val deviceId: String
        get() {
            var id = prefs.getString("device_id", null)
            if (id == null) {
                id = UUID.randomUUID().toString()
                prefs.edit { putString("device_id", id) }
            }
            return id
        }

    var deviceName: String
        get() = prefs.getString("device_name", android.os.Build.MODEL) ?: "Android Device"
        set(value) = prefs.edit { putString("device_name", value) }

    var defaultSaveLocation: String
        get() = prefs.getString("default_save_location", "") ?: ""
        set(value) = prefs.edit { putString("default_save_location", value) }

    fun getDefaultSaveLocationPath(): String {
        val saved = defaultSaveLocation
        if (saved.isNotEmpty()) return saved
        
        // Use a more reliable public directory that doesn't always require MANAGE_EXTERNAL_STORAGE
        val publicDownloads = android.os.Environment.getExternalStoragePublicDirectory(android.os.Environment.DIRECTORY_DOWNLOADS)
        val fileCourierDir = java.io.File(publicDownloads, "FileCourier")
        if (!fileCourierDir.exists()) {
            fileCourierDir.mkdirs()
        }
        return fileCourierDir.absolutePath
    }

    /**
     * Returns a map of Device ID to Device Name
     */
    fun getTrustedDevices(): Map<String, String> {
        val set = prefs.getStringSet("trusted_devices_v2", emptySet()) ?: emptySet()
        return set.associate { 
            val parts = it.split("|", limit = 2)
            parts[0] to (parts.getOrNull(1) ?: "Unknown Device")
        }
    }

    fun addTrustedDevice(id: String, name: String) {
        val current = prefs.getStringSet("trusted_devices_v2", emptySet())?.toMutableSet() ?: mutableSetOf()
        // Remove existing entry for this ID if any
        current.removeIf { it.startsWith("$id|") }
        current.add("$id|$name")
        prefs.edit { putStringSet("trusted_devices_v2", current) }
    }

    fun removeTrustedDevice(id: String) {
        val current = prefs.getStringSet("trusted_devices_v2", emptySet())?.toMutableSet() ?: mutableSetOf()
        current.removeIf { it.startsWith("$id|") }
        prefs.edit { putStringSet("trusted_devices_v2", current) }
    }

    fun isDeviceTrusted(id: String): Boolean {
        val set = prefs.getStringSet("trusted_devices_v2", emptySet()) ?: emptySet()
        return set.any { it.startsWith("$id|") }
    }
}
