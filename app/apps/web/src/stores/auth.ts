// src/stores/auth.ts
import { defineStore } from "pinia";
import { apiFetch } from "@/api/http";
import type { AdminUnit, OrgUnitMembership, PurchasingGroup } from "@/types/index";

const ROLE_LABELS: Record<string, string> = {
  admin: "Administrator",
  purchaser: "Purchaser",
  buyer: "Buyer",
  procurement_manager: "Procurement Manager",
  procurement_director: "Procurement Director",
  quality_manager: "SQE",
  finance_accountant: "Finance Accountant",
  finance_cashier: "Finance Cashier",
  finance_director: "Finance Director",
  temp_supplier: "Temporary Supplier",
  formal_supplier: "Supplier",
  supplier: "Supplier",
};

const normalizeRole = (value: unknown): string => {
  const role = String(value ?? "").trim();
  if (!role) return "";
  const lower = role.toLowerCase();
  if (lower === "quality maneger") return "quality_manager";
  const normalized = lower.replace(/\s+/g, "_");
  return Object.prototype.hasOwnProperty.call(ROLE_LABELS, normalized) ? normalized : role;
};

const TOKEN_KEY = "token";
const USER_KEY = "user";

const toNumber = (v: unknown): number | null => (v == null || Number.isNaN(v) ? null : Number(v));

const getField = (payload: Record<string, unknown>, key: string) => {
  if (key in payload) return payload[key];
  const pascal = key.charAt(0).toUpperCase() + key.slice(1);
  return pascal in payload ? payload[pascal] : undefined;
};

type RawPurchasingGroup = Partial<Pick<PurchasingGroup, "id" | "code" | "name">> & {
  memberRole?: string | null;
};

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === "object" && value !== null;

const normalizePurchasingGroup = (group: unknown) => {
  const record = isRecord(group) ? (group as RawPurchasingGroup) : {};
  return {
    id: Number(record.id) || 0,
    code: String(record.code ?? ""),
    name: String(record.name ?? ""),
    memberRole: String(record.memberRole ?? ""),
  };
};

interface AuthUser {
  id: string;
  name: string;
  role: string;
  supplierId: number | null;
  tempAccountId: number | null;
  relatedApplicationId: number | null;
  accountType: string | null;
  mustChangePassword: boolean;
  permissions: string[];
  orgUnits: OrgUnitMembership[];
  adminUnits: AdminUnit[];
  purchasingGroups: Array<Pick<PurchasingGroup, "id" | "code" | "name"> & { memberRole: string }>;
  isPurchasingGroupLeader: boolean;
  isOrgUnitAdmin: boolean;
  functions: string[];
}

const normalizeOrgUnitMembership = (value: unknown): OrgUnitMembership | null => {
  if (!isRecord(value)) return null;
  return {
    unitId: Number(value.unitId) || 0,
    unitName: String(value.unitName ?? ""),
    unitCode: String(value.unitCode ?? ""),
    memberRole:
      value.memberRole === "lead" || value.memberRole === "admin" ? value.memberRole : "member",
    function: value.function == null ? null : String(value.function),
  };
};

const normalizeAdminUnit = (value: unknown): AdminUnit | null => {
  if (!isRecord(value)) return null;
  return {
    id: Number(value.id) || 0,
    name: String(value.name ?? ""),
    code: String(value.code ?? ""),
    function: value.function == null ? null : String(value.function),
  };
};

const normalizeUser = (raw: Record<string, unknown>): AuthUser => ({
  id: String(raw.id ?? ""),
  name: String(raw.name ?? ""),
  role: normalizeRole(raw.role),
  supplierId: toNumber(getField(raw, "supplierId")),
  tempAccountId: toNumber(getField(raw, "tempAccountId")),
  relatedApplicationId: toNumber(getField(raw, "relatedApplicationId")),
  accountType: raw.accountType != null ? String(raw.accountType) : null,
  mustChangePassword: Boolean(getField(raw, "mustChangePassword")),
  permissions: Array.isArray(raw.permissions) ? raw.permissions.map(String) : [],
  orgUnits: Array.isArray(raw.orgUnits)
    ? raw.orgUnits
        .map(normalizeOrgUnitMembership)
        .filter((item): item is OrgUnitMembership => item !== null)
    : [],
  adminUnits: Array.isArray(raw.adminUnits)
    ? raw.adminUnits.map(normalizeAdminUnit).filter((item): item is AdminUnit => item !== null)
    : [],
  purchasingGroups: Array.isArray(raw.purchasingGroups)
    ? raw.purchasingGroups.map(normalizePurchasingGroup)
    : [],
  isPurchasingGroupLeader:
    Boolean(raw.isPurchasingGroupLeader) ||
    (Array.isArray(raw.purchasingGroups) &&
      raw.purchasingGroups.some((g) => normalizePurchasingGroup(g).memberRole === "lead")),
  isOrgUnitAdmin: Boolean(raw.isOrgUnitAdmin),
  functions: Array.isArray(raw.functions) ? raw.functions.map(String) : [],
});

