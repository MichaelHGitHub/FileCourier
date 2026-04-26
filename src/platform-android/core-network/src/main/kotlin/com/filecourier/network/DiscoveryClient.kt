package com.filecourier.network

import android.content.Context
import android.net.wifi.WifiManager
import android.util.Log
import com.filecourier.network.models.DiscoveryPayload
import com.filecourier.network.models.PeerDevice
import com.google.gson.Gson
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.isActive
import kotlinx.coroutines.withContext
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.net.SocketTimeoutException

class DiscoveryClient(context: Context, private val myDeviceId: String, private val myDeviceName: String, private val myTcpPort: Int) {
    private val wifiManager = context.applicationContext.getSystemService(Context.WIFI_SERVICE) as WifiManager
    private val multicastLock = wifiManager.createMulticastLock("FileCourier:Discovery")
    private val gson = Gson()

    private val _discoveredPeers = MutableStateFlow<List<PeerDevice>>(emptyList())
    val discoveredPeers: StateFlow<List<PeerDevice>> = _discoveredPeers.asStateFlow()

    private val peerMap = mutableMapOf<String, PeerDevice>()
    private val peerTimeoutMs = 10000L // 10 seconds timeout

    suspend fun startListening(port: Int = 45454) = withContext(Dispatchers.IO) {
        multicastLock.acquire()
        var socket: DatagramSocket? = null
        try {
            socket = DatagramSocket(port)
            socket.broadcast = true
            socket.soTimeout = 2000 // Short timeout so we can check for inactive peers
            val buffer = ByteArray(4096)

            while (isActive) {
                try {
                    val packet = DatagramPacket(buffer, buffer.size)
                    socket.receive(packet)
                    val message = String(packet.data, 0, packet.length)
                    val senderIp = packet.address.hostAddress ?: continue
                    
                    val payload = gson.fromJson(message, DiscoveryPayload::class.java)
                    if (payload.DeviceId == myDeviceId) continue // Ignore own broadcasts

                    if (payload.IsGoodbye) {
                        peerMap.remove(payload.DeviceId)
                    } else {
                        peerMap[payload.DeviceId] = PeerDevice(
                            deviceId = payload.DeviceId,
                            deviceName = payload.DeviceName,
                            os = payload.OS,
                            ipAddress = senderIp,
                            tcpPort = payload.TcpPort,
                            lastSeenTimestamp = System.currentTimeMillis(),
                        )
                    }
                } catch (_: SocketTimeoutException) {
                    // Ignore, just used to unblock receive and check peer timeouts
                } catch (e: Exception) {
                    Log.e("DiscoveryClient", "Error parsing discovery message", e)
                }

                // Cleanup stale peers
                val now = System.currentTimeMillis()
                val activePeers = peerMap.values.filter { (now - it.lastSeenTimestamp) < peerTimeoutMs }
                peerMap.clear()
                activePeers.forEach { peerMap[it.deviceId] = it }
                
                _discoveredPeers.value = activePeers.toList()
            }
        } catch (e: Exception) {
            Log.e("DiscoveryClient", "Error during discovery listening", e)
        } finally {
            socket?.close()
            if (multicastLock.isHeld) {
                multicastLock.release()
            }
        }
    }

    suspend fun startBroadcasting(port: Int = 45454) = withContext(Dispatchers.IO) {
        var socket: DatagramSocket? = null
        try {
            // Try to find the local WiFi address to bind to
            val localAddress = getLocalWifiAddress()
            socket = if (localAddress != null) DatagramSocket(0, localAddress) else DatagramSocket()
            socket.broadcast = true
            
            val broadcastAddress = getBroadcastAddress() ?: InetAddress.getByName("255.255.255.255")
            Log.d("DiscoveryClient", "Broadcasting from ${socket.localAddress} to $broadcastAddress:$port")
            
            while (isActive) {
                val payload = DiscoveryPayload(
                    DeviceId = myDeviceId,
                    DeviceName = myDeviceName,
                    TcpPort = myTcpPort,
                    IsGoodbye = false,
                    MacAddress = "00:00:00:00:00:00",
                )
                val data = gson.toJson(payload).toByteArray()
                val packet = DatagramPacket(data, data.size, broadcastAddress, port)
                socket.send(packet)
                delay(3000) // Heartbeat every 3 seconds
            }
        } catch (e: Exception) {
            Log.e("DiscoveryClient", "Error during broadcasting", e)
        } finally {
            socket?.close()
        }
    }

    private fun getLocalWifiAddress(): InetAddress? {
        return try {
            @Suppress("DEPRECATION")
            val ipInt = wifiManager.connectionInfo.ipAddress
            if (ipInt == 0) return null
            val quads = ByteArray(4)
            for (k in 0..3) quads[k] = (ipInt shr (k * 8) and 0xFF).toByte()
            InetAddress.getByAddress(quads)
        } catch (e: Exception) {
            Log.e("DiscoveryClient", "Error getting local wifi address", e)
            null
        }
    }

    private fun getBroadcastAddress(): InetAddress? {
        return try {
            @Suppress("DEPRECATION")
            val dhcp = wifiManager.dhcpInfo ?: return null
            @Suppress("DEPRECATION")
            val broadcast = (dhcp.ipAddress and dhcp.netmask) or dhcp.netmask.inv()
            val quads = ByteArray(4)
            for (k in 0..3) quads[k] = (broadcast shr (k * 8) and 0xFF).toByte()
            InetAddress.getByAddress(quads)
        } catch (e: Exception) {
            Log.e("DiscoveryClient", "Error calculating broadcast address", e)
            null
        }
    }

    suspend fun sendGoodbye(port: Int = 45454) = withContext(Dispatchers.IO) {
        var socket: DatagramSocket? = null
        try {
            socket = DatagramSocket()
            socket.broadcast = true
            val broadcastAddress = getBroadcastAddress() ?: InetAddress.getByName("255.255.255.255")
            val payload = DiscoveryPayload(
                DeviceId = myDeviceId,
                DeviceName = myDeviceName,
                TcpPort = myTcpPort,
                IsGoodbye = true,
                MacAddress = "00:00:00:00:00:00",
            )
            val data = gson.toJson(payload).toByteArray()
            val packet = DatagramPacket(data, data.size, broadcastAddress, port)
            socket.send(packet)
        } catch (e: Exception) {
            Log.e("DiscoveryClient", "Error sending goodbye", e)
        } finally {
            socket?.close()
        }
    }
}
