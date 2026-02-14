/**
 * 分页响应数据接口
 */
export interface PaginatedResponse<T> {
  data: T[];
  pagination: {
    total: number;
    limit: number;
    offset: number;
    hasMore: boolean;
  };
}

/**
 * 可能的响应格式
 */
interface ApiResponse {
  data?: unknown;
  pagination?: unknown;
  total?: number;
  limit?: number;
  offset?: number;
  hasMore?: boolean;
  [key: string]: unknown;
}

/**
 * 构建查询参数字符串
 * 处理数组、布尔值等特殊类型
 */
export function buildQueryParams(filters: Record<string, unknown>): string {
  const params = new URLSearchParams();
  const normalizedFilters: Record<string, unknown> = { ...filters };

  // 标准化搜索参数
  if (normalizedFilters.q && !normalizedFilters.query) {
    normalizedFilters.query = normalizedFilters.q as string;
  }

  Object.entries(normalizedFilters).forEach(([key, value]) => {
    if (Array.isArray(value)) {
      if (!value.length) return;
      value.forEach((entry) => {
        if (entry !== undefined && entry !== null && entry !== "") {
          params.append(key, String(entry));
        }
      });
      return;
    }

    if (value === undefined || value === null || value === "") return;

    if (typeof value === "boolean") {
      params.append(key, value ? "true" : "false");
      return;
    }

    params.append(key, String(value));
  });

  return params.toString();
}

/**
 * 从各种响应格式中提取数据数组
 */
function extractListData(base: unknown): unknown[] {
  const baseObj = base as Record<string, unknown>;
  return (
    (Array.isArray(baseObj?.data) && baseObj.data) ||
    (Array.isArray(baseObj?.list) && baseObj.list) ||
    (Array.isArray(baseObj?.items) && baseObj.items) ||
    (Array.isArray(baseObj?.suppliers) && baseObj.suppliers) ||
    (Array.isArray(baseObj?.Suppliers) && baseObj.Suppliers) ||
    (Array.isArray(base) && base) ||
    []
  );
}

/**
 * 解析分页信息
 */
function parsePaginationInfo(
  response: ApiResponse,
  base: unknown,
  list: unknown[],
  defaultLimit?: number,
  defaultOffset?: number,
): { total: number; limit: number; offset: number; hasMore: boolean } {
  const hasPagination = "pagination" in response && response.pagination;
  const paginationSource = hasPagination
    ? (response.pagination as Record<string, unknown>)
    : ((base as Record<string, unknown>)?.pagination as Record<string, unknown>) ||
      ((base as Record<string, unknown>)?.Pagination as Record<string, unknown>);

  const baseObj = base as Record<string, unknown>;
  const totalRaw =
    baseObj?.total ??
    baseObj?.Total ??
    paginationSource?.total ??
    paginationSource?.Total;
  const total = Number.isFinite(totalRaw) ? Number(totalRaw) : list.length;

  const limitRaw =
    paginationSource?.limit ??
    paginationSource?.Limit ??
    defaultLimit ??
    list.length;
  const limit = Number.isFinite(Number(limitRaw)) && Number(limitRaw) > 0 ? Number(limitRaw) : list.length;

  const offsetRaw =
    paginationSource?.offset ??
    paginationSource?.Offset ??
    defaultOffset ??
    0;
  const offset = Number.isFinite(Number(offsetRaw)) && Number(offsetRaw) >= 0 ? Number(offsetRaw) : 0;

  const hasMoreRaw = paginationSource?.hasMore ?? paginationSource?.HasMore;
  const hasMore =
    typeof hasMoreRaw === "boolean"
      ? hasMoreRaw
      : limit > 0
        ? offset + limit < total
        : false;

  return { total, limit, offset, hasMore };
}

/**
 * 标准化分页响应
 * 处理各种后端返回格式
 */
export function normalizePaginatedResponse<T>(
  response: ApiResponse,
  options?: { defaultLimit?: number; defaultOffset?: number },
): PaginatedResponse<T> {
  const { defaultLimit, defaultOffset } = options || {};

  const hasPagination = "pagination" in response && response.pagination;
  const base = hasPagination ? response : (response.data as ApiResponse) || response;
  const list = extractListData(base) as T[];
  const pagination = parsePaginationInfo(response, base, list, defaultLimit, defaultOffset);

  return {
    data: list,
    pagination,
  };
}

/**
 * 快速创建分页响应的工厂函数
 */
export function createPaginatedResponse<T>(
  data: T[],
  total: number,
  page = 1,
  pageSize = 20,
): PaginatedResponse<T> {
  const offset = (page - 1) * pageSize;
  return {
    data,
    pagination: {
      total,
      limit: pageSize,
      offset,
      hasMore: offset + data.length < total,
    },
  };
}
