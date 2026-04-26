package com.filecourier.app.ui.navigation

import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Info
import androidx.compose.material.icons.filled.List
import androidx.compose.material.icons.filled.Send
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.rememberNavController
import com.filecourier.app.ui.AboutScreen
import com.filecourier.app.ui.DeviceSelectionScreen
import com.filecourier.app.ui.FileDetailsScreen
import com.filecourier.app.ui.HistoryScreen
import com.filecourier.app.ui.SendScreen
import com.filecourier.app.ui.SettingsScreen
import com.filecourier.app.viewmodel.DiscoveryViewModel
import com.filecourier.app.viewmodel.HistoryViewModel
import com.filecourier.app.viewmodel.SettingsViewModel
import com.filecourier.app.viewmodel.TransferViewModel
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AppNavigation(
    discoveryViewModel: DiscoveryViewModel,
    transferViewModel: TransferViewModel,
    historyViewModel: HistoryViewModel,
    settingsViewModel: SettingsViewModel,
) {
    val navController = rememberNavController()
    val navBackStackEntry by navController.currentBackStackEntryAsState()
    val currentRoute = navBackStackEntry?.destination?.route ?: "discovery"
    
    val drawerState = rememberDrawerState(initialValue = DrawerValue.Closed)
    val scope = rememberCoroutineScope()

    val showBottomBar = currentRoute in listOf("discovery", "history")

    ModalNavigationDrawer(
        drawerState = drawerState,
        drawerContent = {
            ModalDrawerSheet {
                Spacer(modifier = Modifier.height(12.dp))
                Text(
                    "FileCourier",
                    modifier = Modifier.padding(16.dp),
                    style = MaterialTheme.typography.titleLarge,
                )
                NavigationDrawerItem(
                    icon = { Icon(Icons.Default.Settings, contentDescription = null) },
                    label = { Text("Settings") },
                    selected = false,
                    onClick = {
                        scope.launch { drawerState.close() }
                        navController.navigate("settings")
                    },
                    modifier = Modifier.padding(NavigationDrawerItemDefaults.ItemPadding),
                )
                NavigationDrawerItem(
                    icon = { Icon(Icons.Default.Info, contentDescription = null) },
                    label = { Text("About") },
                    selected = false,
                    onClick = {
                        scope.launch { drawerState.close() }
                        navController.navigate("about")
                    },
                    modifier = Modifier.padding(NavigationDrawerItemDefaults.ItemPadding),
                )
            }
        },
        gesturesEnabled = showBottomBar,
    ) {
        Scaffold(
            bottomBar = {
                if (showBottomBar) {
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
                        onOpenDrawer = { scope.launch { drawerState.open() } }
                    ) { navController.navigate("device_selection") }
                }
                composable("device_selection") {
                    DeviceSelectionScreen(
                        discoveryViewModel = discoveryViewModel,
                    ) { navController.popBackStack() }
                }
                composable("history") {
                    HistoryScreen(
                        historyViewModel = historyViewModel,
                        onOpenDrawer = { scope.launch { drawerState.open() } },
                        onNavigateToFileDetails = { id -> navController.navigate("file_details/$id") }
                    )
                }
                composable("file_details/{transferId}") { backStackEntry ->
                    val transferId = backStackEntry.arguments?.getString("transferId") ?: ""
                    FileDetailsScreen(
                        transferId = transferId,
                        historyViewModel = historyViewModel,
                        onBack = { navController.popBackStack() }
                    )
                }
                composable("settings") {
                    SettingsScreen(
                        viewModel = settingsViewModel,
                    ) { navController.popBackStack() }
                }
                composable("about") {
                    AboutScreen { navController.popBackStack() }
                }
            }
        }
    }
}
