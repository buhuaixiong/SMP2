<template>
  <el-dialog
    v-model="dialogVisible"
    title="Submit Profile Updates"
    width="900px"
    :before-close="handleClose"
  >
    <el-alert title="Important" type="warning" :closable="false" style="margin-bottom: 20px">
      <p>
        Updates to mandatory fields will start the full approval workflow (Purchaser → Quality →
        Procurement Manager → Procurement Director → Finance Director).
      </p>
      <p>Updates to optional fields only require confirmation from the assigned purchaser.</p>
      <p style="margin-top: 8px; color: #909399; font-size: 13px">
        Changes take effect only after approval. If any step rejects the request, please resubmit.
      </p>
    </el-alert>

    <el-form ref="formRef" :model="formData" label-width="160px">
      <el-divider content-position="left">Core Information</el-divider>

      <el-row :gutter="20">
        <el-col :span="12">
          <el-form-item label="Company Name" prop="companyName">
            <el-input
              v-model="formData.companyName"
              placeholder="Enter company name"
              :readonly="isReadOnly"
            />
            <div v-if="!isReadOnly && hasChanged('companyName')" class="field-hint">
              <el-icon color="#e6a23c"><WarnTriangleFilled /></el-icon>
              <span>Current: {{ currentData.companyName || "Not provided" }}</span>
            </div>
          </el-form-item>
        </el-col>

        <el-col :span="12">
          <el-form-item label="Supplier Code" prop="companyId">
            <el-input
              v-model="formData.companyId"
              placeholder="Enter supplier code"
              :readonly="isReadOnly"
            />
            <div v-if="!isReadOnly && hasChanged('companyId')" class="field-hint">
              <el-icon color="#e6a23c"><WarnTriangleFilled /></el-icon>
              <span>Current: {{ currentData.companyId || "Not provided" }}</span>
            </div>
          </el-form-item>
        </el-col>
      </el-row>

      <el-row :gutter="20">
        <el-col :span="12">
          <el-form-item label="Primary Contact" prop="contactPerson">
            <el-input
              v-model="formData.contactPerson"
              placeholder="Enter primary contact"
              :readonly="isReadOnly"
            />
            <div v-if="!isReadOnly && hasChanged('contactPerson')" class="field-hint">
              <el-icon color="#e6a23c"><WarnTriangleFilled /></el-icon>
              <span>Current: {{ currentData.contactPerson || "Not provided" }}</span>
            </div>
          </el-form-item>
        </el-col>

        <el-col :span="12">
          <el-form-item label="Contact Phone" prop="contactPhone">
            <el-input
              v-model="formData.contactPhone"
              placeholder="Enter contact phone"
              :readonly="isReadOnly"
            />
            <div v-if="!isReadOnly && hasChanged('contactPhone')" class="field-hint">
              <el-icon color="#e6a23c"><WarnTriangleFilled /></el-icon>
              <span>Current: {{ currentData.contactPhone || "Not provided" }}</span>
            </div>
          </el-form-item>
        </el-col>
      </el-row>

      <el-row :gutter="20">
        <el-col :span="12">
          <el-form-item label="Contact Email" prop="contactEmail">
            <el-input
              v-model="formData.contactEmail"
              placeholder="Enter contact email"
              :readonly="isReadOnly"
            />
            <div v-if="!isReadOnly && hasChanged('contactEmail')" class="field-hint">
              <el-icon color="#e6a23c"><WarnTriangleFilled /></el-icon>
              <span>Current: {{ currentData.contactEmail || "Not provided" }}</span>
            </div>
          </el-form-item>
        </el-col>

        <el-col :span="12">
          <el-form-item label="Supplier Category" prop="category">
            <el-input
              v-model="formData.category"
              placeholder="Enter supplier category"
              :readonly="isReadOnly"
            />
            <div v-if="!isReadOnly && hasChanged('category')" class="field-hint">
              <el-icon color="#e6a23c"><WarnTriangleFilled /></el-icon>
              <span>Current: {{ currentData.category || "Not provided" }}</span>
            </div>
          </el-form-item>
        </el-col>
      </el-row>

      <el-form-item label="Registered Address" prop="address">
        <el-input
          v-model="formData.address"
          type="textarea"
          :rows="2"
          placeholder="Enter registered address"
          :readonly="isReadOnly"
        />
        <div v-if="!isReadOnly && hasChanged('address')" class="field-hint">
          <el-icon color="#e6a23c"><WarnTriangleFilled /></el-icon>
          <span>Current: {{ currentData.address || "Not provided" }}</span>
        </div>
      </el-form-item>

      <el-divider content-position="left">Company Information</el-divider>

      <el-row :gutter="20">
        <el-col :span="12">
          <el-form-item label="Business Registration No." prop="businessRegistrationNumber">
            <el-input
              v-model="formData.businessRegistrationNumber"
              placeholder="Enter registration number"
              :readonly="isReadOnly"
            />
            <div v-if="!isReadOnly && hasChanged('businessRegistrationNumber')" class="field-hint">
              <el-icon color="#e6a23c"><WarnTriangleFilled /></el-icon>
              <span>Current: {{ currentData.businessRegistrationNumber || "Not provided" }}</span>
            </div>
          </el-form-item>
        </el-col>

        <el-col :span="12">
          <el-form-item label="Region" prop="region">
            <el-input v-model="formData.region" placeholder="Enter region" :readonly="isReadOnly" />
            <div v-if="!isReadOnly && hasChanged('region')" class="field-hint">
              <el-icon color="#e6a23c"><WarnTriangleFilled /></el-icon>
              <span>Current: {{ currentData.region || "Not provided" }}</span>
            </div>
          </el-form-item>
        </el-col>
      </el-row>

      <el-divider content-position="left">Financial Information</el-divider>

      <el-row :gutter="20">
        <el-col :span="12">
          <el-form-item label="Payment Terms" prop="paymentTerms">
            <el-input
              v-model="formData.paymentTerms"
              placeholder="Enter payment terms"
              :readonly="isReadOnly"
            />
            <div v-if="!isReadOnly && hasChanged('paymentTerms')" class="field-hint">
              <el-icon color="#e6a23c"><WarnTriangleFilled /></el-icon>
              <span>Current: {{ currentData.paymentTerms || "Not provided" }}</span>
            </div>
          </el-form-item>
        </el-col>

        <el-col :span="12">
          <el-form-item label="Payment Currency" prop="paymentCurrency">
            <el-input
              v-model="formData.paymentCurrency"
              placeholder="Enter payment currency"
              :readonly="isReadOnly"
            />
            <div v-if="!isReadOnly && hasChanged('paymentCurrency')" class="field-hint">
              <el-icon color="#e6a23c"><WarnTriangleFilled /></el-icon>
              <span>Current: {{ currentData.paymentCurrency || "Not provided" }}</span>
            </div>
          </el-form-item>
        </el-col>
      </el-row>

      <el-form-item label="Bank Account" prop="bankAccount">
        <el-input
          v-model="formData.bankAccount"
          placeholder="Enter bank account"
          :readonly="isReadOnly"
        />
        <div v-if="!isReadOnly && hasChanged('bankAccount')" class="field-hint">
          <el-icon color="#e6a23c"><WarnTriangleFilled /></el-icon>
          <span>Current: {{ currentData.bankAccount || "Not provided" }}</span>
        </div>
      </el-form-item>

      <el-divider content-position="left">Optional Information</el-divider>

      <el-row :gutter="20">
        <el-col :span="12">
          <el-form-item label="Service Category" prop="serviceCategory">
            <el-input
              v-model="formData.serviceCategory"
              placeholder="Enter service category"
              :readonly="isReadOnly"
            />
            <div v-if="!isReadOnly && hasChanged('serviceCategory')" class="field-hint">
              <el-icon color="#e6a23c"><WarnTriangleFilled /></el-icon>
              <span>Current: {{ currentData.serviceCategory || "Not provided" }}</span>
            </div>
          </el-form-item>
        </el-col>

        <el-col :span="12">
          <el-form-item label="Finance Contact" prop="financialContact">
            <el-input
              v-model="formData.financialContact"
              placeholder="Enter finance contact"
              :readonly="isReadOnly"
            />
            <div v-if="!isReadOnly && hasChanged('financialContact')" class="field-hint">
              <el-icon color="#e6a23c"><WarnTriangleFilled /></el-icon>
              <span>Current: {{ currentData.financialContact || "Not provided" }}</span>
            </div>
          </el-form-item>
        </el-col>
      </el-row>

      <el-form-item label="Notes" prop="notes">
        <el-input
          v-model="formData.notes"
          type="textarea"
          :rows="3"
          placeholder="Enter notes"
          :readonly="isReadOnly"
        />
        <div v-if="!isReadOnly && hasChanged('notes')" class="field-hint">
          <el-icon color="#e6a23c"><WarnTriangleFilled /></el-icon>
          <span>Current: {{ currentData.notes || "Not provided" }}</span>
        </div>
      </el-form-item>

      <el-divider v-if="changedFieldsCount > 0" content-position="left">Change Summary</el-divider>
      <el-alert
        v-if="changedFieldsCount > 0"
        type="info"
        :closable="false"
        style="margin-bottom: 20px"
      >
        <p>
          You updated <strong>{{ changedFieldsCount }}</strong> field(s).
          <span v-if="changedRequiredCount > 0">
            <strong>{{ changedRequiredCount }}</strong> mandatory field(s) will follow the approval
            workflow.
          </span>
          <span v-if="changedOptionalCount > 0">
            <strong>{{ changedOptionalCount }}</strong> optional field(s) require purchaser
            confirmation only.
          </span>
        </p>
        <ul style="margin: 8px 0; padding-left: 20px">
          <li v-for="field in changedFieldsList" :key="field.key">
            <span :class="['change-tag', field.category]">
              {{ field.category === "required" ? "Mandatory" : "Optional" }}
            </span>
            {{ field.label }}:
            <span style="color: #909399">{{ field.oldValue || "Not provided" }}</span>
            →
            <span style="color: #409eff; font-weight: 600">{{ field.newValue }}</span>
          </li>
        </ul>
      </el-alert>
    </el-form>

    <template #footer>
      <span class="dialog-footer">
        <el-button @click="handleClose">Cancel</el-button>
        <el-button
          v-if="!isReadOnly"
          type="primary"
          :loading="submitting"
          :disabled="changedFieldsCount === 0"
          @click="handleSubmit"
        >
          Submit Update
        </el-button>
      </span>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">




