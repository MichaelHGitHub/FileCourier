package com.filecourier.app.ui

import android.content.ClipboardManager
import android.content.Context
import android.widget.Toast
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.ContentCopy
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.filecourier.app.data.TransferHistoryRecord
import com.filecourier.app.viewmodel.HistoryViewModel
import java.io.File
import java.text.SimpleDateFormat
import java.util.*

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun FileDetailsScreen(
    transferId: String,
    historyViewModel: HistoryViewModel,
    onBack: () -> Unit
) {
    var record by remember { mutableStateOf<TransferHistoryRecord?>(null) }
    val context = LocalContext.current
    val dateFormatter = remember { SimpleDateFormat("MMM dd, yyyy HH:mm:ss", Locale.getDefault()) }

    LaunchedEffect(transferId) {
        record = historyViewModel.getRecordById(transferId)
    }

    val isMessage = remember(record) { record != null && record!!.itemPath.isEmpty() }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { 
                    val title = if (record == null) "Details" 
                                else if (isMessage) "Message Details" 
                                else "File Details"
                    Text(title) 
                },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.Default.ArrowBack, contentDescription = "Back")
                    }
                }
            )
        }
    ) { paddingValues ->
        if (record == null) {
            Box(modifier = Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                CircularProgressIndicator()
            }
        } else {
            val currentRecord = record!!
            Column(
                modifier = Modifier
                    .padding(paddingValues)
                    .fillMaxSize()
                    .padding(16.dp)
                    .verticalScroll(rememberScrollState()),
                verticalArrangement = Arrangement.spacedBy(16.dp)
            ) {
                if (isMessage) {
                    Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                        Text(text = "Message", style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.primary)
                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(8.dp),
                            modifier = Modifier.fillMaxWidth()
                        ) {
                            Card(
                                modifier = Modifier.weight(1f),
                                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant)
                            ) {
                                Text(
                                    text = currentRecord.itemName,
                                    modifier = Modifier.padding(12.dp),
                                    style = MaterialTheme.typography.bodyLarge
                                )
                            }
                            IconButton(
                                onClick = {
                                    val clipboard = context.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                                    val clip = android.content.ClipData.newPlainText("FileCourier Message", currentRecord.itemName)
                                    clipboard.setPrimaryClip(clip)
                                    Toast.makeText(context, "Message copied to clipboard", Toast.LENGTH_SHORT).show()
                                },
                                modifier = Modifier.size(48.dp)
                            ) {
                                Icon(
                                    imageVector = Icons.Default.ContentCopy,
                                    contentDescription = "Copy Message",
                                    tint = MaterialTheme.colorScheme.primary
                                )
                            }
                        }
                    }
                } else {
                    DetailItem(label = "Filename", value = currentRecord.itemName)
                }

                DetailItem(label = "Status", value = currentRecord.status)
                DetailItem(label = "Direction", value = currentRecord.direction)
                DetailItem(label = "Counterparty", value = currentRecord.counterpartyName)
                DetailItem(label = "Date", value = dateFormatter.format(Date(currentRecord.timestamp)))
                
                if (currentRecord.itemPath.isNotEmpty()) {
                    val file = remember(currentRecord.itemPath) { File(currentRecord.itemPath) }
                    val locationPath = file.parent ?: ""
                    
                    if (locationPath.isNotEmpty()) {
                        Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                            Text(text = "Storage Location", style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.primary)
                            Row(
                                verticalAlignment = Alignment.CenterVertically,
                                horizontalArrangement = Arrangement.spacedBy(8.dp),
                                modifier = Modifier.fillMaxWidth()
                            ) {
                                Card(
                                    modifier = Modifier.weight(1f),
                                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant)
                                ) {
                                    Text(
                                        text = locationPath,
                                        modifier = Modifier.padding(12.dp),
                                        style = MaterialTheme.typography.bodyMedium
                                    )
                                }
                                IconButton(onClick = {
                                    val clipboard = context.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                                    val clip = android.content.ClipData.newPlainText("Location Path", locationPath)
                                    clipboard.setPrimaryClip(clip)
                                    Toast.makeText(context, "Location path copied to clipboard", Toast.LENGTH_SHORT).show()
                                }) {
                                    Icon(Icons.Default.ContentCopy, contentDescription = "Copy Location Path")
                                }
                            }
                        }
                    }
                }

                if (currentRecord.totalFiles > 1) {
                    DetailItem(label = "Total Files", value = currentRecord.totalFiles.toString())
                }
                
                val sizeStr = if (currentRecord.totalSize > 1024 * 1024) {
                    String.format("%.2f MB", currentRecord.totalSize / (1024.0 * 1024.0))
                } else {
                    String.format("%.2f KB", currentRecord.totalSize / 1024.0)
                }
                DetailItem(label = "Total Size", value = sizeStr)
            }
        }
    }
}

@Composable
fun DetailItem(label: String, value: String) {
    Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
        Text(text = label, style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.primary)
        Text(text = value, style = MaterialTheme.typography.bodyLarge, fontWeight = FontWeight.Medium)
    }
}
