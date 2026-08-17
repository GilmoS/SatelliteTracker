package com.sattrakk.app.navigation

import androidx.compose.runtime.Composable
import androidx.navigation.NavHostController
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import com.sattrakk.app.ui.dashboard.DashboardScreen
import com.sattrakk.app.ui.map.MapScreen
import com.sattrakk.app.ui.passdetails.PassDetailsScreen
import com.sattrakk.app.ui.settings.SettingsScreen
import com.sattrakk.app.ui.skyview.SkyViewScreen

/** The 5 top-level screens. No arguments yet — PassDetails will need a passId. */
sealed class SatTrakkDestination(val route: String) {
    data object Dashboard : SatTrakkDestination("dashboard")
    data object Map : SatTrakkDestination("map")
    data object SkyView : SatTrakkDestination("sky_view")
    data object PassDetails : SatTrakkDestination("pass_details")
    data object Settings : SatTrakkDestination("settings")
}

@Composable
fun SatTrakkNavHost(navController: NavHostController = rememberNavController()) {
    NavHost(navController = navController, startDestination = SatTrakkDestination.Dashboard.route) {
        composable(SatTrakkDestination.Dashboard.route) { DashboardScreen() }
        composable(SatTrakkDestination.Map.route) { MapScreen() }
        composable(SatTrakkDestination.SkyView.route) { SkyViewScreen() }
        composable(SatTrakkDestination.PassDetails.route) { PassDetailsScreen() }
        composable(SatTrakkDestination.Settings.route) { SettingsScreen() }
    }
}
