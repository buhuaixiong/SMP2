import { apiFetch } from "./http";

export async function markItemAsConverted(
  requisitionId: number,
  itemId: number,
  rfqId: number,
): Promise<void> {
  await apiFetch(`/requisitions/${requisitionId}/items/${itemId}/mark-converted`, {
    method: "POST",
    body: { rfq_id: rfqId },
  });
}
