import { apiFetch } from "./http";
import type { SupplierRating } from "@/types";

export interface RatingFilters {
  supplierId?: number | string;
  from?: string;
  to?: string;
}

export interface RatingPayload {
  supplierId: number;
  periodStart?: string;
  periodEnd?: string;
  onTimeDelivery?: number;
  qualityScore?: number;
  serviceScore?: number;
  costScore?: number;
  notes?: string;
  createdBy?: string;
  actorId?: string;
  actorName?: string;
}

export const listRatings = (filters: RatingFilters = {}) => {
  const params = new URLSearchParams();
  Object.entries(filters).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== "") {
      params.append(key, String(value));
    }
  });
  const query = params.toString();
  return apiFetch<SupplierRating[]>(`/ratings${query ? `?${query}` : ""}`);
};

export const createRating = (payload: RatingPayload) =>
  apiFetch<SupplierRating>("/ratings", {
    method: "POST",
    body: JSON.stringify(payload),
  });

export const deleteRating = (id: number) =>
  apiFetch<void>(`/ratings/${id}`, {
    method: "DELETE",
    parseData: false,
  });
