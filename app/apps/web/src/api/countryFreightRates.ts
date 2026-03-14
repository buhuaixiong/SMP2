import { apiFetch } from "@/api/http";

export interface CountryFreightRate {
  countryCode: string;
  countryName: string | null;
  countryNameZh: string | null;
  productGroup: string;
  rate: number;
  isActive: boolean;
}

export interface CountryFreightRateConfig {
  year: number;
  availableYears: number[];
  productGroups: string[];
  rates: CountryFreightRate[];
}

export interface UpdateCountryFreightRatesPayload {
  year: number;
  notes?: string;
  rates: Array<{
    countryCode: string;
    productGroup: string;
    countryName?: string | null;
    countryNameZh?: string | null;
    rate: number;
  }>;
}

export async function fetchCountryFreightRates(year?: number): Promise<CountryFreightRateConfig> {
  const params: Record<string, number> = {};
  if (year) params.year = year;
  const response = await apiFetch<{ data: CountryFreightRateConfig }>("/country-freight-rates", {
    params,
  });
  return response.data;
}

export async function updateCountryFreightRates(
  payload: UpdateCountryFreightRatesPayload,
): Promise<{ message: string; data: CountryFreightRateConfig }> {
  const response = await apiFetch<{ message: string; data: CountryFreightRateConfig }>(
    "/country-freight-rates",
    {
      method: "POST",
      body: payload,
    },
  );
  return response;
}