type ApiEnvelope<T> = { success?: boolean; data?: T; message?: string | null };

const unwrapApiResponse = <T>(payload: T | ApiEnvelope<T>): T =>
  payload && typeof payload === "object" && "data" in payload && payload.data !== undefined
    ? payload.data
    : (payload as T);

const extractAuthMeUser = (payload: unknown): Record<string, unknown> | null => {
  if (!payload || typeof payload !== "object") {
    return null;
  }

  const unwrapped = unwrapApiResponse(payload as Record<string, unknown>);
  if (!unwrapped || typeof unwrapped !== "object") {
    return null;
  }

  if ("user" in unwrapped && unwrapped.user && typeof unwrapped.user === "object") {
    return unwrapped.user as Record<string, unknown>;
  }

  return unwrapped as Record<string, unknown>;
};

const readUser = () => {
  try {
    const raw = localStorage.getItem(USER_KEY);
    if (!raw) return null;
    const parsed = normalizeUser(JSON.parse(raw));
    return parsed.id ? parsed : null;
  } catch {
    return null;
  }
};

interface AuthState {
  token: string | null;
  user: AuthUser | null;
  loading: boolean;
}

export const useAuthStore = defineStore("auth", {
  state: (): AuthState => ({
    token: localStorage.getItem(TOKEN_KEY),
    user: readUser(),
    loading: false,
  }),

  getters: {
    isAuthenticated: (s) => !!s.token,
    role: (s) => s.user?.role ?? null,
    roleDisplayName: (s) => ROLE_LABELS[s.user?.role ?? ""] || s.user?.role || "Guest",
    hasPermission: (s) => (p: string) => s.user?.permissions?.includes(p) ?? false,
    hasAnyPermission:
      (s) =>
      (...ps: string[]) =>
        s.user?.permissions?.some((p) => ps.includes(p)) ?? false,
    hasAllPermissions:
      (s) =>
      (...ps: string[]) =>
        s.user?.permissions?.every((p) => ps.includes(p)) ?? false,
    isPurchasingGroupLeader: (s) => s.user?.isPurchasingGroupLeader ?? false,
    hasFunction: (s) => (f: string) => s.user?.functions?.includes(f) ?? false,
    isProcurement: (s) => s.user?.functions?.includes("procurement") ?? false,
    isFinance: (s) => s.user?.functions?.includes("finance") ?? false,
    isQuality: (s) => s.user?.functions?.includes("quality") ?? false,
    isInOrgUnit: (s) => (id: number) => s.user?.orgUnits?.some((u) => u.unitId === id) ?? false,
    isOrgUnitLeadOrAdmin: (s) => (id: number) =>
      s.user?.orgUnits?.some(
        (u) => u.unitId === id && (u.memberRole === "lead" || u.memberRole === "admin"),
      ) ?? false,
  },

  actions: {
    getToken() {
      return this.token ?? localStorage.getItem(TOKEN_KEY);
    },

    async login(payload: { username: string; password: string }) {
      this.loading = true;
      try {
        const data = unwrapApiResponse(
          await apiFetch<{
            token: string;
            user: Record<string, unknown>;
            mustChangePassword?: boolean;
          }>("/auth/login", {
            method: "POST",
            body: payload,
          }),
        );
        const { token, user, mustChangePassword } = data;
        this.token = token || null;
        this.user = normalizeUser(user);
        if (this.token) localStorage.setItem(TOKEN_KEY, this.token);
        else localStorage.removeItem(TOKEN_KEY);
        localStorage.setItem(USER_KEY, JSON.stringify(this.user));
        return { token, user: this.user, mustChangePassword };
      } finally {
        this.loading = false;
      }
    },

    async fetchMe() {
      const response = await apiFetch<unknown>("/auth/me");
      const user = extractAuthMeUser(response);
      if (user) {
        this.user = normalizeUser({ ...this.user, ...user });
        localStorage.setItem(USER_KEY, JSON.stringify(this.user));
      }
      return response;
    },

    async logout() {
      try {
        await apiFetch("/auth/logout", { method: "POST" });
      } catch {
        // ignore logout errors
      } finally {
        this.token = null;
        this.user = null;
        localStorage.removeItem(TOKEN_KEY);
        localStorage.removeItem(USER_KEY);
      }
    },
  },
});
