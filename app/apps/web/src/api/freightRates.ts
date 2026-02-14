import { apiFetch } from "@/api/http";

export interface FreightRateRoute {
  id: number;
  routeCode: string;
  routeName: string | null;
  routeNameZh: string | null;
  rate: number;
  isActive: boolean;
}

export interface FreightRateConfig {
  year: number;
  availableYears: number[];
  routes: FreightRateRoute[];
}

export interface FreightRateHistory {
  id: number;
  routeCode: string;
  routeName: string | null;
  routeNameZh: string | null;
  rate: number;
  year: number;
  source: string | null;
  notes: string | null;
  createdBy: string | null;
  createdAt: string;
}

export interface UpdateFreightRatesPayload {
  year: number;
  notes?: string;
  routes: Array<{
    routeCode: string;
    routeName?: string | null;
    routeNameZh?: string | null;
    rate: number;
  }>;
}

export async function fetchFreightRates(year?: number): Promise<FreightRateConfig> {
  const params: Record<string, number> = {};
  if (year) params.year = year;
  const response = await apiFetch<{ data: FreightRateConfig }>("/freight-rates", { params });
  return response.data;
}

export async function fetchFreightRateHistory(
  route?: string,
  year?: number,
  limit?: number,
): Promise<FreightRateHistory[]> {
  const params: Record<string, string | number> = {};
  if (route) params.route = route;
  if (year) params.year = year;
  if (limit) params.limit = limit;
  const response = await apiFetch<{ data: FreightRateHistory[] }>("/freight-rates/history", { params });
  return response.data;
}

export async function updateFreightRates(
  payload: UpdateFreightRatesPayload,
): Promise<{ message: string; data: FreightRateConfig }> {
  const response = await apiFetch<{ message: string; data: FreightRateConfig }>("/freight-rates", {
    method: "POST",
    body: payload,
  });
  return response;
}