import { computed, reactive, ref, watch } from "vue";

import { WarnTriangleFilled } from "@element-plus/icons-vue";
import { submitChangeRequest } from "@/api/changeRequests";
import type { ChangeRequestResult } from "@/api/changeRequests";


import { useNotification } from "@/composables";

const notification = useNotification();
type SupplierData = {
  companyName?: string;
  companyId?: string;
  contactPerson?: string;
  contactPhone?: string;
  contactEmail?: string;
  category?: string;
  address?: string;
  businessRegistrationNumber?: string;
  paymentTerms?: string;
  paymentCurrency?: string;
  bankAccount?: string;
  region?: string;
  serviceCategory?: string;
  financialContact?: string;
  notes?: string;
};

const props = defineProps<{
  visible: boolean;
  supplierId: number;
  currentData: SupplierData;
  readonly?: boolean;
}>();

const emit = defineEmits<{
  (e: "update:visible", value: boolean): void;
  (e: "success", result: ChangeRequestResult): void;
}>();

const dialogVisible = computed({
  get: () => props.visible,
  set: (value) => emit("update:visible", value),
});

const isReadOnly = computed(() => props.readonly || false);

const formRef = ref();
const submitting = ref(false);

const formData = reactive<SupplierData>({
  companyName: "",
  companyId: "",
  contactPerson: "",
  contactPhone: "",
  contactEmail: "",
  category: "",
  address: "",
  businessRegistrationNumber: "",
  paymentTerms: "",
  paymentCurrency: "",
  bankAccount: "",
  region: "",
  serviceCategory: "",
  financialContact: "",
  notes: "",
});

