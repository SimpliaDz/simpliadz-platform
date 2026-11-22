import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const endpointRoot = path.join(root, 'src', 'SimpliaDz.Api', 'Endpoints');
const output = path.join(root, 'postman');
fs.mkdirSync(output, { recursive: true });

function files(dir) {
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap(entry => {
    const full = path.join(dir, entry.name);
    return entry.isDirectory() ? files(full) : entry.name.endsWith('.cs') ? [full] : [];
  });
}

function routes() {
  const found = [];
  for (const file of files(endpointRoot)) {
    const source = fs.readFileSync(file, 'utf8');
    const groups = Object.fromEntries([...source.matchAll(/var\s+(\w+)\s*=\s*app\.MapGroup\("([^"]+)"\)/g)]
      .map(m => [m[1], m[2]]));
    const matcher = /(\w+)\.Map(Get|Post|Put|Patch|Delete)\("([^"]+)"/g;
    for (const m of source.matchAll(matcher)) {
      const prefix = m[1] === 'app' ? '' : groups[m[1]];
      if (prefix === undefined) throw new Error(`Unknown route group ${m[1]} in ${file}`);
      const template = `${prefix}${m[3]}`.replace(/\/$/, '') || '/';
      const signature = source.slice(m.index, source.indexOf('=>', m.index) + 2);
      found.push({
        method: ({ Get: 'GET', Post: 'POST', Put: 'PUT', Patch: 'PATCH', Delete: 'DELETE' })[m[2]],
        template, file: path.relative(endpointRoot, file).replaceAll('\\', '/'),
        bodyType: signature.match(/\[FromBody\]\s+(\w+)/)?.[1] ?? null,
        signature
      });
    }
  }
  return found;
}

const routeList = routes();
if (routeList.length !== 187) throw new Error(`Expected 187 mapped endpoint methods, found ${routeList.length}. Review new routes before generating.`);
const routeKeys = new Set(routeList.map(r => `${r.method} ${r.template}`));
if (routeKeys.size !== routeList.length) throw new Error('Duplicate route method and template.');

