package com.sattrakk.app.ui.theme

import androidx.compose.ui.unit.dp

// Shared screen-content edge padding for the three bottom-nav-hosted screens (Dashboard, Full
// Pass List, Settings). Applied on top of each screen's own Scaffold `innerPadding` — which
// already clears that screen's TopAppBar, and, via the app-root Scaffold's bottomBar insets
// (see MainNavHost), the bottom navigation bar itself. These two constants are the *additional*
// breathing room on top of that, and are deliberately shared rather than each screen picking its
// own value, since all three sit under the same bottom nav bar and top app bar chrome.
//
// ScreenContentBottomPadding is 0 on purpose: content should end flush against the bottom nav
// bar, with no dead gap between the last row and the bar. ScreenContentTopPadding is a small,
// fixed gap below the TopAppBar — enough to keep the first row from touching the bar, not enough
// to visually stretch the layout or read as a second app-bar-sized inset.
val ScreenContentTopPadding = 8.dp
val ScreenContentBottomPadding = 0.dp
