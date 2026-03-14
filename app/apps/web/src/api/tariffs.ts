import { apiFetch } from "@/api/http";

export interface TariffCountryOption {
  code: string;
  name: string;
  nameZh?: string | null;
}

export interface TariffOptions {
  countries: TariffCountryOption[];
  productGroups: string[];
}

export interface TariffCalculationPayload {
  originalPrice: number;
  shippingCountry: string;
  productGroup: string;
  productOrigin?: string | null;
  projectLocation?: string;
  deliveryTerms?: string;
  currency?: string;
}

export interface TariffCalculationResult {
  originalPrice: number;
  tariffRate: number;
  tariffRatePercent: string;
  specialTariffRate: number;
  specialTariffRatePercent: string | null;
  tariffAmount: number;
  specialTariffAmount: number;
  totalTariffAmount: number;
  standardCost: number;
  standardCostLocal?: number | null;
  standardCostUsd?: number | null;
  standardCostCurrency?: string;
  currency: string;
  shippingCountry: string;
  productGroup: string;
  productOrigin?: string | null;
  hasSpecialTariff: boolean;
  freightRate?: number | null;
}

export async function fetchTariffOptions(): Promise<TariffOptions> {
  const response = await apiFetch<{ data: TariffOptions }>("/tariffs/options");
  return response.data;
}

export async function calculateTariff(
  payload: TariffCalculationPayload,
): Promise<TariffCalculationResult> {
  const response = await apiFetch<{ data: TariffCalculationResult }>(
    "/tariffs/calculate",
    {
      method: "POST",
      body: payload,
    },
  );
  return response.data;
}