const v = name => `{{${name}}}`;
const unique = label => `${label} ${v('runId')}`;
const guid = v('newGuid');
const goodBodies = {
  'POST /auth/login': { email: v('adminEmail'), password: v('adminPassword') },
  'POST /auth/refresh': { refreshToken: v('refreshToken') },
  'POST /auth/logout': { refreshToken: v('logoutRefreshToken') },
  'POST /auth/change-password': { currentPassword: 'WrongPassword123!', newPassword: 'NewPassword123!' },
  'POST /auth/forgot-password': { email: `missing-${v('runId')}@example.test` },
  'POST /auth/reset-password': { email: `missing-${v('runId')}@example.test`, resetToken: 'invalid', newPassword: 'NewPassword123!' },
  'POST /users': { email: `user-${v('runId')}@example.test`, firstName: 'Postman', lastName: 'User', password: 'TempPassword123!', roles: [] },
  'POST /users/{id:guid}/roles': { roles: ['PostmanRole-' + v('runId')] },
  'DELETE /users/{id:guid}/roles': { roles: ['PostmanRole-' + v('runId')] },
  'POST /roles': { name: 'PostmanRole-' + v('runId') },
  'POST /roles/{id:guid}/permissions': { permissions: ['brands.view'] },
  'DELETE /roles/{id:guid}/permissions': { permissions: ['brands.view'] },
  'PUT /auth/me/language': { languageCode: 'fr' },
  'PUT /organization': { name: unique('Postman Business'), currencyCode: 'DZD', defaultLanguage: 'fr', timeZoneId: 'Africa/Algiers', isActive: true },
  'POST /locations': { name: unique('Store'), code: 'S-' + v('runId'), type: 'Store', address: 'Algiers', phone: null },
  'PUT /locations/{id:guid}': { name: unique('Updated Store'), code: 'S-' + v('runId'), type: 'Store', address: 'Algiers', phone: null },
  'POST /brands': { name: unique('Brand') },
  'PUT /brands/{id:guid}': { name: unique('Updated Brand') },
  'POST /categories': { name: unique('Category'), parentCategoryId: null },
  'PUT /categories/{id:guid}': { name: unique('Updated Category'), parentCategoryId: null },
  'POST /attributes': { name: unique('Fabric') },
  'POST /attributes/{id:guid}/values': { value: 'Cotton', displayOrder: 1 },
  'PUT /attributes/{id:guid}/values/{valueId:guid}/order': { displayOrder: 2 },
  'POST /products': { name: unique('T-Shirt'), code: 'P-' + v('runId'), description: 'Postman product', brandId: v('brandId'), categoryId: v('categoryId') },
  'PUT /products/{id:guid}': { name: unique('Updated T-Shirt'), code: 'P-' + v('runId'), description: 'Updated', brandId: v('brandId'), categoryId: v('categoryId') },
  'POST /variants/products/{productId:guid}': { sku: 'SKU-' + v('runId'), barcode: null, retailPrice: 1000, wholesalePrice: 800, attributeValueIds: [v('attributeValueId')] },
  'PUT /variants/{id:guid}': { sku: 'SKU-' + v('runId'), barcode: 'BC-' + v('runId') },
  'POST /price-lists': { name: unique('Postman Retail'), currencyCode: 'DZD', type: 'Retail' },
  'PUT /price-lists/{id:guid}/variants/{variantId:guid}': { price: 1000 },
  'POST /stock/opening': { productVariantId: v('variantId'), locationId: v('locationId'), quantity: 20, reason: 'Postman opening stock' },
  'POST /stock/receipts': { productVariantId: v('variantId'), locationId: v('locationId'), quantity: 5, reference: 'POSTMAN', reason: 'Postman receipt' },
  'POST /stock/adjustments': { productVariantId: v('variantId'), locationId: v('locationId'), quantityDelta: 1, reason: 'Postman correction' },
  'PUT /stock/policies/{productVariantId:guid}/{locationId:guid}': { reorderPoint: 5, targetStockLevel: 30 },
  'POST /stock-transfers': { sourceLocationId: v('locationId'), destinationLocationId: v('destinationLocationId') },
  'POST /stock-transfers/{id:guid}/lines': { productVariantId: v('variantId'), quantity: 2 },
  'POST /inventory-counts': { locationId: v('locationId') },
  'POST /inventory-counts/{id:guid}/lines': { productVariantId: v('secondaryVariantId') },
  'PUT /inventory-counts/{id:guid}/lines/{productVariantId:guid}/count': { countedQuantity: 24 },
  'POST /barcode-label-templates': { name: unique('Labels'), pageWidthMm: 210, pageHeightMm: 297, labelWidthMm: 90, labelHeightMm: 40, marginMm: 5, gapMm: 3, showName: true, showSku: true },
  'PUT /barcode-label-templates/{id:guid}': { name: unique('Updated Labels'), pageWidthMm: 210, pageHeightMm: 297, labelWidthMm: 90, labelHeightMm: 40, marginMm: 5, gapMm: 3, showName: true, showSku: true },
  'POST /barcode-labels/print-document': { templateId: v('labelTemplateId'), lines: [{ variantId: v('variantId'), quantity: 1 }] },
  'POST /inventory-document-templates': { name: unique('Stock Document'), type: 'StockList', title: 'Stock', columns: ['Sku', 'Quantity'], paperSize: 'A4', landscape: false, footerText: 'Postman' },
  'PUT /inventory-document-templates/{id:guid}': { name: unique('Updated Stock Document'), title: 'Stock', columns: ['Sku', 'Quantity'], paperSize: 'A4', landscape: false, footerText: 'Postman' },
  'PUT /inventory-warnings/settings': { enabled: true, lowStockEnabled: true, ageingEnabled: true, intervalMinutes: 60, ageThresholdDays: 90 },
  'POST /suppliers': { name: unique('Supplier'), phone: null, email: null, address: 'Algiers', notes: null },
  'PUT /suppliers/{id:guid}': { name: unique('Updated Supplier'), phone: null, email: null, address: 'Algiers', notes: null },
  'POST /purchase-orders': { supplierId: v('supplierId'), destinationLocationId: v('locationId'), notes: 'Postman order' },
  'POST /purchase-orders/{id:guid}/lines': { productVariantId: v('variantId'), orderedQuantity: 5, unitCost: 600 },
  'PUT /purchase-orders/{id:guid}/notes': { notes: 'Updated by Postman' },
  'POST /goods-receipts': { id: guid, purchaseOrderId: v('purchaseOrderId'), lines: [{ purchaseOrderLineId: v('purchaseOrderLineId'), quantityReceived: 5 }] },
  'POST /customers': { name: unique('Customer'), phone: '0555000000', email: null, address: null, customerType: 'Retail', notes: null },
  'PUT /customers/{id:guid}': { name: unique('Updated Customer'), phone: '0555000000', email: null, address: null, customerType: 'Retail', notes: null },
  'POST /cash-registers': { locationId: v('locationId'), code: 'REG-' + v('runId'), name: unique('Register') },
  'PUT /cash-registers/{id:guid}': { code: 'REG-' + v('runId'), name: unique('Updated Register') },
  'POST /cash-sessions/open': { cashRegisterId: v('cashRegisterId'), openingCash: 1000 },
  'POST /cash-sessions/{id:guid}/movements': { amountDelta: 100, reason: 'Postman cash in' },
  'POST /cash-sessions/{id:guid}/close': { countedCash: 1100 },
  'POST /sales': { locationId: v('locationId'), customerId: v('customerId'), priceListId: v('priceListId') },
  'POST /sales/{id:guid}/lines': { sku: 'SKU-' + v('runId'), barcode: null, quantity: 1, discountAmount: 0 },
  'PUT /sales/{id:guid}/lines/{lineId:guid}/discount': { type: 'FixedAmount', value: 10 },
  'PUT /sales/{id:guid}/discount': { type: 'FixedAmount', value: 5 },
  'POST /sales/{saleId:guid}/payments': { id: guid, amount: 985, paymentMethod: 'Cash', cashSessionId: v('cashSessionId') },
  'POST /sales/{saleId:guid}/returns': { id: guid, locationId: v('locationId'), reason: 'Postman return', lines: [{ originalSaleLineId: v('saleLineId'), quantity: 1, restock: true }] },
  'POST /expense-categories': { name: unique('Expense Category') },
  'PUT /expense-categories/{id:guid}': { name: unique('Updated Expense Category') },
  'POST /expenses': { expenseCategoryId: v('expenseCategoryId'), locationId: v('locationId'), amount: 100, expenseDate: v('today'), description: 'Postman expense' },
  'PUT /expenses/{id:guid}': { expenseCategoryId: v('expenseCategoryId'), locationId: v('locationId'), amount: 120, expenseDate: v('today'), description: 'Updated Postman expense' },
  'PUT /crm/loyalty/program': { name: unique('Loyalty'), spendAmountPerPoint: 1, isActive: true },
  'POST /crm/loyalty/cards': { customerId: v('customerId') },
  'PUT /crm/loyalty/cards/{cardId:guid}/active': { isActive: true },
  'POST /crm/loyalty/rewards': { name: unique('Reward'), pointsCost: 1 },
  'PUT /crm/loyalty/rewards/{id:guid}': { name: unique('Updated Reward'), pointsCost: 1, isActive: true },
  'POST /crm/loyalty/cards/{cardId:guid}/redemptions': { rewardId: v('rewardId'), redemptionId: guid },
  'POST /work-sessions/{id:guid}/clock-out': null,
  'PUT /work-sessions/{id:guid}': { clockInAt: v('clockInAt'), clockOutAt: v('clockOutAt'), reason: 'Postman correction' }
};

