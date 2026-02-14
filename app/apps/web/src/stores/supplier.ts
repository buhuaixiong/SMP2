import { defineStore } from "pinia";
import { computed, reactive, ref, watch } from "vue";

import { useAuthStore } from "@/stores/auth";
import {
  listSuppliers,
  getSupplier,
  createSupplier as apiCreateSupplier,
  updateSupplier as apiUpdateSupplier,
  deleteSupplier as apiDeleteSupplier,
  updateSupplierStatus,
  approveSupplier as apiApproveSupplier,
  setSupplierTags,
  listTags,
  getSupplierStats,
  type SupplierFilters,
  type SupplierPayload,
  type SupplierStats,
} from "@/api/suppliers";
import {
  listSupplierDocuments,
  uploadSupplierDocument,
  updateSupplierDocument,
  deleteSupplierDocument,
  type UploadDocumentPayload,
  type UpdateDocumentPayload,
} from "@/api/supplierDocuments";
import {
  listContracts,
  createContract as apiCreateContract,
  deleteContract as apiDeleteContract,
  type ContractPayload,
} from "@/api/contracts";
import { createRating as apiCreateRating, type RatingPayload } from "@/api/ratings";

import type {
  Supplier,
  SupplierTag,
  SupplierDocument,
  Contract,
  SupplierRating,
  SupplierChangeRequest,
  SupplierComplianceSummary,
} from "@/types";
import { SupplierStage, SupplierStatus } from "@/types";

const unwrap = <T>(payload: T | { data: T }): T => {
  if (payload && typeof payload === "object" && "data" in payload) {
    return (payload as { data: T }).data;
  }
  return payload as T;
};

const emptyComplianceSummary: SupplierComplianceSummary = {
  requiredProfileFields: [],
  missingProfileFields: [],
  requiredDocumentTypes: [],
  missingDocumentTypes: [],
  isProfileComplete: true,
  isDocumentComplete: true,
  isComplete: true,
  profileScore: 100,
  documentScore: 100,
  overallScore: 100,
  completionCategory: "complete",
  missingItems: [],
};

const normalizeComplianceSummary = (
  summary?: SupplierComplianceSummary | null,
): SupplierComplianceSummary => {
  const base = summary ?? emptyComplianceSummary;
  return {
    ...emptyComplianceSummary,
    ...base,
    requiredProfileFields: [...(base.requiredProfileFields ?? [])],
    missingProfileFields: [...(base.missingProfileFields ?? [])],
    requiredDocumentTypes: [...(base.requiredDocumentTypes ?? [])],
    missingDocumentTypes: [...(base.missingDocumentTypes ?? [])],
    missingItems: [...(base.missingItems ?? [])],
    profileScore: base.profileScore ?? emptyComplianceSummary.profileScore,
    documentScore: base.documentScore ?? emptyComplianceSummary.documentScore,
    overallScore: base.overallScore ?? emptyComplianceSummary.overallScore,
    completionCategory: base.completionCategory ?? emptyComplianceSummary.completionCategory,
  };
};
const normalizeContract = (contract: Contract): Contract => ({
  ...contract,
  autoRenew: !!contract.autoRenew,
  isMandatory: !!contract.isMandatory,
});

const normalizeSupplier = (supplier: Supplier): Supplier => {
  const complianceSummary = normalizeComplianceSummary(supplier.complianceSummary);
  const profileCompletion =
    supplier.profileCompletion ??
    complianceSummary.profileScore ??
    emptyComplianceSummary.profileScore;
  const documentCompletion =
    supplier.documentCompletion ??
    complianceSummary.documentScore ??
    emptyComplianceSummary.documentScore;
  const completionScore =
    supplier.completionScore ??
    complianceSummary.overallScore ??
    emptyComplianceSummary.overallScore;
  const completionStatus =
    supplier.completionStatus ??
    complianceSummary.completionCategory ??
    emptyComplianceSummary.completionCategory;
  const missingRequirements =
    Array.isArray(supplier.missingRequirements) && supplier.missingRequirements.length
      ? [...supplier.missingRequirements]
      : [...complianceSummary.missingItems];

  return {
    ...supplier,
    tags: supplier.tags ?? [],
    documents: supplier.documents ?? [],
    contracts: (supplier.contracts ?? []).map(normalizeContract),
    files: supplier.files ?? [],
    complianceSummary,
    profileCompletion,
    documentCompletion,
    completionScore,
    completionStatus,
    missingRequirements,
    approvalHistory: supplier.approvalHistory ?? [],
    fileApprovals: supplier.fileApprovals ?? [],
    ratingsSummary: supplier.ratingsSummary ?? {
      totalEvaluations: 0,
      overallAverage: null,
      avgOnTimeDelivery: null,
      avgQualityScore: null,
      avgServiceScore: null,
      avgCostScore: null,
    },
    latestRating: supplier.latestRating ?? null,
  };
};

