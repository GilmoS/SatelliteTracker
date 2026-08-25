package com.sattrakk.app.domain.mapper

import com.sattrakk.app.data.remote.dto.NotifyStatusDto
import com.sattrakk.app.domain.model.NotifyStatus

fun NotifyStatusDto.toDomain(): NotifyStatus = NotifyStatus(
    passId = requireNotNull(passId) { "NotifyStatusDto.passId" }.toString(),
    notify = requireNotNull(notify) { "NotifyStatusDto.notify" }
)
