package com.filecourier.app.ui.navigation

import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.List
import androidx.compose.material.icons.filled.Send
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.rememberNavController
import com.filecourier.app.ui.DeviceSelectionScreen
import com.filecourier.app.ui.SendScreen
import com.filecourier.app.ui.HistoryScreen
import com.filecourier.app.viewmodel.DiscoveryViewModel
import com.filecourier.app.viewmodel.HistoryViewModel
import com.filecourier.app.viewmodel.TransferViewModel

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AppNavigation(
    discoveryViewModel: DiscoveryViewModel,
    transferViewModel: TransferViewModel,
    historyViewModel: HistoryViewModel,
) {
    val navController = rememberNavController()
    val navBackStackEntry by navController.currentBackStackEntryAsState()
    val currentRoute = navBackStackEntry?.destination?.route ?: "discovery"

    Scaffold(
        bottomBar = {
            NavigationBar {
                NavigationBarItem(
                    icon = { Icon(Icons.Default.Send, contentDescription = "Transfer") },
                    label = { Text("Transfer") },
                    selected = currentRoute == "discovery",
                    onClick = {
                        if (currentRoute != "discovery") {
                            navController.navigate("discovery") {
                                popUpTo(navController.graph.startDestinationId)
                                launchSingleTop = true
                            }
                        }
                    },
                )
                NavigationBarItem(
                    icon = { Icon(Icons.Default.List, contentDescription = "History") },
                    label = { Text("History") },
                    selected = currentRoute == "history",
                    onClick = {
                        if (currentRoute != "history") {
                            navController.navigate("history") {
                                popUpTo(navController.graph.startDestinationId)
                                launchSingleTop = true
                            }
                        }
                    },
                )
            }
        },
    ) { paddingValues ->
        NavHost(
            navController = navController,
            startDestination = "discovery",
            modifier = Modifier.padding(paddingValues),
        ) {
            composable("discovery") {
                SendScreen(
                    discoveryViewModel = discoveryViewModel,
                    transferViewModel = transferViewModel,
                ) { navController.navigate("device_selection") }
            }
            composable("device_selection") {
                DeviceSelectionScreen(
                    discoveryViewModel = discoveryViewModel,
                ) { navController.popBackStack() }
            }
            composable("history") {
                HistoryScreen(historyViewModel)
            }
        }
    }
}
