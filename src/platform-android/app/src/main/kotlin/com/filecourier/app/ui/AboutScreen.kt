package com.filecourier.app.ui

import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AboutScreen(onBack: () -> Unit) {
    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("About") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.Default.ArrowBack, contentDescription = "Back")
                    }
                },
            )
        }
    ) { paddingValues ->
        Column(
            modifier = Modifier
                .padding(paddingValues)
                .fillMaxSize()
                .padding(24.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center
        ) {
            Text(
                text = "FileCourier",
                fontSize = 32.sp,
                fontWeight = FontWeight.Bold,
                color = MaterialTheme.colorScheme.primary
            )
            Spacer(modifier = Modifier.height(8.dp))
            Text(
                text = "v1.0.0",
                style = MaterialTheme.typography.bodyMedium
            )
            Spacer(modifier = Modifier.height(24.dp))
            Text(
                text = "A simple, cross-platform file sharing application.",
                style = MaterialTheme.typography.bodyLarge,
                textAlign = androidx.compose.ui.text.style.TextAlign.Center
            )
            Text(
                text = "Developed by PandaSoft",
                style = MaterialTheme.typography.bodyMedium
            )
            Spacer(modifier = Modifier.height(48.dp))
            Text(
                text = "© 2026 PandaSoft",
                style = MaterialTheme.typography.bodySmall
            )
        }
    }
}
