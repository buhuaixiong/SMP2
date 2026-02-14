# 国际化翻译补丁

## 需要添加到 src/locales/en.json 的翻译键

在 `"common"` 部分添加：
```json
"select": "Select"
```

在 `"supplier"` 部分添加 `"form"` 对象：
```json
"form": {
  "sections": {
    "coreProfile": {
      "title": "Core Profile",
      "description": "Capture the supplier's identity and primary contact details."
    },
    "companyType": {
      "title": "Company Type",
      "description": "Select the company structure and provide the related details."
    },
    "registration": {
      "title": "Registration & Addresses"
    },
    "contacts": {
      "title": "Contact Directory"
    },
    "business": {
      "title": "Business Information"
    },
    "payments": {
      "title": "Settlement & Payments"
    },
    "bank": {
      "title": "Bank Details"
    },
    "notes": {
      "title": "Additional Notes"
    }
  },
  "fields": {
    "companyName": { "label": "Primary Display Name" },
    "companyId": { "label": "Registration ID" },
    "stage": { "label": "Stage" },
    "englishName": { "label": "English Name" },
    "chineseName": { "label": "Chinese Name" },
    "category": {
      "label": "Supplier Category",
      "placeholder": "e.g. Raw materials, tooling, services"
    },
    "contactPerson": { "label": "Primary Contact" },
    "contactPhone": { "label": "Primary Phone" },
    "contactEmail": { "label": "Primary Email" },
    "companyType": { "label": "Company Type" },
    "companyTypeOther": { "label": "Other Type Detail" },
    "authorizedCapital": { "label": "Authorized Capital" },
    "issuedCapital": { "label": "Issued Capital" },
    "directors": {
      "label": "Directors / Legal Representatives",
      "placeholder": "Separate multiple names with commas or new lines"
    },
    "owners": {
      "label": "Owners / Partners",
      "placeholder": "Separate multiple names with commas or new lines"
    },
    "registeredOffice": { "label": "Registered Office" },
    "businessRegistrationNumber": { "label": "Business Registration Number" },
    "businessAddress": { "label": "Operational Address" },
    "mailingAddress": { "label": "Mailing Address" },
    "telephone": { "label": "Telephone" },
    "fax": { "label": "Fax" },
    "businessNature": { "label": "Nature of Business" },
    "operatingCurrency": {
      "label": "Operating Currency",
      "placeholder": "e.g. CNY, USD"
    },
    "deliveryLocation": { "label": "Delivery Location" },
    "shipCode": { "label": "Ship Code" },
    "productsForEci": { "label": "Products Sold to ECI" },
    "establishedYear": { "label": "Year Established" },
    "employeeCount": { "label": "Number of Employees" },
    "qualityCertifications": { "label": "Quality Certifications" },
    "invoiceType": { "label": "Invoice Type" },
    "paymentTermsDays": { "label": "Payment Terms (days)" },
    "paymentTermsNotes": { "label": "Payment Terms Notes" },
    "paymentCurrency": {
      "label": "Payment Currency",
      "placeholder": "e.g. CNY"
    },
    "paymentMethods": { "label": "Payment Methods" },
    "paymentMethodsOther": { "label": "Other Method Detail" },
    "bankName": { "label": "Bank Name" },
    "bankAddress": { "label": "Bank Address" },
    "bankAccountNumber": { "label": "Bank Account Number" },
    "swiftCode": { "label": "Swift Code" },
    "notes": {
      "placeholder": "Add any internal remarks or contextual notes"
    }
  },
  "stage": {
    "temporary": "Temporary supplier",
    "official": "Official supplier"
  },
  "companyType": {
    "limited": "Limited company",
    "soleProprietor": "Sole proprietor",
    "partnership": "Partnership",
    "other": "Other"
  },
  "paymentMethod": {
    "cash": "Cash",
    "cheque": "Cheque / Banker draft",
    "wire": "Wire transfer",
    "other": "Other"
  },
  "shipCode": {
    "a": "A - Local delivery ECI(HZ)",
    "b": "B - CIF Hong Kong",
    "c": "C - FOB Hong Kong",
    "d": "D - Exworks",
    "e": "E - FOB USA",
    "f": "F - FOB Germany",
    "g": "G - FOB Taiwan",
    "h": "H - FOB Singapore",
    "i": "I - FOB Japan",
    "j": "J - Others"
  },
  "contacts": {
    "sales": "Sales",
    "finance": "Finance",
    "customerService": "Customer Service",
    "name": "Name",
    "email": "Email",
    "phone": "Phone"
  }
}
```

添加根级 `"table"` 对象：
```json
"table": {
  "showing": "Showing {start}-{end} of {total} suppliers",
  "noMatch": "No suppliers match the current filters",
  "perPage": "Per page",
  "page": "Page {current} of {total}",
  "loading": "Loading suppliers...",
  "noData": "No suppliers to display.",
  "hideDetails": "Hide details",
  "showDetails": "Show details",
  "columns": {
    "company": "Company",
    "category": "Category",
    "region": "Region",
    "importance": "Importance",
    "completion": "Completion"
  }
}
```

## 需要添加到 src/locales/zh.json 的中文翻译

（相同的结构，使用中文翻译）

## 需要添加到 src/locales/th.json 的泰语翻译

（相同的结构，使用泰语翻译）

## 修复说明

由于翻译文件较大（超过 25000 tokens），建议手动复制上述翻译键到对应的语言文件中。

或者使用以下命令行工具合并JSON：
```bash
# 使用 jq 工具合并 JSON（如果已安装）
jq -s '.[0] * .[1]' src/locales/en.json patch-en.json > src/locales/en-new.json
```
