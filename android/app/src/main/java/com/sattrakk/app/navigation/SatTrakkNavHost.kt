package com.sattrakk.app.navigation

import android.net.Uri
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.navigation.NavGraph.Companion.findStartDestination
import androidx.navigation.NavHostController
import androidx.navigation.NavType
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.dialog
import androidx.navigation.compose.rememberNavController
import androidx.navigation.navArgument
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import com.sattrakk.app.ui.dashboard.DashboardScreen
import com.sattrakk.app.ui.fullpasslist.FullPassListScreen
import com.sattrakk.app.ui.map.MapScreen
import com.sattrakk.app.ui.passdetails.PassDetailsScreen
import com.sattrakk.app.ui.settings.SettingsScreen
import com.sattrakk.app.ui.skyview.SkyViewScreen

// The 6 routes. FullPassList/PassDetails carry nav args (both required, per
// FullPassListViewModel/PassDetailsViewModel's SavedStateHandle reads — see android/CLAUDE.md);
// the other four take none. PassDetails is registered as a dialog destination, not composable —
// it must render as a modal overlay on top of whatever's behind it, not replace the full screen
// (see android/CLAUDE.md's Pass Details Modal section).
sealed class SatTrakkDestination(val route: String) {
    data object Dashboard : SatTrakkDestination("dashboard")
    data object Map : SatTrakkDestination("map")
    data object SkyView : SatTrakkDestination("sky_view")
    data object Settings : SatTrakkDestination("settings")

    data object FullPassList : SatTrakkDestination("full_pass_list/{satelliteId}/{satelliteName}") {
        // satelliteName is a free-text satellite name (e.g. "EROS C3") and needs percent-encoding
        // to survive as a path segment; Navigation Compose decodes it back automatically when
        // populating the destination's arguments.
        fun buildRoute(satelliteId: String, satelliteName: String) =
            "full_pass_list/${Uri.encode(satelliteId)}/${Uri.encode(satelliteName)}"
    }

    data object PassDetails : SatTrakkDestination("pass_details/{passId}") {
        fun buildRoute(passId: String) = "pass_details/${Uri.encode(passId)}"
    }
}

// App-root Scaffold: owns the bottom navigation bar (shared chrome across all 5 top-level
// destinations) and the NavHost. Individual screens own their own top app bar/FAB, if any — see
// DashboardScreen.
@Composable
fun MainNavHost(navController: NavHostController = rememberNavController()) {
    // The satellite the Dashboard is currently showing, reported up via DashboardScreen's
    // onSelectedSatelliteChanged callback. Used by the bottom nav bar's "Passes" item, which needs
    // a satelliteId/satelliteName to navigate to (Full Pass List is scoped to one satellite — see
    // android/CLAUDE.md). Deliberately local UI state, not something any ViewModel owns — it's
    // purely "what is the nav bar allowed to navigate to right now."
    var selectedSatellite by remember { mutableStateOf<Pair<String, String>?>(null) }

    Scaffold(
        bottomBar = { SatTrakkBottomNavBar(navController, selectedSatellite) }
    ) { innerPadding ->
        NavHost(
            navController = navController,
            startDestination = SatTrakkDestination.Dashboard.route,
            modifier = Modifier.padding(innerPadding),
        ) {
            composable(SatTrakkDestination.Dashboard.route) {
                DashboardScreen(
                    onSelectedSatelliteChanged = { satelliteId, satelliteName ->
                        selectedSatellite = satelliteId to satelliteName
                    },
                    onViewFullPassList = { satelliteId, satelliteName ->
                        navController.navigate(SatTrakkDestination.FullPassList.buildRoute(satelliteId, satelliteName))
                    },
                    onPassClick = { passId ->
                        navController.navigate(SatTrakkDestination.PassDetails.buildRoute(passId))
                    },
                    onOpenMap = { navController.navigate(SatTrakkDestination.Map.route) },
                )
            }
            composable(SatTrakkDestination.Map.route) { MapScreen() }
            composable(SatTrakkDestination.SkyView.route) { SkyViewScreen() }
            composable(SatTrakkDestination.Settings.route) { SettingsScreen() }
            composable(
                route = SatTrakkDestination.FullPassList.route,
                arguments = listOf(
                    navArgument("satelliteId") { type = NavType.StringType },
                    navArgument("satelliteName") { type = NavType.StringType },
                ),
            ) {
                // No satelliteId/satelliteName passed explicitly -- FullPassListViewModel reads
                // both from this same backstack entry's SavedStateHandle via hiltViewModel().
                FullPassListScreen(
                    onBackClick = { navController.popBackStack() },
                    onPassClick = { passId ->
                        navController.navigate(SatTrakkDestination.PassDetails.buildRoute(passId))
                    },
                )
            }
            dialog(
                route = SatTrakkDestination.PassDetails.route,
                arguments = listOf(navArgument("passId") { type = NavType.StringType }),
            ) {
                PassDetailsScreen()
            }
        }
    }
}

