package com.filecourier.app.viewmodel

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import com.filecourier.app.data.SettingsRepository
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

class SettingsViewModel(application: Application) : AndroidViewModel(application) {
    private val repository = SettingsRepository(application)

    private val _deviceName = MutableStateFlow(repository.deviceName)
    val deviceName: StateFlow<String> = _deviceName.asStateFlow()

    private val _defaultSaveLocation = MutableStateFlow(repository.defaultSaveLocation)
    val defaultSaveLocation: StateFlow<String> = _defaultSaveLocation.asStateFlow()

    private val _trustedDevices = MutableStateFlow(repository.getTrustedDevices())
    val trustedDevices: StateFlow<Map<String, String>> = _trustedDevices.asStateFlow()

    fun getDefaultSaveLocationPath(): String {
        return repository.getDefaultSaveLocationPath()
    }

    fun updateDeviceName(name: String) {
        repository.deviceName = name
        _deviceName.value = name
    }

    fun updateDefaultSaveLocation(location: String) {
        repository.defaultSaveLocation = location
        _defaultSaveLocation.value = location
    }

    fun removeTrustedDevice(id: String) {
        repository.removeTrustedDevice(id)
        _trustedDevices.value = repository.getTrustedDevices()
    }
}
