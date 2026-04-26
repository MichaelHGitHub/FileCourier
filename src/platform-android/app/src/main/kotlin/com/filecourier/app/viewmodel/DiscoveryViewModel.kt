package com.filecourier.app.viewmodel

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.filecourier.app.data.SettingsRepository
import com.filecourier.network.DiscoveryClient
import com.filecourier.network.models.PeerDevice
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

class DiscoveryViewModel(application: Application) : AndroidViewModel(application) {
    private val settingsRepo = SettingsRepository(application)
    private val discoveryClient = DiscoveryClient(
        context = application,
        myDeviceId = settingsRepo.deviceId,
        myDeviceName = settingsRepo.deviceName,
        myTcpPort = 45455, // Match Windows default TCP port
    )

    val discoveredPeers: StateFlow<List<PeerDevice>> = discoveryClient.discoveredPeers
    
    private val _selectedPeer = MutableStateFlow<PeerDevice?>(null)
    val selectedPeer: StateFlow<PeerDevice?> = _selectedPeer.asStateFlow()

    fun setSelectedPeer(peer: PeerDevice?) {
        _selectedPeer.value = peer
    }

    init {
        startNetworkDiscovery()
    }

    private fun startNetworkDiscovery() {
        viewModelScope.launch {
            discoveryClient.startListening()
        }
        viewModelScope.launch {
            discoveryClient.startBroadcasting()
        }
    }

    fun triggerManualRefresh() {
        // Since broadcasting is continuous, manual refresh just emits another heartbeat immediately
        viewModelScope.launch {
            discoveryClient.startBroadcasting() // Will overlap, but DiscoveryClient handles it
        }
    }

    override fun onCleared() {
        super.onCleared()
        viewModelScope.launch {
            discoveryClient.sendGoodbye()
        }
    }
}