export const useSupplierStore = defineStore("supplier", () => {
  const authStore = useAuthStore();

  const suppliers = ref<Supplier[]>([]);
  const supplierDetails = reactive(new Map<number, Supplier>());
  const availableTags = ref<SupplierTag[]>([]);
  const changeRequests = ref<SupplierChangeRequest[]>([]);
  const stats = ref<SupplierStats | null>(null);

  // Pagination state
  const DEFAULT_DIRECTORY_PAGE_SIZE = 50;

  const pagination = ref({
    total: 0,
    limit: DEFAULT_DIRECTORY_PAGE_SIZE,
    offset: 0,
    hasMore: false,
  });
  const currentPage = ref(1);
  const pageSize = ref(DEFAULT_DIRECTORY_PAGE_SIZE);

  const loading = ref(false);
  const hasLoaded = ref(false);
  const activeFilters = ref<SupplierFilters>({});
  const selectedSupplierId = ref<number | null>(null);
  const requirementCatalog = computed(
    () => stats.value?.requirementCatalog ?? { documents: [], profileFields: [] },
  );
  const documentRequirementOptions = computed(() => requirementCatalog.value.documents);
  const profileRequirementOptions = computed(() => requirementCatalog.value.profileFields);
  const resetStore = () => {
    suppliers.value = [];
    supplierDetails.clear();
    availableTags.value = [];
    changeRequests.value = [];
    loading.value = false;
    hasLoaded.value = false;
    activeFilters.value = {};
    selectedSupplierId.value = null;
    stats.value = null;
    pagination.value = {
      total: 0,
      limit: DEFAULT_DIRECTORY_PAGE_SIZE,
      offset: 0,
      hasMore: false,
    };
    currentPage.value = 1;
    pageSize.value = DEFAULT_DIRECTORY_PAGE_SIZE;
  };

  watch(
    () => {
      const user = authStore.user;
      if (!user) {
        return null;
      }
      return `${user.id}:${user.supplierId ?? ""}:${user.role ?? ""}`;
    },
    (newKey, oldKey) => {
      if (newKey !== oldKey) {
        resetStore();
      }
    },
  );

  const actorInfo = () => ({
    actorId: authStore.user ? String(authStore.user.id) : undefined,
    actorName: authStore.user?.name,
  });

  const ensureAuthUser = async () => {
    if (authStore.user) {
      return authStore.user;
    }
    const token = typeof authStore.getToken === "function" ? authStore.getToken() : authStore.token;
    if (token) {
      try {
        await authStore.fetchMe();
      } catch (error) {
        console.warn("Failed to load authenticated user state", error);
      }
    }
    return authStore.user;
  };

  const isSupplierPortalUser = () => {
    const user = authStore.user;
    if (!user || user.supplierId === null || user.supplierId === undefined) {
      return false;
    }
    const staffRoles = new Set([
      "admin",
      "purchaser",
      "procurement_manager",
      "procurement_director",
      "finance_accountant",
      "finance_director",
    ]);
    if (staffRoles.has(user.role)) {
      return false;
    }
    const permissions = new Set(user.permissions || []);
    if (permissions.has("supplier.segment.manage")) {
      return false;
    }
    return true;
  };

  const selectedSupplier = computed(() => {
    if (selectedSupplierId.value == null) {
      return null;
    }
    return (
      supplierDetails.get(selectedSupplierId.value) ||
      suppliers.value.find((supplier) => supplier.id === selectedSupplierId.value) ||
      null
    );
  });

  const REGISTRATION_STATUSES: SupplierStatus[] = [
    SupplierStatus.POTENTIAL,
    SupplierStatus.UNDER_REVIEW,
    SupplierStatus.PENDING_INFO,
    SupplierStatus.PENDING_PURCHASER,
    SupplierStatus.PENDING_PURCHASE_MANAGER,
    SupplierStatus.PENDING_FINANCE_MANAGER,
  ];

  const REGISTRATION_STAGES = new Set(["", "potential", "prospective", "temporary", "registration"]);

  const pendingSuppliers = computed(() =>
    suppliers.value.filter((supplier) => {
      const stageKey = String(supplier.stage ?? "").trim().toLowerCase();
      const isRegistrationStage =
        !stageKey ||
        REGISTRATION_STAGES.has(stageKey) ||
        supplier.stage === SupplierStage.TEMPORARY;
      const registrationCompletedValue = supplier.registrationCompleted;
      const hasCompletedRegistration =
        registrationCompletedValue === true ||
        registrationCompletedValue === 1 ||
        registrationCompletedValue === "1";

      return (
        !hasCompletedRegistration &&
        isRegistrationStage &&
        REGISTRATION_STATUSES.includes(supplier.status)
      );
    }),
  );

  const approvedSuppliers = computed(() =>
    suppliers.value.filter((supplier) =>
      [SupplierStatus.APPROVED, SupplierStatus.QUALIFIED].includes(supplier.status),
    ),
  );

  const storeSupplier = (input: Supplier) => {
    const normalized = normalizeSupplier(input);
    supplierDetails.set(normalized.id, normalized);
    const index = suppliers.value.findIndex((item) => item.id === normalized.id);
    if (index >= 0) {
      suppliers.value.splice(index, 1, normalized);
    } else {
      suppliers.value.unshift(normalized);
    }
    return normalized;
  };

  const reloadSupplier = async (id: number) => {
    const detail = unwrap(await getSupplier(id));
    return storeSupplier(detail);
  };

  const loadPortalSupplier = async (force = false) => {
    if (hasLoaded.value && !force) {
      return suppliers.value;
    }

    loading.value = true;
    try {
      activeFilters.value = {};
      const rawSupplierId = authStore.user?.supplierId;
      const numericSupplierId =
        typeof rawSupplierId === "number" ? rawSupplierId : Number(rawSupplierId);
      if (!Number.isFinite(numericSupplierId) || numericSupplierId <= 0) {
        suppliers.value = [];
        supplierDetails.clear();
        selectedSupplierId.value = null;
        hasLoaded.value = true;
        return suppliers.value;
      }
      const supplierId = Math.trunc(numericSupplierId);
      const detail = unwrap(await getSupplier(supplierId));
      const normalized = normalizeSupplier(detail);
      suppliers.value = [normalized];
      supplierDetails.clear();
      supplierDetails.set(normalized.id, normalized);
      selectedSupplierId.value = normalized.id;
      hasLoaded.value = true;
      return suppliers.value;
    } finally {
      loading.value = false;
    }
  };

  const cloneDirectoryFilters = (input: SupplierFilters = {}) => {
    const clone: Record<string, any> = {};
    const keys: (keyof SupplierFilters)[] = [
      "status",
      "category",
      "region",
      "stage",
      "importance",
      "tag",
      "q",
      "missingDocument",
    ];

    keys.forEach((key) => {
      const value = input[key];
      if (value === undefined || value === null || value === "") {
        return;
      }

      if (Array.isArray(value)) {
        if (value.length > 0) {
          clone[key as string] = [...value];
        }
        return;
      }

      clone[key as string] = value;
    });

    return clone as SupplierFilters;
  };

  const loadDirectorySuppliers = async (filters: SupplierFilters = {}, force = false) => {
    loading.value = true;
    try {
      const normalizedFilters = cloneDirectoryFilters(filters);
      activeFilters.value = normalizedFilters;

      const limit =
        Number.isFinite(pageSize.value) && pageSize.value > 0
          ? pageSize.value
          : DEFAULT_DIRECTORY_PAGE_SIZE;
      const offset = Math.max(0, (currentPage.value - 1) * limit);

      const query: SupplierFilters = {
        ...normalizedFilters,
        limit,
        offset,
      };

      const response = await listSuppliers(query);
      const list = (response.data ?? []) as Supplier[];
      const paginationMeta = response.pagination ?? {
        total: list.length,
        limit,
        offset,
        hasMore: false,
      };

      suppliers.value = list.map((supplier) => normalizeSupplier(supplier));
      supplierDetails.clear();
      suppliers.value.forEach((supplier) => supplierDetails.set(supplier.id, supplier));

      pagination.value = {
        total: paginationMeta.total ?? list.length,
        limit: paginationMeta.limit ?? limit,
        offset: paginationMeta.offset ?? offset,
        hasMore:
          typeof paginationMeta.hasMore === "boolean"
            ? paginationMeta.hasMore
            : (paginationMeta.offset ?? offset) + (paginationMeta.limit ?? limit) <
              (paginationMeta.total ?? list.length),
      };

      pageSize.value = pagination.value.limit;
      currentPage.value =
        pagination.value.limit > 0
          ? Math.floor(pagination.value.offset / pagination.value.limit) + 1
          : 1;

      if (suppliers.value.length && selectedSupplierId.value == null) {
        selectedSupplierId.value = suppliers.value[0].id;
      }
      hasLoaded.value = true;
      return suppliers.value;
    } finally {
      loading.value = false;
    }
  };

  const fetchSuppliers = async (
    filters: SupplierFilters = {},
    options: { page?: number; pageSize?: number; force?: boolean } | boolean = {},
  ) => {
    await ensureAuthUser();
    const supplierPortalUser = isSupplierPortalUser();
    const normalizedOptions = typeof options === "boolean" ? { force: options } : (options ?? {});

    if (supplierPortalUser) {
      return loadPortalSupplier(normalizedOptions.force ?? false);
    }

    if (
      typeof normalizedOptions.pageSize === "number" &&
      Number.isFinite(normalizedOptions.pageSize) &&
      normalizedOptions.pageSize > 0
    ) {
      pageSize.value = Math.floor(normalizedOptions.pageSize);
    }

    if (
      typeof normalizedOptions.page === "number" &&
      Number.isFinite(normalizedOptions.page) &&
      normalizedOptions.page > 0
    ) {
      currentPage.value = Math.floor(normalizedOptions.page);
    } else if (normalizedOptions.force) {
      currentPage.value = Math.max(1, currentPage.value);
    }

    try {
      return await loadDirectorySuppliers(filters, normalizedOptions.force ?? false);
    } catch (error) {
      const status = (error as { response?: { status?: number } })?.response?.status;
      if (status === 403) {
        await ensureAuthUser();
        if (isSupplierPortalUser()) {
          return loadPortalSupplier(true);
        }
      }
      throw error;
    }
  };

  const setPageSize = async (nextSize: number) => {
    if (!Number.isFinite(nextSize) || nextSize <= 0) {
      return suppliers.value;
    }

    const sanitized = Math.floor(nextSize);
    if (sanitized === pageSize.value) {
      return suppliers.value;
    }

    pageSize.value = sanitized;
    currentPage.value = 1;
    return loadDirectorySuppliers(activeFilters.value, true);
  };

  const goToPage = async (page: number) => {
    if (!Number.isFinite(page) || page <= 0) {
      return suppliers.value;
    }

    const target = Math.floor(page);
    if (target === currentPage.value) {
      return suppliers.value;
    }

    currentPage.value = target;
    return loadDirectorySuppliers(activeFilters.value, true);
  };

  const fetchStats = async (force = false) => {
    if (isSupplierPortalUser()) {
      stats.value = null;
      return null;
    }

    if (!force && stats.value) {
      return stats.value;
    }

    const data = unwrap(await getSupplierStats());
    stats.value = data;
    return data;
  };

  const ensureTags = async (force = false) => {
    if (!force && availableTags.value.length > 0) {
      return availableTags.value;
    }
    const tags = unwrap(await listTags());
    availableTags.value = tags;
    return tags;
  };

  const selectSupplier = async (id: number) => {
    selectedSupplierId.value = id;
    return reloadSupplier(id);
  };

  const clearSelection = () => {
    selectedSupplierId.value = null;
  };

  const createSupplier = async (payload: SupplierPayload) => {
    const created = unwrap(await apiCreateSupplier(payload));
    storeSupplier(created);
    selectedSupplierId.value = created.id;
    return reloadSupplier(created.id);
  };

  const updateSupplier = async (id: number, payload: Partial<SupplierPayload>) => {
    const response = unwrap(await apiUpdateSupplier(id, payload));
    if (
      response &&
      typeof response === "object" &&
      ("isChangeRequest" in response || "flow" in response)
    ) {
      return response;
    }
    storeSupplier(response as Supplier);
    return reloadSupplier(id);
  };

  const removeSupplier = async (id: number) => {
    await apiDeleteSupplier(id);
    suppliers.value = suppliers.value.filter((supplier) => supplier.id !== id);
    supplierDetails.delete(id);
    if (selectedSupplierId.value === id) {
      selectedSupplierId.value = null;
    }
  };

  const changeSupplierStatus = async (
    id: number,
    status: string,
    extras: { currentApprover?: string | null; notes?: string | null } = {},
  ) => {
    const response = await updateSupplierStatus(id, {
      status,
      currentApprover: extras.currentApprover,
      notes: extras.notes,
      ...actorInfo(),
    });
    const updated = unwrap(response);
    storeSupplier(updated);
    return reloadSupplier(id);
  };

  const approveSupplier = async (
    id: number,
    decision: "approved" | "rejected",
    comments?: string,
  ) => {
    const result = await apiApproveSupplier(id, {
      decision,
      approver: authStore.user?.name || "system",
      comments,
      actorId: actorInfo().actorId,
    });
    const updated = unwrap(result);
    storeSupplier(updated);
    return reloadSupplier(id);
  };

  const updateTags = async (id: number, tags: string[]) => {
    await setSupplierTags(id, tags, actorInfo());
    return reloadSupplier(id);
  };

  const refreshDocuments = async (supplierId: number) => {
    const docs = unwrap(await listSupplierDocuments(supplierId));
    const detail = supplierDetails.get(supplierId);
    if (detail) {
      detail.documents = docs;
    }
    const index = suppliers.value.findIndex((supplier) => supplier.id === supplierId);
    if (index >= 0) {
      suppliers.value[index] = {
        ...suppliers.value[index],
        documents: docs,
      };
    }
    return docs;
  };

  const addSupplierDocument = async (payload: UploadDocumentPayload) => {
    const result = await uploadSupplierDocument({ ...payload, ...actorInfo() });
    const docs = unwrap(result);
    const detail = supplierDetails.get(payload.supplierId);
    if (detail) {
      detail.documents = docs;
    }
    await reloadSupplier(payload.supplierId);
    return docs;
  };

  const updateSupplierDocumentMetadata = async (
    supplierId: number,
    documentId: number,
    payload: UpdateDocumentPayload,
  ) => {
    const result = await updateSupplierDocument(supplierId, documentId, {
      ...payload,
      ...actorInfo(),
    });
    const docs = unwrap(result);
    const detail = supplierDetails.get(supplierId);
    if (detail) {
      detail.documents = docs;
    }
    await reloadSupplier(supplierId);
    return docs;
  };

  const removeSupplierDocument = async (supplierId: number, documentId: number) => {
    await deleteSupplierDocument(supplierId, documentId);
    if (supplierDetails.has(supplierId)) {
      const detail = supplierDetails.get(supplierId)!;
      detail.documents = detail.documents.filter((doc) => doc.id !== documentId);
    }
    await reloadSupplier(supplierId);
  };

  const createContract = async (payload: ContractPayload) => {
    const result = await apiCreateContract({ ...payload, ...actorInfo() });
    const contract = unwrap(result);
    await reloadSupplier(contract.supplierId);
    return contract;
  };

  const removeContract = async (contractId: number, supplierId: number) => {
    await apiDeleteContract(contractId);
    await reloadSupplier(supplierId);
  };

  const createRating = async (payload: RatingPayload) => {
    const result = await apiCreateRating({ ...payload, ...actorInfo() });
    const rating = unwrap(result);
    await reloadSupplier(payload.supplierId);
    return rating as SupplierRating;
  };

  const fetchChangeRequests = async () => {
    changeRequests.value = [];
    return changeRequests.value;
  };

  const getSupplierChangeRequest = (supplierId: number) =>
    changeRequests.value.find((request) => request.supplierId === supplierId) || null;

  const submitSupplierChangeRequest = async (
    supplier: Supplier,
    payload: Record<string, unknown>,
  ) => {
    throw new Error("Supplier change request workflow is not implemented yet.");
  };

  const approveSupplierChangeRequestStep = async (supplierId: number) => {
    throw new Error("Supplier change request workflow is not implemented yet.");
  };

  const rejectSupplierChangeRequest = async (supplierId: number) => {
    throw new Error("Supplier change request workflow is not implemented yet.");
  };

  return {
    suppliers,
    availableTags,
    changeRequests,
    stats,
    requirementCatalog,
    documentRequirementOptions,
    profileRequirementOptions,
    loading,
    hasLoaded,
    activeFilters,
    selectedSupplierId,
    pagination,
    pageSize,
    currentPage,
    resetStore,
    selectedSupplier,
    pendingSuppliers,
    approvedSuppliers,
    fetchSuppliers,
    setPageSize,
    goToPage,
    fetchStats,
    ensureTags,
    selectSupplier,
    clearSelection,
    createSupplier,
    updateSupplier,
    removeSupplier,
    changeSupplierStatus,
    approveSupplier,
    updateTags,
    refreshDocuments,
    addSupplierDocument,
    updateSupplierDocumentMetadata,
    removeSupplierDocument,
    createContract,
    removeContract,
    createRating,
    fetchChangeRequests,
    getSupplierChangeRequest,
    submitSupplierChangeRequest,
    approveSupplierChangeRequestStep,
    rejectSupplierChangeRequest,
  };
});
