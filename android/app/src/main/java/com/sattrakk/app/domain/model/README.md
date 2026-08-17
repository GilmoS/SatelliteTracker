# domain/model

Plain Kotlin domain types the UI layer actually consumes — decoupled from
both the generated OpenAPI DTOs and the Room entities, so backend/schema
churn doesn't ripple into `ui/`. None exist yet.
