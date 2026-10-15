package com.sattrakk.app.domain.mapper

import com.sattrakk.app.data.local.entity.NoteEntity
import com.sattrakk.app.data.remote.dto.NoteDto
import com.sattrakk.app.domain.model.Note
import java.time.Instant
import java.time.OffsetDateTime
import java.time.ZoneOffset

fun NoteDto.toDomain(): Note = Note(
    id = requireNotNull(id) { "NoteDto.id" }.toString(),
    passId = requireNotNull(passId) { "NoteDto.passId" }.toString(),
    content = requireNotNull(content) { "NoteDto.content" },
    createdAt = requireNotNull(createdAt) { "NoteDto.createdAt" },
    updatedAt = requireNotNull(updatedAt) { "NoteDto.updatedAt" }
)

fun Note.toEntity(): NoteEntity = NoteEntity(
    id = id,
    passId = passId,
    content = content,
    createdAtEpochMillis = createdAt.toInstant().toEpochMilli(),
    updatedAtEpochMillis = updatedAt.toInstant().toEpochMilli()
)

fun NoteEntity.toDomain(): Note = Note(
    id = id,
    passId = passId,
    content = content,
    createdAt = OffsetDateTime.ofInstant(Instant.ofEpochMilli(createdAtEpochMillis), ZoneOffset.UTC),
    updatedAt = OffsetDateTime.ofInstant(Instant.ofEpochMilli(updatedAtEpochMillis), ZoneOffset.UTC)
)
