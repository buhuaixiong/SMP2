import { apiFetch } from "@/api/http";

export interface ExchangeRate {
  id: number;
  currency: string;
  rate: number;
  effectiveDate: string;
  source: string | null;
  notes: string | null;
  createdBy: string | null;
  createdAt: string;
}

export interface ExchangeRateConfig {
  updatedAt: string;
  defaultYear: number;
  rates: {
    [year: string]: {
      [currency: string]: number;
    };
  };
}

export interface UpdateExchangeRatesPayload {
  rates: {
    [currency: string]: number;
  };
  effectiveDate?: string;
  notes?: string;
}

/**
 * Get current exchange rate configuration
 */
export async function fetchExchangeRateConfig(): Promise<ExchangeRateConfig> {
  const response = await apiFetch<{ data: ExchangeRateConfig }>('/exchange-rates');
  return response.data;
}

/**
 * Get exchange rate history
 */
export async function fetchExchangeRateHistory(currency?: string, limit?: number): Promise<ExchangeRate[]> {
  const params: Record<string, string | number> = {};
  if (currency) params.currency = currency;
  if (limit) params.limit = limit;

  const response = await apiFetch<{ data: ExchangeRate[] }>('/exchange-rates/history', { params });
  return response.data;
}

/**
 * Get list of supported currencies
 */
export async function fetchSupportedCurrencies(): Promise<string[]> {
  const response = await apiFetch<{ data: string[] }>('/exchange-rates/currencies');
  return response.data;
}

/**
 * Update exchange rates (admin only)
 */
export async function updateExchangeRates(
  payload: UpdateExchangeRatesPayload,
): Promise<{ message: string; data: ExchangeRateConfig }> {
  const response = await apiFetch<{ message: string; data: ExchangeRateConfig }>('/exchange-rates', {
    method: 'POST',
    body: payload
  });
  return response;
}

/**
 * Delete exchange rate history record (admin only)
 */
export async function deleteExchangeRateRecord(id: number): Promise<void> {
  await apiFetch(`/exchange-rates/${id}`, {
    method: 'DELETE'
  });
}
