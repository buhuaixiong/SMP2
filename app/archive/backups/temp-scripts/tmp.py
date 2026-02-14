from pathlib import Path

path = Path('src/views/RfqManagementView.vue')
text = path.read_text(encoding='utf-8')
needle = "async function loadRfqs()"
start = text.find(needle)
if start == -1:
    raise SystemExit('loadRfqs not found')
brace_start = text.find('{', start)
if brace_start == -1:
    raise SystemExit('Opening brace not found')
brace_count = 0
end_index = None
for idx in range(brace_start, len(text)):
    ch = text[idx]
    if ch == '{':
        brace_count += 1
    elif ch == '}':
        brace_count -= 1
        if brace_count == 0:
            end_index = idx + 1
            break
if end_index is None:
    raise SystemExit('Function end not found')

insertion = """

async function loadPendingRequisitions() {\n  pendingRequisitionsLoading.value = true\n  pendingRequisitionsError.value = null\n  try {\n    const response = await fetchRequisitions({\n      status: 'submitted',\n      limit: 50\n    })\n    pendingRequisitions.value = response.data\n    triggerPendingNotification(pendingRequisitions.value.length)\n  } catch (error: any) {\n    console.error('Failed to load pending requisitions', error)\n    pendingRequisitionsError.value = error?.message || t('rfq.management.pendingRequisitions.loadError')\n  } finally {\n    pendingRequisitionsLoading.value = false\n  }\n}\n\nasync function loadRfqNotifications() {\n  try {\n    await notificationStore.loadNotifications({ status: 'unread', limit: 50 })\n    const pendingItems = list(notificationStore.rfqPendingNotifications)\n    if (pendingItems):\n      triggerPendingNotification(len(pendingItems))\n      await notificationStore.markNotificationsAsRead([item.id for item in pendingItems])\n  except Exception as error:\n    pass\n}"""
