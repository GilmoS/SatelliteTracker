package com.sattrakk.app.domain.model

import java.time.OffsetDateTime

data class Satellite(
    val id: String,
    val name: String,
    val noradId: Int,
    val description: String?,
    val isActive: Boolean,
    val isDefault: Boolean,
    val createdAt: OffsetDateTime
)
