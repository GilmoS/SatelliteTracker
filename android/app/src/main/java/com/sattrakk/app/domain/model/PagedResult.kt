package com.sattrakk.app.domain.model

// Mirrors the backend's PagedResultDto envelope shape ({ items, page, pageSize, hasMore } — see
// repo-root CLAUDE.md's paginated pass history section) for any client-side paginated read.
// Currently only produced by PassRepository.getPassHistory, from either the network response or
// the equivalent Room-local filtered/paginated query.
data class PagedResult<T>(
    val items: List<T>,
    val page: Int,
    val pageSize: Int,
    val hasMore: Boolean
)
