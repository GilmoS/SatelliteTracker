package com.sattrakk.app.domain.model

data class UserSettings(
    val alertMinutes: List<Int>,
    val fcmToken: String?
)