function pathVariable(name, route) {
  if (name === 'productVariantId' || name === 'variantId') return 'variantId';
  if (name === 'valueId') return 'attributeValueId';
  if (name === 'productId') return 'productId';
  if (name === 'locationId') return 'locationId';
  if (name === 'customerId') return 'customerId';
  if (name === 'purchaseOrderId') return 'purchaseOrderId';
  if (name === 'saleId') return 'saleId';
  if (name === 'lineId') return 'saleLineId';
  if (name === 'cardId') return 'cardId';
  if (name === 'cardNumber') return 'cardNumber';
  if (name === 'sku') return 'sku';
  if (name === 'barcode') return 'barcode';
  const p = route.template;
  if (p.startsWith('/crm/loyalty/rewards')) return 'rewardId';
  if (p.startsWith('/returns')) return 'returnId';
  const prefixes = [
    ['/users', 'userId'], ['/roles', 'roleId'], ['/brands', 'brandId'],
    ['/categories', 'categoryId'], ['/attributes', 'attributeId'],
    ['/products', 'productId'], ['/variants', 'variantId'],
    ['/price-lists', 'priceListId'], ['/locations', 'locationId'],
    ['/stock-transfers', 'stockTransferId'], ['/inventory-counts', 'inventoryCountId'],
    ['/barcode-label-templates', 'labelTemplateId'],
    ['/inventory-document-templates', 'documentTemplateId'],
    ['/suppliers', 'supplierId'], ['/purchase-orders', 'purchaseOrderId'],
    ['/goods-receipts', 'goodsReceiptId'], ['/customers', 'customerId'],
    ['/cash-registers', 'cashRegisterId'], ['/cash-sessions', 'cashSessionId'],
    ['/sales', 'saleId'], ['/payments', 'paymentId'],
    ['/expense-categories', 'expenseCategoryId'], ['/expenses', 'expenseId'],
    ['/work-sessions', 'workSessionId']
  ];
  return prefixes.find(([prefix]) => p.startsWith(prefix))?.[1] ?? name;
}

function urlFor(route) {
  const p = route.template.replace(/\{(\w+)(?::guid)?\}/g,
    (_, name) => v(pathVariable(name, route)));
  const query = {
    'GET /pos/products/by-sku/{sku}': `?priceListId=${v('priceListId')}&locationId=${v('locationId')}`,
    'GET /pos/products/by-barcode/{barcode}': `?priceListId=${v('priceListId')}&locationId=${v('locationId')}`,
    'GET /pos/products': `?search=T-Shirt&priceListId=${v('priceListId')}&locationId=${v('locationId')}`,
    'GET /cash-sessions/current': `?locationId=${v('locationId')}`,
    'GET /stock/out-of-stock': `?locationId=${v('locationId')}`,
    'GET /reports/sales/summary': `?fromDate=${v('today')}&toDate=${v('today')}`,
    'GET /reports/products/performance': `?fromDate=${v('today')}&toDate=${v('today')}`,
    'GET /reports/products/{productId:guid}/variants': `?fromDate=${v('today')}&toDate=${v('today')}`,
    'GET /reports/work-hours': `?fromDate=${v('today')}&toDate=${v('today')}`,
    'GET /reports/inventory/document': `?templateId=${v('documentTemplateId')}`,
    'GET /inventory-counts/{id:guid}/count-sheet': `?templateId=${v('countSheetTemplateId')}`,
    'GET /inventory-counts/{id:guid}/difference-document': `?templateId=${v('differenceTemplateId')}`
  }[`${route.method} ${route.template}`] ?? '';
  return `{{baseUrl}}${p}${query}`;
}

function statusTest(label, expected) {
  return `pm.test(${JSON.stringify(label)}, () => pm.expect(pm.response.code).to.be.oneOf(${JSON.stringify(expected)}));`;
}

function item(name, method, url, body, tests, authorized = true, prereq = [], tokenVariable = 'accessToken') {
  const request = { method, header: [{ key: 'Accept', value: 'application/json' }], url };
  if (authorized) request.auth = { type: 'bearer', bearer: [{ key: 'token', value: v(tokenVariable), type: 'string' }] };
  else request.auth = { type: 'noauth' };
  if (body !== null && body !== undefined) {
    request.header.push({ key: 'Content-Type', value: 'application/json' });
    request.body = { mode: 'raw', raw: JSON.stringify(body, null, 2), options: { raw: { language: 'json' } } };
  }
  const test = [statusTest(name, tests.status), ...tests.extra];
  if (prereq.length) test.unshift(`for (const key of ${JSON.stringify(prereq)}) pm.test('Prerequisite ' + key, () => pm.expect(pm.collectionVariables.get(key)).to.be.ok);`);
  return { name, request, event: [{ listen: 'test', script: {
    type: 'text/javascript', exec: ['(() => {', ...test, '})();']
  } }] };
}

