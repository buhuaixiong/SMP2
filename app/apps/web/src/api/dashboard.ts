import { apiFetch } from "./http";

export interface TodoItem {
  type: string;
  count: number;
  title: string;
  route: string;
  priority: "high" | "warning" | "info" | "success";
  icon: string;
}

export interface DashboardStats {
  supplierStatus?: string;
  documentsUploaded?: number;
  totalSuppliers?: number;
  activeRfqs?: number;
  totalInvoices?: number;
  [key: string]: unknown;
}

/**
 * Get dashboard todos for the current user
 */
export async function getDashboardTodos(): Promise<TodoItem[]> {
  const res = await apiFetch<{ data: TodoItem[] }>("/dashboard/todos");
  return res.data;
}

/**
 * Get dashboard statistics for the current user
 */
export async function getDashboardStats(): Promise<DashboardStats> {
  const res = await apiFetch<{ data: DashboardStats }>("/dashboard/stats");
  return res.data;
}
