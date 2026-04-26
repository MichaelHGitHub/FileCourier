package com.filecourier.app.ui

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.filecourier.app.viewmodel.SettingsViewModel

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SettingsScreen(
    viewModel: SettingsViewModel,
    onBack: () -> Unit,
) {
    val deviceName by viewModel.deviceName.collectAsState()
    val defaultSaveLocation by viewModel.defaultSaveLocation.collectAsState()
    val trustedDevices by viewModel.trustedDevices.collectAsState()

    var showNameDialog by remember { mutableStateOf(value = false) }
    var newName by remember { mutableStateOf(value = deviceName) }

    var showPathDialog by remember { mutableStateOf(value = false) }
    var newPath by remember { mutableStateOf(value = defaultSaveLocation) }

    if (showNameDialog) {
        AlertDialog(
            onDismissRequest = { showNameDialog = false },
            title = { Text("Edit Device Name") },
            text = {
                TextField(
                    value = newName,
                    onValueChange = { newName = it },
                    singleLine = true,
                )
            },
            confirmButton = {
                TextButton(
                    onClick = {
                        viewModel.updateDeviceName(newName)
                        showNameDialog = false
                    },
                ) {
                    Text("Save")
                }
            },
            dismissButton = {
                TextButton(onClick = { showNameDialog = false }) {
                    Text("Cancel")
                }
            },
        )
    }

    if (showPathDialog) {
        AlertDialog(
            onDismissRequest = { showPathDialog = false },
            title = { Text("Default Save Location") },
            text = {
                Column {
                    Text(
                        "Enter the absolute path where files should be saved.",
                        style = MaterialTheme.typography.bodySmall,
                    )
                    Spacer(modifier = Modifier.height(8.dp))
                    TextField(
                        value = newPath,
                        onValueChange = { newPath = it },
                        label = { Text("Path") },
                        placeholder = { Text(viewModel.getDefaultSaveLocationPath()) },
                    )
                }
            },
            confirmButton = {
                TextButton(
                    onClick = {
                        viewModel.updateDefaultSaveLocation(newPath)
                        showPathDialog = false
                    },
                ) {
                    Text("Save")
                }
            },
            dismissButton = {
                TextButton(onClick = { showPathDialog = false }) {
                    Text("Cancel")
                }
            },
        )
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Settings") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.Default.ArrowBack, contentDescription = "Back")
                    }
                },
            )
        },
    ) { paddingValues ->
        LazyColumn(
            modifier = Modifier
                .padding(paddingValues)
                .fillMaxSize()
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp),
        ) {
            item {
                Text("General", style = MaterialTheme.typography.titleMedium)
            }

            item {
                SettingsItem(
                    title = "Device Name",
                    subtitle = deviceName,
                ) {
                    newName = deviceName
                    showNameDialog = true
                }
            }

            item {
                SettingsItem(
                    title = "Default Save Location",
                    subtitle = viewModel.getDefaultSaveLocationPath(),
                ) {
                    newPath = defaultSaveLocation
                    showPathDialog = true
                }
            }

            item {
                Divider()
                Spacer(modifier = Modifier.height(8.dp))
                Text("Security", style = MaterialTheme.typography.titleMedium)
            }

            item {
                Text("Trusted Devices", style = MaterialTheme.typography.bodyMedium)
                if (trustedDevices.isEmpty()) {
                    Text("No trusted devices yet", style = MaterialTheme.typography.bodySmall)
                }
            }

            items(trustedDevices.toList()) { (deviceId, name) ->
                Row(
                    modifier = Modifier.fillMaxWidth().padding(vertical = 4.dp),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    Column(modifier = Modifier.weight(1f)) {
                        Text(name, style = MaterialTheme.typography.bodyLarge)
                        Text(
                            deviceId,
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                    }
                    IconButton(onClick = { viewModel.removeTrustedDevice(deviceId) }) {
                        Icon(Icons.Default.Delete, contentDescription = "Remove")
                    }
                }
            }
        }
    }
}

@Composable
fun SettingsItem(
    title: String,
    subtitle: String,
    onClick: () -> Unit,
) {
    Surface(
        onClick = onClick,
        modifier = Modifier.fillMaxWidth(),
    ) {
        Column(modifier = Modifier.padding(vertical = 8.dp)) {
            Text(title, style = MaterialTheme.typography.bodyLarge)
            Text(
                subtitle,
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}
