import { computed, ref, watch } from "vue";
import type { ComputedRef } from "vue";

export interface PaginationState {
  pageSize: number;
  currentPage: number;
  totalItems: number;
  totalPages: number;
  pageStart: number;
  pageEnd: number;
}

export interface UsePaginationOptions {
  initialPageSize?: number;
  pageSizeOptions?: number[];
}

export const DEFAULT_PAGE_SIZES = [25, 50, 100];

export function usePagination<T>(
  items: ComputedRef<T[]>,
  { initialPageSize = 50, pageSizeOptions = DEFAULT_PAGE_SIZES }: UsePaginationOptions = {},
) {
  const pageSize = ref(initialPageSize);
  const currentPage = ref(1);

  const totalItems = computed(() => items.value.length);
  const totalPages = computed(() =>
    totalItems.value === 0 ? 1 : Math.ceil(totalItems.value / pageSize.value),
  );

  const pageStart = computed(() => {
    if (totalItems.value === 0) {
      return 0;
    }
    return (currentPage.value - 1) * pageSize.value + 1;
  });

  const pageEnd = computed(() => {
    if (totalItems.value === 0) {
      return 0;
    }
    return Math.min(totalItems.value, currentPage.value * pageSize.value);
  });

  const paginatedItems = computed(() => {
    if (totalItems.value === 0) {
      return [] as T[];
    }
    const startIndex = (currentPage.value - 1) * pageSize.value;
    return items.value.slice(startIndex, startIndex + pageSize.value);
  });

  const canGoPrevious = computed(() => currentPage.value > 1);
  const canGoNext = computed(() => currentPage.value < totalPages.value);

  const setPageSize = (nextSize: number) => {
    if (!pageSizeOptions.includes(nextSize)) {
      return;
    }
    if (nextSize === pageSize.value) {
      return;
    }
    pageSize.value = nextSize;
  };

  const goToPreviousPage = () => {
    if (canGoPrevious.value) {
      currentPage.value -= 1;
    }
  };

  const goToNextPage = () => {
    if (canGoNext.value) {
      currentPage.value += 1;
    }
  };

  watch(pageSize, () => {
    currentPage.value = 1;
  });

  watch(items, () => {
    currentPage.value = 1;
  });

  watch([currentPage, totalPages], ([page, pages]) => {
    if (page < 1) {
      currentPage.value = 1;
      return;
    }
    if (page > pages) {
      currentPage.value = pages;
    }
  });

  return {
    pageSizeOptions,
    pageSize,
    currentPage,
    totalItems,
    totalPages,
    pageStart,
    pageEnd,
    paginatedItems,
    canGoPrevious,
    canGoNext,
    setPageSize,
    goToPreviousPage,
    goToNextPage,
  };
}
