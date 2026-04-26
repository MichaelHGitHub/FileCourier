package com.filecourier.app.ui

import android.net.Uri
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Close

import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.filecourier.app.viewmodel.DiscoveryViewModel
import com.filecourier.app.viewmodel.TransferViewModel

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SendScreen(
    discoveryViewModel: DiscoveryViewModel,
    transferViewModel: TransferViewModel,
    onNavigateToDeviceSelection: () -> Unit,
) {
    val selectedPeer by discoveryViewModel.selectedPeer.collectAsState()
    var textMessage by remember { mutableStateOf("") }
    val selectedFiles = remember { mutableStateListOf<Uri>() }
    
    val filePickerLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.GetMultipleContents(),
    ) { uris ->
        selectedFiles.addAll(uris)
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("FileCourier") },
            )
        },
    ) { paddingValues ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
                .padding(16.dp),
        ) {
            // 1. Selected Device Section
            Text("Selected Device", style = MaterialTheme.typography.titleMedium)
            Spacer(modifier = Modifier.height(8.dp))
            Card(
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors(
                    containerColor = MaterialTheme.colorScheme.secondaryContainer,
                ),
            ) {
                Row(
                    modifier = Modifier
                        .padding(16.dp)
                        .fillMaxWidth(),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.SpaceBetween,
                ) {
                    Column(modifier = Modifier.weight(1f)) {
                        if (selectedPeer != null) {
                            Text(selectedPeer!!.deviceName, style = MaterialTheme.typography.bodyLarge)
                            Text(selectedPeer!!.ipAddress, style = MaterialTheme.typography.bodySmall)
                        } else {
                            Text("No device selected", style = MaterialTheme.typography.bodyLarge, color = MaterialTheme.colorScheme.outline)
                        }
                    }
                    Button(onClick = onNavigateToDeviceSelection) {
                        Text(if (selectedPeer == null) "Select" else "Change")
                    }
                }
            }

            Spacer(modifier = Modifier.height(24.dp))

            // 2. Message Section
            Text("Message", style = MaterialTheme.typography.titleMedium)
            Spacer(modifier = Modifier.height(8.dp))
            OutlinedTextField(
                value = textMessage,
                onValueChange = { textMessage = it },
                label = { Text("Enter text to send") },
                modifier = Modifier.fillMaxWidth(),
            )
            Spacer(modifier = Modifier.height(8.dp))
            Button(
                onClick = {
                    val peer = selectedPeer
                    if ((peer != null) && textMessage.isNotBlank()) {
                        transferViewModel.sendTextPayload(
                            host = peer.ipAddress,
                            port = peer.tcpPort,
                            targetId = peer.deviceId,
                            text = textMessage,
                        )
                        textMessage = "" // Clear after send
                    }
                },
                enabled = (selectedPeer != null) && textMessage.isNotBlank(),
                modifier = Modifier.fillMaxWidth(),
            ) {
                Text("Send Text")
            }

            Spacer(modifier = Modifier.height(24.dp))

            // 3. Add Files Section
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Text("Selected Files", style = MaterialTheme.typography.titleMedium)
                TextButton(
                    onClick = { filePickerLauncher.launch("*/*") },
                    enabled = selectedPeer != null,
                ) {
                    Text("Add")
                }
            }
            
            if (selectedFiles.isNotEmpty()) {
                LazyColumn(
                    modifier = Modifier
                        .weight(1f)
                        .fillMaxWidth(),
                ) {
                    items(selectedFiles) { uri ->
                        ListItem(
                            headlineContent = { Text(uri.lastPathSegment ?: "Unknown file") },
                            trailingContent = {
                                IconButton(onClick = { selectedFiles.remove(uri) }) {
                                    Icon(Icons.Default.Close, contentDescription = "Remove")
                                }
                            },
                        )
                    }
                }
            } else {
                Box(
                    modifier = Modifier
                        .weight(1f)
                        .fillMaxWidth(),
                    contentAlignment = Alignment.Center,
                ) {
                    Text("No files selected", color = MaterialTheme.colorScheme.outline)
                }
            }

            Spacer(modifier = Modifier.height(16.dp))

            // 4. Transfer Button
            Button(
                onClick = {
                    val peer = selectedPeer
                    if (peer != null) {
                        if (textMessage.isNotBlank()) {
                            transferViewModel.sendTextPayload(
                                host = peer.ipAddress,
                                port = peer.tcpPort,
                                targetId = peer.deviceId,
                                text = textMessage,
                            )
                            textMessage = ""
                        }
                        if (selectedFiles.isNotEmpty()) {
                            transferViewModel.sendFilePayload(
                                host = peer.ipAddress,
                                port = peer.tcpPort,
                                targetId = peer.deviceId,
                                uris = selectedFiles.toList(),
                            )
                            selectedFiles.clear()
                        }
                    }
                },
                enabled = (selectedPeer != null) && (textMessage.isNotBlank() || selectedFiles.isNotEmpty()),
                modifier = Modifier.fillMaxWidth(),
                colors = ButtonDefaults.buttonColors(
                    containerColor = MaterialTheme.colorScheme.primary,
                ),
            ) {
                Text("Send")
            }
            
            if (selectedPeer == null) {
                Text(
                    "Please select a device first to enable transfer.",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.error,
                    modifier = Modifier.align(Alignment.CenterHorizontally),
                )
            }
        }
    }
}

