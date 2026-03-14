import { beforeEach, describe, expect, it, vi } from "vitest";
import { createPinia, setActivePinia } from "pinia";
import { useSupplierStore } from "@/stores/supplier";

const authState = {
  user: {
    id: "u-1",
    name: "tester",
    role: "admin",
    supplierId: null as number | null,
    permissions: [] as string[],
  },
  token: null as string | null,
  getToken: vi.fn(() => null),
  fetchMe: vi.fn(async () => undefined),
};

vi.mock("@/stores/auth", () => ({
  useAuthStore: () => authState,
}));

const supplierApiMocks = vi.hoisted(() => ({
  listSuppliers: vi.fn(),
  getSupplier: vi.fn(),
  createSupplier: vi.fn(),
  updateSupplier: vi.fn(),
  deleteSupplier: vi.fn(),
  updateSupplierStatus: vi.fn(),
  approveSupplier: vi.fn(),
  setSupplierTags: vi.fn(),
  listTags: vi.fn(),
  getSupplierStats: vi.fn(),
}));

vi.mock("@/api/suppliers", () => supplierApiMocks);

vi.mock("@/api/supplierDocuments", () => ({
  listSupplierDocuments: vi.fn(),
  uploadSupplierDocument: vi.fn(),
  updateSupplierDocument: vi.fn(),
  deleteSupplierDocument: vi.fn(),
}));

vi.mock("@/api/contracts", () => ({
  listContracts: vi.fn(),
  createContract: vi.fn(),
  deleteContract: vi.fn(),
}));

vi.mock("@/api/ratings", () => ({
  createRating: vi.fn(),
}));

describe("supplier store request dedupe", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    authState.user = {
      id: "u-1",
      name: "tester",
      role: "admin",
      supplierId: null,
      permissions: [],
    };
  });

  it("dedupes concurrent fetchSuppliers calls with same params", async () => {
    let resolveList:
      | ((value: { data: Array<{ id: number; companyName: string }>; pagination: any }) => void)
      | null = null;

    supplierApiMocks.listSuppliers.mockImplementation(
      () =>
        new Promise((resolve) => {
          resolveList = resolve;
        }),
    );

    const store = useSupplierStore();
    const query = { q: "acme" };
    const options = { page: 1, pageSize: 50, force: false };

    const p1 = store.fetchSuppliers(query, options);
    const p2 = store.fetchSuppliers(query, options);
    await Promise.resolve();

    expect(supplierApiMocks.listSuppliers).toHaveBeenCalledTimes(1);

    resolveList?.({
      data: [{ id: 101, companyName: "Acme" }],
      pagination: { total: 1, limit: 50, offset: 0, hasMore: false },
    });

    const [r1, r2] = await Promise.all([p1, p2]);
    expect(r1).toHaveLength(1);
    expect(r2).toHaveLength(1);
    expect(supplierApiMocks.listSuppliers).toHaveBeenCalledTimes(1);
  });
});
