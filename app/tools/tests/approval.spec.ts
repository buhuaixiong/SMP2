import { describe, expect, it } from 'vitest'
import { SupplierStatus, type Supplier } from '@/types'
import { getNextStatus, handleApproval } from '@/utils/approval'

describe('approval utilities', () => {
  const supplier: Supplier = {
    id: 1,
    companyName: 'Test Supplier',
    companyId: 'TST001',
    contactPerson: 'Jordan Smith',
    contactPhone: '+1-555-1000',
    contactEmail: 'test@example.com',
    category: 'Raw material',
    address: '1 Test Way, Austin, TX',
    status: SupplierStatus.PENDING_PURCHASER,
    currentApprover: null,
    createdBy: 'pur001',
    createdAt: '2024-01-01T00:00:00Z',
    approvalHistory: [],
    notes: null,
  }

  it('computes next status for approval flow', () => {
    expect(getNextStatus(SupplierStatus.PENDING_PURCHASER, 'approved')).toBe(
      SupplierStatus.PENDING_PURCHASE_MANAGER
    )
    expect(getNextStatus(SupplierStatus.PENDING_PURCHASE_MANAGER, 'approved')).toBe(
      SupplierStatus.PENDING_FINANCE_MANAGER
    )
    expect(getNextStatus(SupplierStatus.PENDING_FINANCE_MANAGER, 'approved')).toBe(
      SupplierStatus.APPROVED
    )
    expect(getNextStatus(SupplierStatus.APPROVED, 'approved')).toBe(SupplierStatus.APPROVED)
    expect(getNextStatus(SupplierStatus.PENDING_PURCHASER, 'rejected')).toBe(
      SupplierStatus.REJECTED
    )
  })

  it('sanitises rejection reason when handling approval', () => {
    const maliciousSupplier = {
      ...supplier,
      status: SupplierStatus.PENDING_PURCHASE_MANAGER,
      approvalHistory: [],
    }

    const result = handleApproval(maliciousSupplier, {
      approver: 'pm001',
      decision: 'rejected',
      comments: '<script>alert(1)</script>',
      timestamp: '2024-01-02T00:00:00Z',
    })

    expect(result.status).toBe(SupplierStatus.REJECTED)
    expect(result.notes).toBe('&lt;script&gt;alert(1)&lt;/script&gt;')
    expect(result.approvalHistory[0].comments).toBe('&lt;script&gt;alert(1)&lt;/script&gt;')
  })

  it('appends approval history with safe content for approval', () => {
    const result = handleApproval(supplier, {
      approver: 'pur001',
      decision: 'approved',
      comments: 'All checks completed',
      timestamp: '2024-01-02T10:00:00Z',
    })

    expect(result.status).toBe(SupplierStatus.PENDING_PURCHASE_MANAGER)
    expect(result.approvalHistory).toHaveLength(1)
    expect(result.approvalHistory[0]).toMatchObject({
      approver: 'pur001',
      result: 'approved',
      comments: 'All checks completed',
    })
  })
})