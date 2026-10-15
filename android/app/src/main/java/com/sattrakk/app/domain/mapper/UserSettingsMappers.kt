package com.sattrakk.app.domain.mapper

import com.sattrakk.app.data.remote.dto.UserSettingsDto
import com.sattrakk.app.domain.model.UserSettings

// fcmToken is legitimately nullable per the backend contract (repo-root CLAUDE.md's
// /api/settings/me section) so it's passed through as-is, unlike alertMinutes which the backend
// always returns as a list (empty, never null/missing) — even for a tester who's never written to
// either field, GET /api/settings/me returns a computed default rather than omitting the field.
fun UserSettingsDto.toDomain(): UserSettings = UserSettings(
    alertMinutes = requireNotNull(alertMinutes) { "UserSettingsDto.alertMinutes" },
    fcmToken = fcmToken
)
