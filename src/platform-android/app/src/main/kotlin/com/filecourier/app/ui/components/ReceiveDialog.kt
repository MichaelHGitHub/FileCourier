package com.filecourier.app.ui.components

import androidx.compose.foundation.layout.Row
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import com.filecourier.app.viewmodel.IncomingRequestState
import com.filecourier.app.viewmodel.TransferViewModel

@Composable
fun ReceiveDialog(
    incomingState: IncomingRequestState,
    transferViewModel: TransferViewModel,
) {
    if (incomingState is IncomingRequestState.Pending) {
        val header = incomingState.header
        val filesList = header.Files
        
        AlertDialog(
            onDismissRequest = { transferViewModel.respondToRequest(accept = false) },
            title = { Text("Incoming Transfer") },
            text = {
                val desc = if (!filesList.isNullOrEmpty()) {
                    "${header.SenderName} wants to send you ${filesList.size} files."
                } else if (header.TextPayload != null) {
                    "${header.SenderName} sent a text message:\n\n${header.TextPayload}"
                } else {
                    "${header.SenderName} sent an empty request."
                }
                Text(desc)
            },
            confirmButton = {
                Row {
                    if (!filesList.isNullOrEmpty()) {
                        if (!incomingState.isAutoAccepted) {
                            TextButton(onClick = { transferViewModel.respondToRequest(accept = true, alwaysAccept = true) }) {
                                Text("Always Allow")
                            }
                            Button(onClick = { transferViewModel.respondToRequest(accept = true, alwaysAccept = false) }) {
                                Text("Allow")
                            }
                        } else {
                            Button(onClick = { transferViewModel.respondToRequest(accept = true) }) {
                                Text("OK")
                            }
                        }
                    } else if (header.TextPayload != null) {
                        val text = header.TextPayload!!
                        TextButton(onClick = { transferViewModel.copyToClipboard(text) }) {
                            Text("Copy to clipboard")
                        }
                        Button(onClick = { transferViewModel.respondToRequest(accept = true) }) {
                            Text("OK")
                        }
                    } else {
                        Button(onClick = { transferViewModel.respondToRequest(accept = true) }) {
                            Text("OK")
                        }
                    }
                }
            },
            dismissButton = {
                if (!filesList.isNullOrEmpty()) {
                    TextButton(onClick = { transferViewModel.respondToRequest(accept = false) }) {
                        Text("Reject")
                    }
                }
            },
        )
    }
}
