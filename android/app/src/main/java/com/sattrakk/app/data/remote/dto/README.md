# data/remote/dto

Deliberately empty at commit time. The OpenAPI Generator plugin (see
`app/build.gradle.kts` → `openApiGenerate`) writes generated DTOs into this
package name at build time, but the files themselves land under
`app/build/generated/openapi/...` — never here — so nothing generated is ever
committed. Run `./gradlew :app:openApiGenerate` (or a normal build) to produce
them locally; `data/repository/` maps them to `domain/model/` types via
`domain/mapper/`.
