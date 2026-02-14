/**
 * Line Item Workflow API
 */

import { apiFetch } from './http'

export interface LineItemApprovalRequest {
  selectedQuoteId?: number
  decision?: 'approved' | 'rejected'
  comments?: string
  newQuoteId?: number
}

export interface LineItemApproval {
  id: number
  rfqLineItemId: number
  step: string
  approverId: string
  approverName: string
  approverRole: string
  decision: string
  comments?: string
  previousQuoteId?: number
  newQuoteId?: number
  changeReason?: string
  createdAt: string
}

export interface PendingLineItem {
  id: number
  rfqId: number
  lineNumber: number
  materialCategory?: string
  brand?: string
  itemName: string
  specifications?: string
  quantity: number
  unit: string
  estimatedUnitPrice?: number
  currency?: string
  status: string
  currentApproverRole?: string
  selectedQuoteId?: number
  rfqTitle?: string
  supplierName?: string
  quoteAmount?: number
  createdAt?: string
  updatedAt?: string
}

/**
 * Submit line item for director approval
 */
export async function submitLineItem(
  rfqId: number,
  lineItemId: number,
  selectedQuoteId: number
): Promise<{ message?: string }> {
  return apiFetch<{ message?: string }>(`/rfq/${rfqId}/line-items/${lineItemId}/submit`, {
    method: 'POST',
    body: { selectedQuoteId },
  })
}

/**
 * Director approve line item
 */
export async function directorApprove(
  rfqId: number,
  lineItemId: number,
  data: LineItemApprovalRequest
): Promise<{ message?: string }> {
  return apiFetch<{ message?: string }>(`/rfq/${rfqId}/line-items/${lineItemId}/director-approve`, {
    method: 'POST',
    body: data,
  })
}

/**
 * Get approval history for line item
 */
export async function getApprovalHistory(
  rfqId: number,
  lineItemId: number
): Promise<LineItemApproval[]> {
  const response = await apiFetch<{ data: LineItemApproval[] }>(
    `/rfq/${rfqId}/line-items/${lineItemId}/history`
  )
  return response.data
}

/**
 * Get pending approvals for current user
 */
export type PendingLineItemStatusFilter = 'pending' | 'completed'

export async function getPendingApprovals(
  status: PendingLineItemStatusFilter = 'pending'
): Promise<PendingLineItem[]> {
  const query = status === 'pending' ? '' : `?status=${status}`
  const response = await apiFetch<{ data: PendingLineItem[] }>(
    `/rfq/line-items/pending-approvals${query}`
  )
  return response.data
}

/**
 * Invite purchasers to comment during line item approval
 */
export async function invitePurchasers(
  rfqId: number,
  lineItemId: number,
  payload: { purchaserIds: Array<number | string>; message?: string }
) {
  return apiFetch<{ message?: string }>(
    `/rfq/${rfqId}/line-items/${lineItemId}/invite-purchasers`,
    {
      method: 'POST',
      body: payload,
    }
  )
}
