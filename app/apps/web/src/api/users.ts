import { apiFetch } from "./http";

export interface User {
  id: string;
  name: string;
  role: string;
  supplierId?: number | null;
  permissions?: string[];
}

/**
 * List all users (admin only)
 */
export async function listUsers(): Promise<User[]> {
  const response = await apiFetch<{ data: User[] }>("/users");
  return response.data;
}

/**
 * Get current user info
 */
export async function getCurrentUser(): Promise<User> {
  return apiFetch<User>("/auth/me");
}