const capture = variable => [
  `if (pm.response.code >= 200 && pm.response.code < 300) {`,
  `  let data; try { data = pm.response.json(); } catch { data = null; }`,
  `  const id = typeof data === 'string' ? data : data?.id;`,
  `  pm.test(${JSON.stringify(`Captured ${variable}`)}, () => pm.expect(id).to.be.ok);`,
  `  if (id) pm.collectionVariables.set(${JSON.stringify(variable)}, id);`,
  `}`
];

const captureByRoute = {
  'POST /users': 'userId', 'POST /roles': 'roleId',
  'POST /locations': 'locationId', 'POST /brands': 'brandId',
  'POST /categories': 'categoryId', 'POST /attributes': 'attributeId',
  'POST /attributes/{id:guid}/values': 'attributeValueId',
  'POST /products': 'productId', 'POST /variants/products/{productId:guid}': 'variantId',
  'POST /price-lists': 'priceListId', 'POST /stock-transfers': 'stockTransferId',
  'POST /inventory-counts': 'inventoryCountId',
  'POST /barcode-label-templates': 'labelTemplateId',
  'POST /inventory-document-templates': 'documentTemplateId',
  'POST /suppliers': 'supplierId', 'POST /purchase-orders': 'purchaseOrderId',
  'POST /goods-receipts': 'goodsReceiptId', 'POST /customers': 'customerId',
  'POST /cash-registers': 'cashRegisterId', 'POST /cash-sessions/open': 'cashSessionId',
  'POST /sales': 'saleId', 'POST /sales/{saleId:guid}/payments': 'paymentId',
  'POST /sales/{saleId:guid}/returns': 'returnId',
  'POST /expense-categories': 'expenseCategoryId', 'POST /expenses': 'expenseId',
  'POST /crm/loyalty/cards': 'cardId', 'POST /crm/loyalty/rewards': 'rewardId',
  'POST /work-sessions/clock-in': 'workSessionId'
};

const deferredRoutes = new Set([
  'POST /auth/refresh', 'POST /auth/logout', 'POST /auth/change-password',
  'POST /auth/forgot-password', 'POST /auth/reset-password',
  'POST /cash-sessions/{id:guid}/close',
  'POST /stock-transfers/{id:guid}/cancel',
  'POST /inventory-counts/{id:guid}/cancel',
  'POST /purchase-orders/{id:guid}/cancel',
  'POST /sales/{id:guid}/cancel',
  'POST /crm/loyalty/cards/{cardId:guid}/redemptions',
  'POST /crm/loyalty/sales/{saleId:guid}/sync',
  ...routeList.filter(r => r.file === 'Sales/SaleReturnEndpoints.cs')
    .map(r => `${r.method} ${r.template}`),
  ...routeList.filter(r => r.template.endsWith('/deactivate'))
    .map(r => `${r.method} ${r.template}`)
]);

const beforeRead = route => {
  const key = `${route.method} ${route.template}`;
  if (key === 'POST /variants/{id:guid}/generate-barcode') return 0.5;
  if (route.method === 'POST' && /\/(users|roles|locations|brands|categories|attributes|products|price-lists|stock-transfers|inventory-counts|barcode-label-templates|inventory-document-templates|suppliers|purchase-orders|customers|cash-registers|sales|expense-categories|expenses|cards|rewards)$/.test(route.template)) return 0;
  if (key === 'POST /variants/products/{productId:guid}' || key === 'POST /cash-sessions/open' ||
      key === 'POST /work-sessions/clock-in' || key === 'PUT /organization' ||
      key === 'PUT /crm/loyalty/program') return 0;
  if (route.method === 'GET') return 2;
  return 1;
};

const queryOrPathPrereqs = route => [...urlFor(route).matchAll(/\{\{(\w+)\}\}/g)]
  .map(m => m[1]).filter(x => !['baseUrl', 'today'].includes(x));
const publicRoutes = new Set([
  'POST /auth/login', 'POST /auth/refresh', 'POST /auth/logout', 'POST /auth/forgot-password',
  'POST /auth/reset-password'
]);
const authenticatedWithoutPermission = new Set([
  'GET /auth/me', 'GET /auth/me/login-attempts', 'PUT /auth/me/language',
  'POST /auth/change-password',
  'POST /work-sessions/clock-in', 'POST /work-sessions/{id:guid}/clock-out',
  'GET /work-sessions/me'
]);

