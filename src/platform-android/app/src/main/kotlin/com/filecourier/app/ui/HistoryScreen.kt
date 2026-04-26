package com.filecourier.app.ui

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.core.content.FileProvider
import com.filecourier.app.viewmodel.HistoryViewModel
import java.io.File
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import android.content.Intent
import android.net.Uri

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun HistoryScreen(
    historyViewModel: HistoryViewModel,
    onOpenDrawer: () -> Unit,
) {
    var expanded by remember { mutableStateOf(value = false) }
    var currentFilter by remember { mutableStateOf(value = "All") }

    val sentHistory by historyViewModel.sentHistory.collectAsState()
    val receivedHistory by historyViewModel.receivedHistory.collectAsState()

    val combinedHistory = when (currentFilter) {
        "Sent" -> sentHistory
        "Received" -> receivedHistory
        else -> (sentHistory + receivedHistory).sortedByDescending { it.timestamp }
    }

    val dateFormatter = remember { SimpleDateFormat("MMM dd, yyyy HH:mm", Locale.getDefault()) }
    val context = LocalContext.current

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("History") },
                navigationIcon = {
                    IconButton(onClick = onOpenDrawer) {
                        Icon(Icons.Default.Menu, contentDescription = "Menu")
                    }
                },
                actions = {
                    Box {
                        IconButton(onClick = { expanded = true }) {
                            Icon(Icons.Default.MoreVert, contentDescription = "Filter")
                        }
                        DropdownMenu(
                            expanded = expanded,
                            onDismissRequest = { expanded = false },
                        ) {
                            DropdownMenuItem(
                                text = { Text("All") },
                                onClick = { 
                                    currentFilter = "All"
                                    expanded = false 
                                },
                            )
                            DropdownMenuItem(
                                text = { Text("Sent") },
                                onClick = { 
                                    currentFilter = "Sent"
                                    expanded = false 
                                },
                            )
                            DropdownMenuItem(
                                text = { Text("Received") },
                                onClick = { 
                                    currentFilter = "Received"
                                    expanded = false 
                                },
                            )
                        }
                    }
                }
            )
        },
    ) { paddingValues ->
        LazyColumn(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues),
            contentPadding = PaddingValues(all = 16.dp),
        ) {
            item {
                Row(
                    modifier = Modifier.fillMaxWidth().padding(bottom = 16.dp),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    Text(
                        text = "$currentFilter (${combinedHistory.size})",
                        style = MaterialTheme.typography.titleMedium,
                    )
                    if ((currentFilter != "All") && combinedHistory.isNotEmpty()) {
                        TextButton(onClick = { historyViewModel.clearHistory(currentFilter) }) {
                            Text("Clear $currentFilter")
                        }
                    }
                }
            }
            
            if (combinedHistory.isEmpty()) {
                item {
                    Text("No history records found.", color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            } else {
                items(combinedHistory) { record ->
                    val isReceived = record.direction == "Received"
                    Card(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(vertical = 8.dp),
                        colors = if (isReceived) {
                            CardDefaults.cardColors(
                                containerColor = MaterialTheme.colorScheme.secondaryContainer,
                                contentColor = MaterialTheme.colorScheme.onSecondaryContainer
                            )
                        } else {
                            CardDefaults.cardColors()
                        }
                    ) {
                        Row(
                            modifier = Modifier.padding(16.dp).fillMaxWidth(),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically,
                        ) {
                            Column(modifier = Modifier.weight(1f)) {
                                Row(verticalAlignment = Alignment.CenterVertically) {
                                    Icon(
                                        imageVector = if (isReceived) Icons.Default.KeyboardArrowDown else Icons.Default.KeyboardArrowUp,
                                        contentDescription = null,
                                        modifier = Modifier.size(16.dp),
                                        tint = if (isReceived) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.secondary
                                    )
                                    Spacer(modifier = Modifier.width(8.dp))
                                    Text(
                                        text = record.itemName + if (record.totalFiles > 1) " (+${record.totalFiles - 1} files)" else "",
                                        style = MaterialTheme.typography.bodyLarge,
                                    )
                                }
                                Spacer(modifier = Modifier.height(4.dp))
                                Text(
                                    text = "${record.direction} • ${record.counterpartyName} • ${record.status}",
                                    style = MaterialTheme.typography.bodySmall,
                                    color = if (isReceived) MaterialTheme.colorScheme.onSecondaryContainer.copy(alpha = 0.7f) else MaterialTheme.colorScheme.onSurfaceVariant,
                                )
                                Text(
                                    text = dateFormatter.format(Date(record.timestamp)),
                                    style = MaterialTheme.typography.bodySmall,
                                    color = if (isReceived) MaterialTheme.colorScheme.onSecondaryContainer.copy(alpha = 0.7f) else MaterialTheme.colorScheme.onSurfaceVariant,
                                )
                            }
                            Row {
                                if (isReceived && record.status == "Completed" && record.itemPath.isNotEmpty()) {
                                    val file = remember { File(record.itemPath) }
                                    if (file.exists()) {
                                        IconButton(onClick = {
                                            try {
                                                val uri = FileProvider.getUriForFile(
                                                    context,
                                                    "${context.packageName}.provider",
                                                    file
                                                )
                                                val intent = Intent(Intent.ACTION_VIEW).apply {
                                                    setDataAndType(uri, context.contentResolver.getType(uri))
                                                    flags = Intent.FLAG_GRANT_READ_URI_PERMISSION
                                                }
                                                context.startActivity(Intent.createChooser(intent, "Open File"))
                                            } catch (e: Exception) {
                                            }
                                        }) {
                                            Icon(Icons.Default.PlayArrow, contentDescription = "Open File")
                                        }

                                        IconButton(onClick = {
                                            try {
                                                val folder = file.parentFile
                                                if (folder != null) {
                                                    val uri = FileProvider.getUriForFile(
                                                        context,
                                                        "${context.packageName}.provider",
                                                        folder
                                                    )
                                                    val intent = Intent(Intent.ACTION_VIEW).apply {
                                                        setDataAndType(uri, "resource/folder")
                                                        flags = Intent.FLAG_GRANT_READ_URI_PERMISSION
                                                    }
                                                    context.startActivity(intent)
                                                }
                                            } catch (e: Exception) {
                                            }
                                        }) {
                                            Icon(Icons.Default.Search, contentDescription = "Open Location")
                                        }
                                    }
                                }
                                IconButton(onClick = { historyViewModel.deleteRecord(record.transferId) }) {
                                    Icon(Icons.Default.Delete, contentDescription = "Delete Record")
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
