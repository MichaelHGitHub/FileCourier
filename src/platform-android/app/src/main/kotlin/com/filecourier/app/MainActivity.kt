package com.filecourier.app

import android.content.Intent
import android.os.Bundle
import android.util.Log
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.viewModels
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import com.filecourier.app.ui.components.ReceiveDialog
import com.filecourier.app.ui.navigation.AppNavigation
import com.filecourier.app.viewmodel.DiscoveryViewModel
import com.filecourier.app.viewmodel.HistoryViewModel
import com.filecourier.app.viewmodel.TransferViewModel

class MainActivity : ComponentActivity() {

    private val discoveryViewModel: DiscoveryViewModel by viewModels()
    private val transferViewModel: TransferViewModel by viewModels()
    private val historyViewModel: HistoryViewModel by viewModels()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        
        handleIntent(intent)
        
        setContent {
            val incomingRequestState by transferViewModel.incomingRequest.collectAsState()

            MaterialTheme {
                Surface(
                    modifier = Modifier.fillMaxSize(),
                    color = MaterialTheme.colorScheme.background,
                ) {
                    Box(modifier = Modifier.fillMaxSize()) {
                        AppNavigation(
                            discoveryViewModel = discoveryViewModel,
                            transferViewModel = transferViewModel,
                            historyViewModel = historyViewModel,
                        )
                        
                        // Global overlay for incoming requests
                        ReceiveDialog(
                            incomingState = incomingRequestState,
                            transferViewModel = transferViewModel,
                        )
                    }
                }
            }
        }
    }

    override fun onNewIntent(intent: Intent?) {
        super.onNewIntent(intent)
        intent?.let { handleIntent(it) }
    }

    private fun handleIntent(intent: Intent) {
        when (intent.action) {
            Intent.ACTION_SEND -> {
                Log.d("MainActivity", "Handling single shared file")
                // Extract URI and push to TransferViewModel
            }
            Intent.ACTION_SEND_MULTIPLE -> {
                Log.d("MainActivity", "Handling multiple shared files")
                // Extract URIs and push to TransferViewModel
            }
        }
    }
}
