export type PagedResponse<T> = { items: T[]; pageNumber: number; pageSize: number; totalCount: number; totalPages: number }
export type EntityRow = Record<string, unknown> & { id: string; isActive: boolean }
export type ApiProblem = { title?: string; detail?: string; status?: number }
