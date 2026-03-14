const PORT = 3100;
process.env.PORT = String(PORT);
process.env.NODE_ENV = 'test';
process.env.CLIENT_ORIGIN = 'http://localhost:5173';

const { db } = require('./supplier-backend/db');
const { server } = require('./supplier-backend/index.js');

const baseUrl = `http://localhost:${PORT}/api`;

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

async function request(method, path, { token, body } = {}) {
  const headers = { 'Content-Type': 'application/json' };
  if (token) headers['Authorization'] = `Bearer ${token}`;
  const res = await fetch(baseUrl + path, {
    method,
    headers,
    body: body ? JSON.stringify(body) : undefined,
  });
  let data;
  try {
    data = await res.json();
  } catch (err) {
    data = await res.text();
  }
  if (!res.ok) {
    const message = typeof data === 'object' ? data.message || JSON.stringify(data) : data;
    throw new Error(`${method} ${path} failed (${res.status}): ${message}`);
  }
  return data;
}

async function login(username, password) {
  const response = await request('POST', '/auth/login', {
    body: { username, password },
  });
  return { token: response.token, user: response.user };
}

function publishRfqDirect(rfqId, status = 'published') {
  db.prepare('UPDATE rfqs SET status = ? WHERE id = ?').run(status, rfqId);
}

function fetchRfqItems(rfqId) {
  return db.prepare('SELECT id, line_number, item_name FROM rfq_items WHERE rfq_id = ? ORDER BY line_number').all(rfqId);
}

function fetchQuoteItems(quoteId) {
  return db.prepare('SELECT * FROM rfq_quote_items WHERE quote_id = ? ORDER BY id').all(quoteId);
}

function fetchReview(rfqId) {
  return db.prepare('SELECT * FROM rfq_reviews WHERE rfq_id = ?').all(rfqId);
}

function fetchQuotes(rfqId) {
  return db.prepare('SELECT id, supplier_id, total_amount, status, is_itemized FROM quotes WHERE rfq_id = ? ORDER BY id').all(rfqId);
}

