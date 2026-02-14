import { UserRole } from "../types";

// Normalize any role input to lowercase string for matching.
export const normalizeRoleInput = (role: unknown): string => {
  if (role == null) return "";
  if (typeof role === "string") return role.trim().toLowerCase();
  return String(role).trim().toLowerCase();
};

// Backend -> frontend role map (all keys in lowercase).
export const BACKEND_TO_FRONTEND_ROLE_MAP: Record<string, UserRole> = {
  // Admin
  admin: UserRole.ADMIN,
  administrator: UserRole.ADMIN,
  sys_admin: UserRole.ADMIN,
  system_admin: UserRole.ADMIN,

  // Procurement
  purchaser: UserRole.PURCHASER,
  purchase_manager: UserRole.PURCHASE_MANAGER,
  procurement_manager: UserRole.PROCUREMENT_MANAGER,
  procurement_director: UserRole.PROCUREMENT_DIRECTOR,

  // Finance
  finance_manager: UserRole.FINANCE_MANAGER,
  finance_accountant: UserRole.FINANCE_ACCOUNTANT,
  finance_cashier: UserRole.FINANCE_CASHIER,
  finance_director: UserRole.FINANCE_DIRECTOR,
  finance_dir: UserRole.FINANCE_DIRECTOR,

  // SQE
  sqe: UserRole.SQE,

  // Supplier
  supplier: UserRole.FORMAL_SUPPLIER,
  formal_supplier: UserRole.FORMAL_SUPPLIER,
  temp_supplier: UserRole.TEMP_SUPPLIER,
  temporary_supplier: UserRole.TEMP_SUPPLIER,
};

// Frontend -> backend role map.
export const FRONTEND_TO_BACKEND_ROLE_MAP: Record<UserRole, string> = {
  [UserRole.ADMIN]: "admin",

  [UserRole.PURCHASER]: "purchaser",
  [UserRole.PURCHASE_MANAGER]: "purchase_manager",
  [UserRole.PROCUREMENT_MANAGER]: "procurement_manager",
  [UserRole.PROCUREMENT_DIRECTOR]: "procurement_director",

  [UserRole.FINANCE_MANAGER]: "finance_manager",
  [UserRole.FINANCE_ACCOUNTANT]: "finance_accountant",
  [UserRole.FINANCE_CASHIER]: "finance_cashier",
  [UserRole.FINANCE_DIRECTOR]: "finance_director",

  [UserRole.SQE]: "sqe",

  [UserRole.FORMAL_SUPPLIER]: "formal_supplier",
  [UserRole.TEMP_SUPPLIER]: "temp_supplier",
  [UserRole.SUPPLIER]: "supplier",
};

// Map backend role (any casing/alias) to frontend UserRole.
export const mapBackendRoleToFrontend = (backendRole: unknown): UserRole => {
  const key = normalizeRoleInput(backendRole);
  return BACKEND_TO_FRONTEND_ROLE_MAP[key] ?? UserRole.SUPPLIER;
};

// Map frontend UserRole to backend role key.
export const mapFrontendRoleToBackend = (frontendRole: UserRole | undefined): string => {
  if (!frontendRole) return "supplier";
  return FRONTEND_TO_BACKEND_ROLE_MAP[frontendRole] ?? "supplier";
};

// Role display names.
export const getRoleDisplayName = (role: UserRole | undefined): string => {
  switch (role) {
    case UserRole.ADMIN:
      return "System administrator";

    case UserRole.PURCHASER:
      return "Purchaser";
    case UserRole.PURCHASE_MANAGER:
      return "Purchasing manager";
    case UserRole.PROCUREMENT_MANAGER:
      return "Procurement manager";
    case UserRole.PROCUREMENT_DIRECTOR:
      return "Procurement director";

    case UserRole.FINANCE_MANAGER:
      return "Finance manager";
    case UserRole.FINANCE_ACCOUNTANT:
      return "Finance accountant";
    case UserRole.FINANCE_CASHIER:
      return "Finance cashier";
    case UserRole.FINANCE_DIRECTOR:
      return "Finance director";

    case UserRole.SQE:
      return "SQE engineer";

    case UserRole.FORMAL_SUPPLIER:
      return "Supplier";
    case UserRole.TEMP_SUPPLIER:
      return "Temporary supplier";

    default:
      return "User";
  }
};

// Role groups.
export const isSupplierRole = (role: UserRole | undefined): boolean => {
  return role === UserRole.FORMAL_SUPPLIER || role === UserRole.TEMP_SUPPLIER;
};

export const isProcurementRole = (role: UserRole | undefined): boolean => {
  if (!role) return false;
  return [
    UserRole.PURCHASER,
    UserRole.PURCHASE_MANAGER,
    UserRole.PROCUREMENT_MANAGER,
    UserRole.PROCUREMENT_DIRECTOR,
  ].includes(role);
};

export const isFinanceRole = (role: UserRole | undefined): boolean => {
  if (!role) return false;
  return [
    UserRole.FINANCE_MANAGER,
    UserRole.FINANCE_ACCOUNTANT,
    UserRole.FINANCE_CASHIER,
    UserRole.FINANCE_DIRECTOR,
  ].includes(role);
};

export const isAdminRole = (role: UserRole | undefined): boolean => {
  return role === UserRole.ADMIN;
};
