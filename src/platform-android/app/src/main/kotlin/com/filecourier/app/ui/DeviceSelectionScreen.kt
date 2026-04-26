package com.filecourier.app.ui

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.filecourier.app.viewmodel.DiscoveryViewModel
import com.filecourier.network.models.PeerDevice

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DeviceSelectionScreen(
    discoveryViewModel: DiscoveryViewModel,
    onDeviceSelected: () -> Unit,
) {
    val peers by discoveryViewModel.discoveredPeers.collectAsState()
    var manualIp by remember { mutableStateOf("") }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Select Device") },
                navigationIcon = {
                    IconButton(onClick = onDeviceSelected) {
                        Icon(Icons.Default.ArrowBack, contentDescription = "Back")
                    }
                },
            )
        },
    ) { paddingValues ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
                .padding(16.dp),
        ) {
            Text("Manual IP Address", style = MaterialTheme.typography.titleMedium)
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                OutlinedTextField(
                    value = manualIp,
                    onValueChange = { manualIp = it },
                    label = { Text("IP Address (e.g. 192.168.1.5)") },
                    modifier = Modifier.weight(1f),
                )
                Spacer(modifier = Modifier.width(8.dp))
                Button(
                    onClick = {
                        if (manualIp.isNotBlank()) {
                            discoveryViewModel.setSelectedPeer(
                                PeerDevice(
                                    deviceId = "manual",
                                    deviceName = "Manual IP ($manualIp)",
                                    ipAddress = manualIp,
                                    tcpPort = 45000,
                                    os = "Unknown",
                                    lastSeenTimestamp = System.currentTimeMillis(),
                                ),
                            )
                            onDeviceSelected()
                        }
                    },
                    enabled = manualIp.isNotBlank(),
                ) {
                    Text("Use")
                }
            }

            Spacer(modifier = Modifier.height(24.dp))

            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.SpaceBetween,
            ) {
                Text("Online Devices (${peers.size})", style = MaterialTheme.typography.titleMedium)
                IconButton(onClick = { discoveryViewModel.triggerManualRefresh() }) {
                    Icon(Icons.Default.Refresh, contentDescription = "Refresh")
                }
            }
            Spacer(modifier = Modifier.height(8.dp))

            if (peers.isEmpty()) {
                Box(modifier = Modifier.weight(1f), contentAlignment = Alignment.Center) {
                    Text(
                        "Searching for devices...",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            } else {
                LazyColumn(modifier = Modifier.weight(1f)) {
                    items(peers) { peer ->
                        Card(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(vertical = 4.dp)
                                .clickable {
                                    discoveryViewModel.setSelectedPeer(peer)
                                    onDeviceSelected()
                                },
                            colors = CardDefaults.cardColors(
                                containerColor = MaterialTheme.colorScheme.surfaceVariant,
                            ),
                        ) {
                            Column(modifier = Modifier.padding(16.dp)) {
                                Text(peer.deviceName, style = MaterialTheme.typography.bodyLarge)
                                Text("${peer.os} • ${peer.ipAddress}", style = MaterialTheme.typography.bodySmall)
                            }
                        }
                    }
                }
            }
        }
    }
}
