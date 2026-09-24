package com.sattrakk.app.navigation

import android.net.Uri
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
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
import androidx.compose.ui.window.DialogProperties
import com.sattrakk.app.ui.dashboard.DashboardScreen
import com.sattrakk.app.ui.fullpasslist.FullPassListScreen
import com.sattrakk.app.ui.map.MapScreen
import com.sattrakk.app.ui.map.MapViewModel
import com.sattrakk.app.ui.passdetails.PassDetailsScreen
import com.sattrakk.app.ui.settings.SettingsScreen
import com.sattrakk.app.ui.skyview.SkyViewScreen

// The 6 routes. FullPassList/PassDetails carry required nav args (per
// FullPassListViewModel/PassDetailsViewModel's SavedStateHandle reads — see android/CLAUDE.md);
// Map carries two optional ones (see Map below); the other three take none. PassDetails is registered as a dialog destination, not composable —
// it must render as a modal overlay on top of whatever's behind it, not replace the full screen
// (see android/CLAUDE.md's Pass Details Modal section).
sealed class SatTrakkDestination(val route: String) {
    data object Dashboard : SatTrakkDestination("dashboard")
    // Both args optional (query params, nullable), read by MapViewModel via SavedStateHandle:
    //  - passId present      -> Flow 2, that pass's fixed ground track ("Show on map", drawer taps);
    //  - passId absent       -> Flow 1, the live track of satelliteId (Dashboard FAB, bottom nav).
    // satelliteId isn't in the task's original "map?passId={passId}" sketch, but MapViewModel's
    // live flow needs it (with none it shows "No satellite selected") — so it rides along the same
    // way, rather than MapViewModel guessing a default satellite.
    data object Map : SatTrakkDestination(
        "map?${MapViewModel.PASS_ID_ARG}={${MapViewModel.PASS_ID_ARG}}" +
            "&${MapViewModel.SATELLITE_ID_ARG}={${MapViewModel.SATELLITE_ID_ARG}}"
    ) {
        fun buildRoute(passId: String? = null, satelliteId: String? = null): String {
            val params = listOfNotNull(
                passId?.let { "${MapViewModel.PASS_ID_ARG}=${Uri.encode(it)}" },
                satelliteId?.let { "${MapViewModel.SATELLITE_ID_ARG}=${Uri.encode(it)}" },
            )
            return if (params.isEmpty()) "map" else "map?" + params.joinToString("&")
        }
    }
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
//
// pendingPassDetailsId: a pass-reminder notification tap waiting to open Pass Details (see
// AppViewModel). Consumed once this NavHost exists — which, since SatTrakkApp only composes
// MainNavHost under SessionState.Valid, is also what defers a link that arrived during
// RequiresReauth until the tester has re-registered.
@Composable
fun MainNavHost(
    navController: NavHostController = rememberNavController(),
    pendingPassDetailsId: String? = null,
    onPendingPassDetailsConsumed: () -> Unit = {},
) {
    // The satellite the Dashboard is currently showing, reported up via DashboardScreen's
    // onSelectedSatelliteChanged callback. Used by the bottom nav bar's "Passes" item, which needs
    // a satelliteId/satelliteName to navigate to (Full Pass List is scoped to one satellite — see
    // android/CLAUDE.md). Deliberately local UI state, not something any ViewModel owns — it's
    // purely "what is the nav bar allowed to navigate to right now."
    var selectedSatellite by remember { mutableStateOf<Pair<String, String>?>(null) }

    Scaffold(
        bottomBar = { SatTrakkBottomNavBar(navController, selectedSatellite) },
        // No topBar here — each screen owns its own TopAppBar, which already pads for the status
        // bar internally. Leaving this at the Scaffold default (WindowInsets.safeDrawing) would
        // make THIS Scaffold also reserve the status-bar inset at the top (nothing here consumes
        // it), stacking a second status-bar-height gap above every screen's own TopAppBar and
        // making it look oversized. contentWindowInsets = WindowInsets(0) removes that reservation
        // entirely; the bottom nav bar's own real measured height (not a system-inset guess) still
        // correctly reserves innerPadding.bottom for the NavHost content below it.
        contentWindowInsets = WindowInsets(0),
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
                        navController.navigateDebounced(SatTrakkDestination.PassDetails.buildRoute(passId))
                    },
                    // Flow 1: no passId, the live track of whichever satellite the Dashboard is
                    // showing. The FAB is only reachable once Dashboard has loaded, so
                    // selectedSatellite is set by then; if not, MapViewModel's own "No satellite
                    // selected" Error covers it rather than a guessed default.
                    onOpenMap = { navController.navigateToMap(satelliteId = selectedSatellite?.first) },
                )
            }
            composable(
                route = SatTrakkDestination.Map.route,
                arguments = listOf(
                    navArgument(MapViewModel.PASS_ID_ARG) {
                        type = NavType.StringType
                        nullable = true
                        defaultValue = null
                    },
                    navArgument(MapViewModel.SATELLITE_ID_ARG) {
                        type = NavType.StringType
                        nullable = true
                        defaultValue = null
                    },
                ),
            ) {
                // MapViewModel reads both args from this entry's SavedStateHandle via
                // hiltViewModel(), same pattern as FullPassListScreen/PassDetailsScreen.
                MapScreen(
                    onBackClick = { navController.popBackStack() },
                    // Drawer tap: switch to Flow 2 for the tapped pass, replacing this Map entry
                    // rather than stacking another one on top of it (see navigateToMap).
                    onPassSelected = { passId -> navController.navigateToMap(passId = passId) },
                )
            }
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
                        navController.navigateDebounced(SatTrakkDestination.PassDetails.buildRoute(passId))
                    },
                )
            }
            dialog(
                route = SatTrakkDestination.PassDetails.route,
                arguments = listOf(navArgument("passId") { type = NavType.StringType }),
                // usePlatformDefaultWidth = false: the stock AlertDialog-style width cap is too
                // narrow for this screen's AOS/LOS + metric-grid + notes content, so
                // PassDetailsScreen sizes and centers its own card instead (see that file).
                dialogProperties = DialogProperties(usePlatformDefaultWidth = false),
            ) {
                // No satelliteId/satelliteName passed explicitly -- PassDetailsViewModel reads
                // passId from this same backstack entry's SavedStateHandle via hiltViewModel(),
                // same pattern as FullPassListScreen above.
                PassDetailsScreen(
                    onBackClick = { navController.popBackStack() },
                    // "Show on map" dismisses this modal and opens Map in Flow 2 for this pass.
                    onNavigateToMap = { passId ->
                        navController.popBackStack()
                        navController.navigateToMap(passId = passId)
                    },
                )
            }
        }

        // After NavHost in composition order, so its graph is already set when this runs. Goes
        // through navigateDebounced (launchSingleTop) like the row taps: tapping a reminder for the
        // pass whose modal is already on top updates it in place rather than stacking a copy.
        LaunchedEffect(pendingPassDetailsId) {
            val passId = pendingPassDetailsId ?: return@LaunchedEffect
            navController.navigateDebounced(SatTrakkDestination.PassDetails.buildRoute(passId))
            onPendingPassDetailsConsumed()
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
        // Always Flow 1 (no passId): the live track of the Dashboard's selected satellite. Disabled
        // until one is known, same fallback as the Passes item above. restoreState = false: a
        // previously-visited Map entry (possibly a Flow 2 pass track) is never brought back — the
        // bottom-nav Map is always "live, now."
        NavigationBarItem(
            selected = currentRoute == SatTrakkDestination.Map.route,
            enabled = selectedSatellite != null,
            onClick = {
                val (satelliteId, _) = selectedSatellite ?: return@NavigationBarItem
                if (navController.isShowingMap(passId = null, satelliteId = satelliteId)) return@NavigationBarItem
                navController.navigateToTopLevel(
                    SatTrakkDestination.Map.buildRoute(satelliteId = satelliteId),
                    restoreState = false,
                )
            },
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

// Round-1's RESUMED-lifecycle guard (see the old comment preserved in git history) did NOT fix
// rapid repeated taps -- diagnosed in round 2 by reading androidx.navigation's own source
// (NavControllerImpl.updateBackStackLifecycle / DialogNavigator, navigation-runtime 2.9.7):
//
// The guard's premise was "a second tap arriving before the first navigation finishes finds the
// current entry not yet RESUMED." That premise only holds for `composable()` destinations, where
// Compose Navigation gates the incoming entry's promotion to RESUMED on its enter transition
// (AnimatedContent) actually completing -- so there's a real window, however short, during which
// a repeat tap's currentBackStackEntry check fails and is dropped. PassDetails is registered as a
// `dialog()` destination (see SatTrakkDestination.PassDetails), and DialogNavigator has no such
// transition to gate on: `DialogNavigator.navigate()` pushes the entry directly, and
// updateBackStackLifecycle() promotes a plain (non-SupportingPane) top-of-stack entry to RESUMED
// immediately, synchronously, within the same navigate() call -- there is no "still transitioning
// in" window at all for a dialog destination. So by the time a second tap's click handler runs
// (a separate frame/event, not the same call stack as the first), the guard's check always finds
// the current entry (now the just-pushed dialog) already RESUMED and lets the second navigate()
// through too, pushing a duplicate PassDetails instance -- exactly the bug that was reported as
// still happening after round 1.
//
// Fixed by relying on NavController's own built-in dedup instead of a lifecycle-timing heuristic:
// `launchSingleTop = true` compares the target route's destination against currentBackStackEntry
// synchronously inside navigate() itself (NavControllerImpl.launchSingleTopInternal) -- if
// PassDetails is already the current top entry, the existing entry's args are updated in place
// instead of a new one being pushed, regardless of any animation/lifecycle timing. This works
// identically for dialog and composable destinations. Used by every navigation to PassDetails
// (Dashboard's and Full Pass List's row taps, plus the notification-tap deep link in MainNavHost)
// rather than duplicating the option at each one.
private fun NavHostController.navigateDebounced(route: String) {
    navigate(route) { launchSingleTop = true }
}

// Standard single-top bottom-nav pattern: avoid piling up backstack copies of the same
// destination, and restore each tab's own scroll/state when switching back to it.
//
// Exception — never save a Map entry's state when leaving it: saveState keeps a popped entry's
// ViewModelStore alive for a later restore, and MapViewModel's live-flow polling (position every
// 15 s, track every 5 min) runs on viewModelScope, which is only cancelled when that store is
// cleared. Saving it would keep polling the backend in the background after the user left the Map.
// Map is always re-entered fresh anyway (live data, and restoreState = false on its own nav item).
private fun NavHostController.navigateToTopLevel(route: String, restoreState: Boolean = true) {
    val leavingMap = currentDestination?.route == SatTrakkDestination.Map.route
    navigate(route) {
        popUpTo(graph.findStartDestination().id) { saveState = !leavingMap }
        launchSingleTop = true
        this.restoreState = restoreState
    }
}

// Every non-bottom-nav navigation to Map (Dashboard FAB, Pass Details' "Show on map", the Map
// drawer) goes through here.
//
// popUpTo(Map) { inclusive = true }: at most one Map entry ever exists in the back stack. Any
// existing one is popped before the new one is pushed, so drawer-tapping through several passes
// never accumulates Map entries, and back always leaves the Map in one step. NavController applies
// popUpTo BEFORE its single-top check (NavControllerImpl.navigate, navigation-runtime 2.9.7) — so
// this also guarantees a fresh entry (and a fresh MapViewModel reading the new args) rather than
// launchSingleTop reusing the current Map entry, whose ViewModel reads its args only once in init
// and would silently keep showing the old pass.
//
// launchSingleTop = true is kept for parity with navigateDebounced, but on its own it can't dedupe
// here (the pop above always removes the Map entry it would compare against). The rapid-tap guard
// is therefore isShowingMap: the same synchronous "is the target already the current top entry?"
// comparison launchSingleTop makes, but including args — a second tap on the same drawer entry, or
// a repeat FAB tap, is dropped instead of tearing down and recreating the screen it's already on.
private fun NavHostController.navigateToMap(passId: String? = null, satelliteId: String? = null) {
    if (isShowingMap(passId, satelliteId)) return
    navigate(SatTrakkDestination.Map.buildRoute(passId = passId, satelliteId = satelliteId)) {
        popUpTo(SatTrakkDestination.Map.route) { inclusive = true }
        launchSingleTop = true
    }
}

private fun NavHostController.isShowingMap(passId: String?, satelliteId: String?): Boolean {
    val entry = currentBackStackEntry ?: return false
    if (entry.destination.route != SatTrakkDestination.Map.route) return false
    val args = entry.arguments
    return args?.getString(MapViewModel.PASS_ID_ARG) == passId &&
        args?.getString(MapViewModel.SATELLITE_ID_ARG) == satelliteId
}
