package com.sattrakk.app.domain.model

import java.time.OffsetDateTime

data class Note(
    val id: String,
    val passId: String,
    val content: String,
    val createdAt: OffsetDateTime,
    val updatedAt: OffsetDateTime
)