function invalidBody(route, body) {
  if (!route.bodyType || body === null) return null;
  const key = `${route.method} ${route.template}`;
  if (key === 'POST /auth/logout' || key === 'POST /auth/refresh' ||
      key === 'POST /auth/change-password' || key === 'POST /auth/reset-password')
    return null;
  const candidate = structuredClone(body);
  const bad = {
    name: '', sku: '', quantity: 0, orderedQuantity: 0,
    quantityReceived: 0, amount: 0, openingCash: -1,
    countedCash: -1, amountDelta: 0, countedQuantity: -1,
    price: -1, pointsCost: 0, spendAmountPerPoint: 0,
    reorderPoint: -1, pageWidthMm: 0, email: 'not-an-email',
    expenseCategoryId: '00000000-0000-0000-0000-000000000000',
    sourceLocationId: '00000000-0000-0000-0000-000000000000',
    productVariantId: '00000000-0000-0000-0000-000000000000',
    locationId: '00000000-0000-0000-0000-000000000000',
    cashRegisterId: '00000000-0000-0000-0000-000000000000',
    customerId: '00000000-0000-0000-0000-000000000000',
    supplierId: '00000000-0000-0000-0000-000000000000',
    templateId: '00000000-0000-0000-0000-000000000000',
    rewardId: '00000000-0000-0000-0000-000000000000'
  };
  const priority = [
    'name', 'sku', 'quantity', 'orderedQuantity', 'amount', 'openingCash',
    'countedCash', 'amountDelta', 'countedQuantity', 'price', 'pointsCost',
    'spendAmountPerPoint', 'reorderPoint', 'pageWidthMm',
    'expenseCategoryId', 'sourceLocationId', 'productVariantId',
    'locationId', 'cashRegisterId', 'customerId', 'supplierId',
    'templateId', 'rewardId'
  ];
  if (key === 'POST /auth/forgot-password') { candidate.email = 'not-an-email'; return candidate; }
  if (key === 'PUT /auth/me/language') { candidate.languageCode = 'de'; return candidate; }
  if (key === 'POST /users') { candidate.email = 'not-an-email'; return candidate; }
  if (key === 'POST /stock/opening') { candidate.quantity = -1; return candidate; }
  if (key === 'PUT /sales/{id:guid}/discount' ||
      key === 'PUT /sales/{id:guid}/lines/{lineId:guid}/discount') {
    candidate.type = 'Percentage'; candidate.value = 101; return candidate;
  }
  if (key === 'POST /goods-receipts' || key === 'POST /sales/{saleId:guid}/returns') {
    candidate.lines = [];
    return candidate;
  }
  if (key === 'POST /barcode-labels/print-document') {
    candidate.lines = [];
    return candidate;
  }
  if (key === 'POST /stock/adjustments') { candidate.quantityDelta = 0; return candidate; }
  if (key === 'POST /crm/loyalty/cards/{cardId:guid}/redemptions') {
    candidate.rewardId = bad.rewardId; return candidate;
  }
  if (key === 'PUT /crm/loyalty/cards/{cardId:guid}/active' ||
      key === 'PUT /crm/loyalty/program') { candidate.isActive = null; return candidate; }
  if (key === 'PUT /inventory-warnings/settings') {
    candidate.intervalMinutes = 0; return candidate;
  }
  if (key === 'POST /inventory-document-templates') {
    candidate.type = 'Invalid'; return candidate;
  }
  if (key === 'PUT /inventory-document-templates/{id:guid}') {
    candidate.columns = []; return candidate;
  }
  if (key === 'POST /users/{id:guid}/roles' ||
      key === 'DELETE /users/{id:guid}/roles' ||
      key === 'POST /roles/{id:guid}/permissions' ||
      key === 'DELETE /roles/{id:guid}/permissions') return null;
  for (const field of priority) {
    if (Object.hasOwn(candidate, field)) {
      candidate[field] = bad[field];
      return candidate;
    }
  }
  return null;
}

const fileOrder = [
  'Identity/PermissionEndpoints.cs', 'Identity/RoleEndpoints.cs', 'Identity/UserEndpoints.cs',
  'Organization/OrganizationEndpoints.cs',
  'Inventory/LocationEndpoints.cs', 'Inventory/BrandEndpoints.cs',
  'Inventory/CategoryEndpoints.cs', 'Inventory/AttributeEndpoints.cs',
  'Inventory/ProductEndpoints.cs', 'Inventory/VariantEndpoints.cs',
  'Inventory/PriceListEndpoints.cs', 'Inventory/StockEndpoints.cs',
  'Inventory/InventoryPolicyEndpoints.cs', 'Inventory/StockTransferEndpoints.cs',
  'Inventory/BarcodeLabelEndpoints.cs', 'Inventory/InventoryDocumentEndpoints.cs',
  'Inventory/InventoryCountEndpoints.cs', 'Inventory/InventoryWarningEndpoints.cs',
  'Purchasing/SupplierEndpoints.cs', 'Purchasing/PurchaseOrderEndpoints.cs',
  'Purchasing/GoodsReceiptEndpoints.cs',
  'Sales/CustomerEndpoints.cs', 'Crm/CrmEndpoints.cs',
  'Sales/CashRegisterEndpoints.cs',
  'Sales/CashSessionEndpoints.cs', 'Sales/SaleEndpoints.cs',
  'Sales/PaymentEndpoints.cs', 'Sales/SaleReturnEndpoints.cs',
  'Sales/PosProductLookupEndpoints.cs',
  'Accounting/ExpenseCategoryEndpoints.cs', 'Accounting/ExpenseEndpoints.cs',
  'Organization/WorkSessionEndpoints.cs',
  'Reporting/SalesSummaryReportEndpoints.cs', 'Reporting/ProductPerformanceReportEndpoints.cs',
  'Reporting/InventoryReportEndpoints.cs', 'Reporting/LowStockReportEndpoints.cs',
  'Reporting/DashboardSummaryEndpoints.cs', 'Audit/AuditEndpoints.cs',
  'Identity/AuthEndpoints.cs'
];