const requiredFields = [
  { key: "companyName", label: "Company Name" },
  { key: "companyId", label: "Supplier Code" },
  { key: "contactPerson", label: "Primary Contact" },
  { key: "contactPhone", label: "Contact Phone" },
  { key: "contactEmail", label: "Contact Email" },
  { key: "category", label: "Supplier Category" },
  { key: "address", label: "Registered Address" },
  { key: "businessRegistrationNumber", label: "Business Registration No." },
  { key: "paymentTerms", label: "Payment Terms" },
  { key: "paymentCurrency", label: "Payment Currency" },
  { key: "bankAccount", label: "Bank Account" },
  { key: "region", label: "Region" },
] as const;

const optionalFields = [
  { key: "serviceCategory", label: "Service Category" },
  { key: "financialContact", label: "Finance Contact" },
  { key: "notes", label: "Notes" },
] as const;

const hasChanged = (fieldKey: string) =>
  formData[fieldKey as keyof SupplierData] !== props.currentData[fieldKey as keyof SupplierData];

const mapFieldChange = (
  items: readonly { key: string; label: string }[],
  category: "required" | "optional",
) =>
  items
    .filter((field) => hasChanged(field.key))
    .map((field) => ({
      key: field.key,
      label: field.label,
      oldValue: props.currentData[field.key as keyof SupplierData] || "",
      newValue: formData[field.key as keyof SupplierData] || "",
      category,
    }));

