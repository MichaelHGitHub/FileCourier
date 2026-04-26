package com.filecourier.app.data

import android.content.Context
import android.content.SharedPreferences
import java.util.UUID

class SettingsRepository(context: Context) {
    private val prefs: SharedPreferences = context.getSharedPreferences("filecourier_settings", Context.MODE_PRIVATE)

    val deviceId: String
        get() {
            var id = prefs.getString("device_id", null)
            if (id == null) {
                id = UUID.randomUUID().toString()
                prefs.edit().putString("device_id", id).apply()
            }
            return id
        }

    var deviceName: String
        get() = prefs.getString("device_name", android.os.Build.MODEL) ?: "Android Device"
        set(value) = prefs.edit().putString("device_name", value).apply()

    fun getTrustedDevices(): Set<String> {
        return prefs.getStringSet("trusted_devices", emptySet()) ?: emptySet()
    }

    fun addTrustedDevice(id: String) {
        val current = getTrustedDevices().toMutableSet()
        current.add(id)
        prefs.edit().putStringSet("trusted_devices", current).apply()
    }

    fun removeTrustedDevice(id: String) {
        val current = getTrustedDevices().toMutableSet()
        current.remove(id)
        prefs.edit().putStringSet("trusted_devices", current).apply()
    }

    fun isDeviceTrusted(id: String): Boolean {
        return getTrustedDevices().contains(id)
    }
}