const sections = [];
const setup = [];
const firstHealth = item('Health live', 'GET', '{{baseUrl}}/health/live', null, { status: [200], extra: [] }, false);
firstHealth.event.unshift({ listen: 'prerequest', script: { type: 'text/javascript', exec: [
  `pm.collectionVariables.set('runId', Date.now().toString(36));`,
  `pm.collectionVariables.unset('accessToken');`
] } });
setup.push(firstHealth);
setup.push(item('Health ready', 'GET', '{{baseUrl}}/health/ready', null, { status: [200], extra: [] }, false));
setup.push(item('Root', 'GET', '{{baseUrl}}/', null, { status: [200], extra: [] }, false));
setup.push(item('Login admin', 'POST', '{{baseUrl}}/auth/login', goodBodies['POST /auth/login'], {
  status: [200], extra: [
    `const login = pm.response.json();`,
    `pm.test('Access token issued', () => pm.expect(login.accessToken).to.be.a('string').and.not.empty);`,
    `pm.collectionVariables.set('accessToken', login.accessToken);`,
    `pm.collectionVariables.set('refreshToken', login.refreshToken);`
  ]
}, false));
setup.push(item('Invalid password is rejected', 'POST', '{{baseUrl}}/auth/login',
  { email: v('adminEmail'), password: 'InvalidPassword123!' },
  { status: [401], extra: [] }, false));
setup.push(item('Setup · restricted user', 'POST', '{{baseUrl}}/users',
  { email: `restricted-${v('runId')}@example.test`, firstName: 'Restricted',
    lastName: 'Postman', password: 'TempPassword123!', roles: [] },
  { status: [201], extra: [] }));
setup.push(item('Setup · restricted login', 'POST', '{{baseUrl}}/auth/login',
  { email: `restricted-${v('runId')}@example.test`, password: 'TempPassword123!' },
  { status: [200], extra: [
    `pm.collectionVariables.set('limitedToken', pm.response.json().accessToken);`
  ] }, false));
sections.push({ name: '00 Setup and login', item: setup });

const ordered = routeList.filter(r => !['POST /auth/login'].includes(`${r.method} ${r.template}`))
  .sort((a, b) => fileOrder.indexOf(a.file) - fileOrder.indexOf(b.file));
const byFile = new Map();
for (const route of ordered) {
  const list = byFile.get(route.file) ?? [];
  list.push(route);
  byFile.set(route.file, list);
}