@Composable
private fun SatTrakkBottomNavBar(navController: NavHostController, selectedSatellite: Pair<String, String>?) {
    val currentRoute = navController.currentBackStackEntryAsState().value?.destination?.route
    val onSurfaceVariant = MaterialTheme.colorScheme.onSurfaceVariant
    val onSurface = MaterialTheme.colorScheme.onSurface

    NavigationBar {
        NavigationBarItem(
            selected = currentRoute == SatTrakkDestination.Dashboard.route,
            onClick = { navController.navigateToTopLevel(SatTrakkDestination.Dashboard.route) },
            icon = { HomeIcon(if (currentRoute == SatTrakkDestination.Dashboard.route) onSurface else onSurfaceVariant) },
            label = { Text("Home") },
        )
        NavigationBarItem(
            selected = currentRoute == SatTrakkDestination.FullPassList.route,
            enabled = selectedSatellite != null,
            onClick = {
                val (satelliteId, satelliteName) = selectedSatellite ?: return@NavigationBarItem
                navController.navigateToTopLevel(SatTrakkDestination.FullPassList.buildRoute(satelliteId, satelliteName))
            },
            icon = { PassesIcon(if (currentRoute == SatTrakkDestination.FullPassList.route) onSurface else onSurfaceVariant) },
            label = { Text("Passes") },
        )
        NavigationBarItem(
            selected = currentRoute == SatTrakkDestination.Map.route,
            onClick = { navController.navigateToTopLevel(SatTrakkDestination.Map.route) },
            icon = { MapIcon(if (currentRoute == SatTrakkDestination.Map.route) onSurface else onSurfaceVariant) },
            label = { Text("Map") },
        )
        NavigationBarItem(
            selected = currentRoute == SatTrakkDestination.SkyView.route,
            onClick = { navController.navigateToTopLevel(SatTrakkDestination.SkyView.route) },
            icon = { OrbitIcon(if (currentRoute == SatTrakkDestination.SkyView.route) onSurface else onSurfaceVariant) },
            label = { Text("Sky View") },
        )
        NavigationBarItem(
            selected = currentRoute == SatTrakkDestination.Settings.route,
            onClick = { navController.navigateToTopLevel(SatTrakkDestination.Settings.route) },
            icon = { SettingsIcon(if (currentRoute == SatTrakkDestination.Settings.route) onSurface else onSurfaceVariant) },
            label = { Text("Settings") },
        )
    }
}

// Standard single-top bottom-nav pattern: avoid piling up backstack copies of the same
// destination, and restore each tab's own scroll/state when switching back to it.
private fun NavHostController.navigateToTopLevel(route: String) {
    navigate(route) {
        popUpTo(graph.findStartDestination().id) { saveState = true }
        launchSingleTop = true
        restoreState = true
    }
}
