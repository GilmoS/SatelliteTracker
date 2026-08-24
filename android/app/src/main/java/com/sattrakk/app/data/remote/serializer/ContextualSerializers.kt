package com.sattrakk.app.data.remote.serializer

import java.time.OffsetDateTime
import java.util.UUID
import kotlinx.serialization.KSerializer
import kotlinx.serialization.descriptors.PrimitiveKind
import kotlinx.serialization.descriptors.PrimitiveSerialDescriptor
import kotlinx.serialization.descriptors.SerialDescriptor
import kotlinx.serialization.encoding.Decoder
import kotlinx.serialization.encoding.Encoder
import kotlinx.serialization.modules.SerializersModule
import kotlinx.serialization.modules.contextual

// The OpenAPI Generator's kotlinx_serialization option marks java.util.UUID and
// java.time.OffsetDateTime fields as @Contextual instead of emitting serializers for them
// (models-only codegen — see app/build.gradle.kts openApiGenerate — never generates supporting
// infrastructure files). Both types round-trip through the backend as plain ISO-8601 strings
// (System.Text.Json's default Guid/DateTime handling), so both serializers are just string
// (de)serialization.
object UuidSerializer : KSerializer<UUID> {
    override val descriptor: SerialDescriptor = PrimitiveSerialDescriptor("java.util.UUID", PrimitiveKind.STRING)
    override fun serialize(encoder: Encoder, value: UUID) = encoder.encodeString(value.toString())
    override fun deserialize(decoder: Decoder): UUID = UUID.fromString(decoder.decodeString())
}

object OffsetDateTimeSerializer : KSerializer<OffsetDateTime> {
    override val descriptor: SerialDescriptor =
        PrimitiveSerialDescriptor("java.time.OffsetDateTime", PrimitiveKind.STRING)
    override fun serialize(encoder: Encoder, value: OffsetDateTime) = encoder.encodeString(value.toString())
    override fun deserialize(decoder: Decoder): OffsetDateTime = OffsetDateTime.parse(decoder.decodeString())
}

val sattrakkSerializersModule: SerializersModule = SerializersModule {
    contextual(UUID::class, UuidSerializer)
    contextual(OffsetDateTime::class, OffsetDateTimeSerializer)
}