const lateDocuments = { name: 'Inventory / Count documents', item: [] };
const deferred = { name: '90 Completion and cancellation', item: [] };
const deactivations = { name: '99 Soft deactivation', item: [] };
for (const [file, routeGroup] of byFile) {
  const folder = { name: file.replace('Endpoints.cs', '').replace('/', ' / '), item: [] };
  routeGroup.sort((a, b) => beforeRead(a) - beforeRead(b));
  for (const route of routeGroup) {
    const key = `${route.method} ${route.template}`;
    let body = goodBodies[key] ?? null;
    if (route.bodyType && !(key in goodBodies)) throw new Error(`Missing request body for ${key} (${route.bodyType})`);
    const extra = captureByRoute[key] ? capture(captureByRoute[key]) : [];
    if (key === 'POST /purchase-orders/{id:guid}/lines') extra.push(`const lines = pm.response.json().lines; pm.collectionVariables.set('purchaseOrderLineId', lines[lines.length - 1].id);`);
    if (key === 'POST /sales/{id:guid}/lines') extra.push(`const lines = pm.response.json().lines; pm.collectionVariables.set('saleLineId', lines[lines.length - 1].id);`);
    if (key === 'POST /crm/loyalty/cards') extra.push(`pm.collectionVariables.set('cardNumber', pm.response.json().cardNumber);`);
    if (key === 'POST /variants/products/{productId:guid}') extra.push(
      `pm.collectionVariables.set('sku', pm.response.json().sku);`);
    if (key === 'PUT /variants/{id:guid}') extra.push(
      `pm.collectionVariables.set('barcode', pm.response.json().barcode);`);
    if (key === 'POST /work-sessions/clock-in') extra.push(
      `pm.collectionVariables.set('clockInAt', pm.response.json().clockInAt);`);
    if (key === 'POST /work-sessions/{id:guid}/clock-out') extra.push(
      `pm.collectionVariables.set('clockOutAt', pm.response.json().clockOutAt);`);
    const expected = route.method === 'GET' ? [200] : [200, 201, 204];
    const prereq = queryOrPathPrereqs(route);
    const targetFolder = route.template.endsWith('/deactivate') ? deactivations :
      key.startsWith('GET /inventory-counts/{id:guid}/') && file.includes('InventoryDocument')
        ? lateDocuments : deferredRoutes.has(key) ? deferred : folder;
    let requestUrl = urlFor(route);
    if (key === 'POST /stock-transfers/{id:guid}/cancel')
      requestUrl = requestUrl.replace(v('stockTransferId'), v('cancelStockTransferId'));
    if (key === 'POST /inventory-counts/{id:guid}/cancel')
      requestUrl = requestUrl.replace(v('inventoryCountId'), v('cancelInventoryCountId'));
    if (key === 'POST /purchase-orders/{id:guid}/cancel')
      requestUrl = requestUrl.replace(v('purchaseOrderId'), v('cancelPurchaseOrderId'));
    if (key === 'POST /sales/{id:guid}/cancel')
      requestUrl = requestUrl.replace(v('saleId'), v('cancelSaleId'));
    if (key === 'POST /auth/logout') body = { refreshToken: v('logoutRefreshToken') };
    if (key === 'POST /auth/change-password') body =
      { currentPassword: 'TempPassword123!', newPassword: 'ChangedPassword123!' };
    if (key === 'POST /auth/forgot-password' || key === 'POST /auth/reset-password')
      body = key.endsWith('forgot-password') ?
        { email: `user-${v('runId')}@example.test` } :
        { email: `user-${v('runId')}@example.test`, resetToken: v('resetToken'),
          newPassword: 'ResetPassword123!' };
    if (key === 'POST /auth/refresh') extra.push(
      `const data = pm.response.json(); pm.collectionVariables.set('logoutRefreshToken', data.refreshToken);`);
    if (key === 'POST /auth/reset-password') extra.push(
      `pm.collectionVariables.set('accessToken', pm.collectionVariables.get('adminAccessToken'));`);
    if (key === 'POST /auth/forgot-password') extra.push(
      `pm.collectionVariables.set('resetToken', pm.response.json().resetToken);`);
    if (key === 'GET /stock/variants/{variantId:guid}/total') extra.push(
      `const total = pm.response.json().quantityOnHand;`,
      `pm.test('Created variant has stock', () => { if (!(total > 0)) throw new Error('Expected positive stock'); });`);
    if (key === 'POST /auth/logout') {
      targetFolder.item.push(item('Invalid refresh token cannot log out another session', 'POST',
        '{{baseUrl}}/auth/logout', { refreshToken: 'invalid-token' },
        { status: [204], extra: [] }, false));
    }
    if (key.endsWith('/cancel')) {
      const cancelVar = key.includes('stock-transfers') ? 'cancelStockTransferId' :
        key.includes('inventory-counts') ? 'cancelInventoryCountId' :
          key.includes('purchase-orders') ? 'cancelPurchaseOrderId' : 'cancelSaleId';
      const createPath = route.template.split('/{id:guid}')[0];
      targetFolder.item.push(item(`Setup · draft for ${key}`, 'POST', `{{baseUrl}}${createPath}`,
        goodBodies[`POST ${createPath}`], { status: [201], extra: capture(cancelVar) }));
    }
    if (key === 'POST /auth/change-password') {
      targetFolder.item.push(item('Setup · login temporary user', 'POST', '{{baseUrl}}/auth/login',
        { email: `user-${v('runId')}@example.test`, password: 'TempPassword123!' },
        { status: [200], extra: [
          `pm.collectionVariables.set('adminAccessToken', pm.collectionVariables.get('accessToken'));`,
          `pm.collectionVariables.set('accessToken', pm.response.json().accessToken);`
        ] }, false));
    }
    targetFolder.item.push(item(`Happy · ${key}`, route.method, requestUrl, body,
      { status: expected, extra }, !publicRoutes.has(key), prereq));
    if (key === 'PUT /auth/me/language') {
      targetFolder.item.push(item('State · saved language', 'GET', '{{baseUrl}}/auth/me', null,
        { status: [200], extra: [
          `pm.test('Language preference persisted', () => pm.expect(pm.response.json().languageCode).to.eql('fr'));`
        ] }));
    }
    if (route.template.endsWith('/deactivate')) {
      const base = route.template.replace(/\/\{id:guid\}\/deactivate$/, '');
      const idVar = pathVariable('id', route);
      const byId = routeKeys.has(`GET ${base}/{id:guid}`);
      const readUrl = `{{baseUrl}}${base}${byId ? `/${v(idVar)}` : ''}`;
      targetFolder.item.push(item(`State · ${key}`, 'GET', readUrl, null,
        { status: [200], extra: [
          `const data = pm.response.json();`,
          `const id = pm.collectionVariables.get(${JSON.stringify(idVar)});`,
          `const rows = Array.isArray(data) ? data : (data.items ?? [data]);`,
          `const entity = rows.find(x => x.id === id);`,
          `pm.test('Record remains and is inactive', () => {`,
          `  if (!entity || entity.isActive !== false) throw new Error('Soft deactivation was not persisted');`,
          `});`
        ] }));
    }
    if (!publicRoutes.has(key)) {
      targetFolder.item.push(item(`Unauthorized · ${key}`, route.method, requestUrl, body,
        { status: [401], extra: [] }, false));
      if (!authenticatedWithoutPermission.has(key)) {
        targetFolder.item.push(item(`Forbidden · ${key}`, route.method, requestUrl, body,
          { status: [403], extra: [] }, true, [], 'limitedToken'));
      }
    }
    const invalid = invalidBody(route, body);
    if (invalid) targetFolder.item.push(item(`Validation · ${key}`, route.method,
      requestUrl, invalid, { status: [400], extra: [] }, !publicRoutes.has(key)));
    if (route.method === 'GET' && route.template.endsWith('/{id:guid}')) {
      const missingUrl = requestUrl.replace(/\{\{\w+Id\}\}(?=\?|$)/, v('newGuid'));
      if (missingUrl !== requestUrl) targetFolder.item.push(item(`Not found · ${key}`,
        'GET', missingUrl, null, { status: [404], extra: [] }));
    }
    if (route.method === 'GET' && route.signature.includes('int? pageSize')) {
      const invalidUrl = requestUrl + (requestUrl.includes('?') ? '&' : '?') + 'pageSize=0';
      targetFolder.item.push(item(`Pagination · ${key}`, 'GET', invalidUrl, null,
        { status: [400], extra: [] }));
    }
    if (key === 'POST /locations') {
      folder.item.push(item('Setup · destination location', 'POST', '{{baseUrl}}/locations',
        { ...goodBodies[key], name: unique('Warehouse'), code: 'W-' + v('runId'), type: 'Warehouse' },
        { status: [201], extra: capture('destinationLocationId') }));
    }
    if (key === 'POST /inventory-document-templates') {
      for (const [type, variable] of [
        ['CountSheet', 'countSheetTemplateId'],
        ['CountDifferences', 'differenceTemplateId']
      ]) {
        folder.item.push(item(`Setup · ${type} template`, 'POST', '{{baseUrl}}/inventory-document-templates',
          { ...goodBodies[key], name: unique(type), type,
            columns: type === 'CountSheet' ? ['Sku', 'Expected', 'Counted'] :
              ['Sku', 'Expected', 'Counted', 'Difference'] },
          { status: [201], extra: capture(variable) }));
      }
    }
    if (key === 'POST /attributes/{id:guid}/values') {
      folder.item.push(item('Setup · second attribute value', 'POST',
        `{{baseUrl}}/attributes/${v('attributeId')}/values`,
        { value: 'Linen', displayOrder: 3 },
        { status: [201], extra: capture('secondaryAttributeValueId') }));
    }
    if (key === 'POST /variants/products/{productId:guid}') {
      folder.item.push(item('Setup · second variant for count', 'POST',
        `{{baseUrl}}/variants/products/${v('productId')}`,
        { ...goodBodies[key], sku: 'SKU2-' + v('runId'),
          attributeValueIds: [v('secondaryAttributeValueId')] },
        { status: [201], extra: capture('secondaryVariantId') }));
    }
    if (key === 'PUT /inventory-counts/{id:guid}/lines/{productVariantId:guid}/count') {
      folder.item.push(item('Setup · count second variant', 'PUT',
        `{{baseUrl}}/inventory-counts/${v('inventoryCountId')}/lines/${v('secondaryVariantId')}/count`,
        { countedQuantity: 0 }, { status: [200], extra: [] }));
    }
    if (key === 'POST /auth/logout') {
      targetFolder.item.push(item('Logged-out refresh token is rejected', 'POST',
        '{{baseUrl}}/auth/refresh', { refreshToken: v('logoutRefreshToken') },
        { status: [401], extra: [] }, false));
    }
  }
  if (folder.item.length) sections.push(folder);
  if (file === 'Inventory/InventoryCountEndpoints.cs') sections.push(lateDocuments);
  if (file === 'Audit/AuditEndpoints.cs') {
    folder.item.push(item('State · stock adjustment was audited', 'GET',
      '{{baseUrl}}/audit/entries?action=StockAdjusted', null,
      { status: [200], extra: [
        `const data = pm.response.json();`,
        `pm.test('Stock adjustment audit exists', () => {`,
        `  if (!(data.totalItems > 0)) throw new Error('Missing stock adjustment audit');`,
        `});`
      ] }));
  }
}
const syncIndex = deferred.item.findIndex(x => x.name ===
  'Happy · POST /crm/loyalty/sales/{saleId:guid}/sync');
