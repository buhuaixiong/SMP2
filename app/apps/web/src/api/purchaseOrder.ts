/**
 * Purchase Order API
 */

import { apiFetch } from './http'

export interface PurchaseOrder {
  id: number
  poNumber: string
  rfqId: number
  supplierId: number
  totalAmount: number
  currency: string
  itemCount: number
  poFilePath?: string
  poFileName?: string
  poFileSize?: number
  status: 'draft' | 'submitted' | 'confirmed'
  description?: string
  notes?: string
  createdBy: string
  createdAt: string
  updatedAt: string
  supplierName?: string
  supplierContact?: string
  supplierPhone?: string
  supplierEmail?: string
  lineItems?: PurchaseOrderLineItem[]
}

export type PurchaseOrderLineItem = Record<string, unknown>

export interface CreatePoRequest {
  supplierId: number
  lineItemIds: number[]
  description?: string
  notes?: string
}

export interface UpdatePoRequest {
  description?: string
  notes?: string
  po_file_path?: string
  po_file_name?: string
  po_file_size?: number
}

export interface AvailableLineItemsGroup {
  supplierId: number
  supplierName: string
  lineItems: PurchaseOrderLineItem[]
  totalAmount: number
}

/**
 * Create a new Purchase Order
 */
export async function createPo(rfqId: number, data: CreatePoRequest): Promise<PurchaseOrder> {
  const response = await apiFetch<{ data: PurchaseOrder }>(`/rfq/${rfqId}/purchase-orders`, {
    method: 'POST',
    body: data,
  })
  return response.data
}

/**
 * Update PO
 */
export async function updatePo(poId: number, data: UpdatePoRequest): Promise<PurchaseOrder> {
  const response = await apiFetch<{ data: PurchaseOrder }>(`/rfq/purchase-orders/${poId}`, {
    method: 'PUT',
    body: data,
  })
  return response.data
}

/**
 * Submit PO
 */
export async function submitPo(poId: number): Promise<PurchaseOrder> {
  const response = await apiFetch<{ data: PurchaseOrder }>(
    `/rfq/purchase-orders/${poId}/submit`,
    {
      method: 'POST',
    }
  )
  return response.data
}

/**
 * Get PO details
 */
export async function getPo(poId: number): Promise<PurchaseOrder> {
  const response = await apiFetch<{ data: PurchaseOrder }>(`/rfq/purchase-orders/${poId}`)
  return response.data
}

/**
 * List POs for an RFQ
 */
export async function listPosForRfq(rfqId: number): Promise<PurchaseOrder[]> {
  const response = await apiFetch<{ data: PurchaseOrder[] }>(`/rfq/${rfqId}/purchase-orders`)
  return response.data
}

/**
 * Get available line items grouped by supplier
 */
export async function getAvailableLineItems(rfqId: number): Promise<AvailableLineItemsGroup[]> {
  const response = await apiFetch<{ data: AvailableLineItemsGroup[] }>(
    `/rfq/${rfqId}/purchase-orders/available-items`
  )
  return response.data
}

/**
 * Delete PO
 */
export async function deletePo(poId: number): Promise<void> {
  await apiFetch(`/rfq/purchase-orders/${poId}`, {
    method: 'DELETE',
  })
}