const changedRequiredFields = computed(() => mapFieldChange(requiredFields, "required"));
const changedOptionalFields = computed(() => mapFieldChange(optionalFields, "optional"));

const changedFieldsList = computed(() => [
  ...changedRequiredFields.value,
  ...changedOptionalFields.value,
]);

const changedRequiredCount = computed(() => changedRequiredFields.value.length);
const changedOptionalCount = computed(() => changedOptionalFields.value.length);
const changedFieldsCount = computed(() => changedRequiredCount.value + changedOptionalCount.value);

watch(
  () => props.currentData,
  (newData) => {
    if (newData) {
      Object.assign(formData, newData);
    }
  },
  { immediate: true, deep: true },
);

const handleClose = () => {
  dialogVisible.value = false;
};

const handleSubmit = async () => {
  if (changedFieldsCount.value === 0) {
    notification.warning("No changes detected");
    return;
  }

  try {
    submitting.value = true;

    const changes: Record<string, any> = {};
    changedFieldsList.value.forEach((field) => {
      changes[field.key] = formData[field.key as keyof SupplierData] ?? null;
    });

    const result = await submitChangeRequest(props.supplierId, changes);
    const fallback =
      result.flow === "optional"
        ? "Update submitted and pending purchaser confirmation."
        : "Change request submitted and awaiting approvals.";

    notification.success({
      message: result.message ?? fallback,
      duration: 3000,
    });

    emit("success", result);
    dialogVisible.value = false;
  } catch (error: any) {
    console.error("Failed to submit change request:", error);
    notification.error(error?.message || "Submission failed, please try again later");
  } finally {
    submitting.value = false;
  }
};




</script>

<style scoped>
.field-hint {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-top: 4px;
  font-size: 12px;
  color: #909399;
}

.field-hint .el-icon {
  flex-shrink: 0;
}

.change-tag {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 2px 6px;
  border-radius: 4px;
  font-size: 12px;
  font-weight: 500;
  margin-right: 6px;
  color: #ffffff;
}

.change-tag.required {
  background-color: #f56c6c;
}

.change-tag.optional {
  background-color: #409eff;
}

.dialog-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}
</style>