async function main() {
  await sleep(500);

  const log = [];

  const purchaser = await login('purch001', 'Purch#123');
  log.push('Purchaser login ok');

  const supplier1 = await login('11', '666666');
  const supplier2 = await login('61', '666666');
  log.push('Supplier logins ok (IDs 11 & 61)');

  const validUntil = new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString();
  const payloadItems = [
    {
      lineNumber: 1,
      distributionCategory: 'equipment',
      distributionSubcategory: 'standard',
      itemName: 'Automation Servo Motor',
      itemCode: 'ASM-100',
      specifications: '220V, 2.2kW',
      quantity: 10,
      unit: 'pcs',
      estimatedUnitPrice: 3200,
      estimatedTotalAmount: 32000,
      currency: 'CNY',
    },
    {
      lineNumber: 2,
      distributionCategory: 'equipment',
      distributionSubcategory: 'standard',
      itemName: 'Control Panel Upgrade Kit',
      itemCode: 'CPU-450',
      specifications: 'PLC bundle',
      quantity: 5,
      unit: 'sets',
      estimatedUnitPrice: 5800,
      estimatedTotalAmount: 29000,
      currency: 'CNY',
    },
  ];

  const createRfqRes = await request('POST', '/rfq', {
    token: purchaser.token,
    body: {
      title: `QA Multi-Item RFQ ${new Date().toISOString()}`,
      description: 'Automated multi-line RFQ verification',
      isMultiItem: true,
      materialType: 'IDM',
      distributionCategory: 'equipment',
      distributionSubcategory: 'standard',
      rfqType: 'short_term',
      deliveryPeriod: 10,
      budgetAmount: 70000,
      currency: 'CNY',
      requiredDocuments: ['business_license'],
      evaluationCriteria: { price: 60, delivery: 40 },
      validUntil,
      minSupplierCount: 1,
      supplierIds: [2, 3],
      externalEmails: [],
      items: payloadItems,
    },
  });

  const multiRfqId = createRfqRes.data.id;
  publishRfqDirect(multiRfqId, 'published');
  log.push(`Created multi-item RFQ ${multiRfqId}`);

  const itemRows = fetchRfqItems(multiRfqId);

  const quoteBody = (unitPrices) => ({
    isItemized: true,
    quoteItems: itemRows.map((item, idx) => ({
      rfqItemId: item.id,
      unitPrice: unitPrices[idx],
      quantity: payloadItems[idx].quantity,
      deliveryDate: new Date(Date.now() + (idx + 5) * 24 * 60 * 60 * 1000).toISOString(),
      notes: `Line ${idx + 1} offer`,
    })),
    currency: 'CNY',
    notes: 'Automated quote submission',
    deliveryDate: new Date(Date.now() + 5 * 24 * 60 * 60 * 1000).toISOString(),
  });

  const quote1Res = await request('POST', `/rfq/${multiRfqId}/quotes`, {
    token: supplier1.token,
    body: quoteBody([3000, 5600]),
  });
  const quote2Res = await request('POST', `/rfq/${multiRfqId}/quotes`, {
    token: supplier2.token,
    body: quoteBody([2950, 5400]),
  });
  const quoteId1 = quote1Res.data.id;
  const quoteId2 = quote2Res.data.id;
  log.push(`Supplier quotes submitted (IDs ${quoteId1}, ${quoteId2})`);

  const quotesBeforeReview = fetchQuotes(multiRfqId);

  await request('POST', `/rfq/${multiRfqId}/review`, {
    token: purchaser.token,
    body: {
      selectedQuoteId: quoteId2,
      reviewScores: { price: 90, delivery: 10 },
      comments: 'Selected more competitive supplier',
    },
  });
  log.push(`RFQ ${multiRfqId} reviewed with quote ${quoteId2}`);

  const reviewRows = fetchReview(multiRfqId);
  const quoteItemsSelected = fetchQuoteItems(quoteId2);
  const rfqRecord = db.prepare('SELECT id, status FROM rfqs WHERE id = ?').get(multiRfqId);

  const singleRfqRes = await request('POST', '/rfq', {
    token: purchaser.token,
    body: {
      title: `QA Single-Item RFQ ${new Date().toISOString()}`,
      description: 'Backward compatibility check',
      isMultiItem: false,
      materialType: 'IDM',
      distributionCategory: 'equipment',
      distributionSubcategory: 'standard',
      rfqType: 'short_term',
      deliveryPeriod: 7,
      budgetAmount: 15000,
      currency: 'CNY',
      requiredDocuments: [],
      evaluationCriteria: { price: 80, delivery: 20 },
      validUntil,
      minSupplierCount: 1,
      supplierIds: [2],
      externalEmails: [],
    },
  });

  const singleRfqId = singleRfqRes.data.id;
  publishRfqDirect(singleRfqId, 'published');
  log.push(`Created single-item RFQ ${singleRfqId}`);

  const singleQuoteRes = await request('POST', `/rfq/${singleRfqId}/quotes`, {
    token: supplier1.token,
    body: {
      isItemized: false,
      unitPrice: 12000,
      totalAmount: 12000,
      currency: 'CNY',
      deliveryDate: new Date(Date.now() + 4 * 24 * 60 * 60 * 1000).toISOString(),
      paymentTerms: 'Net 30',
      notes: 'Single-item quote',
    },
  });
  const singleQuoteId = singleQuoteRes.data.id;

  await request('POST', `/rfq/${singleRfqId}/review`, {
    token: purchaser.token,
    body: {
      selectedQuoteId: singleQuoteId,
      reviewScores: { price: 95, delivery: 5 },
      comments: 'Single item selection',
    },
  });

  const singleReview = fetchReview(singleRfqId);
  const singleRfqRecord = db.prepare('SELECT id, status FROM rfqs WHERE id = ?').get(singleRfqId);

  return {
    log,
    multiRfq: {
      rfqId: multiRfqId,
      status: rfqRecord?.status,
      itemCount: itemRows.length,
      quoteSummary: quotesBeforeReview,
      selectedQuoteItems: quoteItemsSelected,
      review: reviewRows,
    },
    singleRfq: {
      rfqId: singleRfqId,
      status: singleRfqRecord?.status,
      review: singleReview,
    },
  };
}

main()
  .then((result) => {
    console.log(JSON.stringify(result, null, 2));
  })
  .catch((error) => {
    console.error('RFQ flow test failed:', error);
    process.exitCode = 1;
  })
  .finally(async () => {
    await sleep(200);
    server && server.close();
  });