const redeemIndex = deferred.item.findIndex(x => x.name ===
  'Happy · POST /crm/loyalty/cards/{cardId:guid}/redemptions');
if (syncIndex > redeemIndex && redeemIndex >= 0) {
  const [sync, unauthenticated] = deferred.item.splice(syncIndex, 2);
  deferred.item.splice(redeemIndex, 0, sync, unauthenticated);
}
sections.push(deferred, deactivations);

const collection = {
  info: {
    _postman_id: crypto.randomUUID(),
    name: 'SimpliaDz API functional actions',
    description: 'Generated from all 187 mapped endpoint methods. Run only against a disposable local tenant. Each protected endpoint has authorized and unauthenticated requests; workflows also capture IDs for chained operations.',
    schema: 'https://schema.getpostman.com/json/collection/v2.1.0/collection.json'
  },
  event: [{ listen: 'prerequest', script: { type: 'text/javascript', exec: [
    `if (!pm.collectionVariables.get('runId')) pm.collectionVariables.set('runId', Date.now().toString(36));`,
    `pm.collectionVariables.set('newGuid', pm.variables.replaceIn('{{$guid}}'));`,
    `pm.collectionVariables.set('today', new Date().toISOString().slice(0, 10));`
  ] } }],
  item: sections,
  variable: [
    { key: 'runId', value: '' }, { key: 'accessToken', value: '' },
    { key: 'refreshToken', value: '' }, { key: 'newGuid', value: '' }
  ]
};

const environment = {
  id: crypto.randomUUID(), name: 'SimpliaDz Local API',
  values: [
    { key: 'baseUrl', value: 'http://localhost:5288', enabled: true },
    { key: 'adminEmail', value: 'admin@dev.local', enabled: true },
    { key: 'adminPassword', value: '', enabled: true, type: 'secret' }
  ],
  _postman_variable_scope: 'environment',
  _postman_exported_at: new Date().toISOString(),
  _postman_exported_using: 'SimpliaDz generator'
};

fs.writeFileSync(path.join(output, 'SimpliaDz.postman_collection.json'), JSON.stringify(collection, null, 2) + '\n');
fs.writeFileSync(path.join(output, 'SimpliaDz.local.postman_environment.json'), JSON.stringify(environment, null, 2) + '\n');
console.log(`Generated ${routeList.length} route workflows in ${output}`);
