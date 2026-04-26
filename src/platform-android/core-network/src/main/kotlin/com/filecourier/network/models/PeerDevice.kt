package com.filecourier.network.models

data class PeerDevice(
    val deviceId: String,
    val deviceName: String,
    val os: String,
    val ipAddress: String,
    val tcpPort: Int,
    val lastSeenTimestamp: Long
)
