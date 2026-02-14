<template>
  <el-dialog
    v-model="visible"
    title="Complete Your Profile"
    :width="800"
    :close-on-click-modal="false"
    :close-on-press-escape="false"
    class="profile-wizard-dialog"
    @close="handleClose"
  >
    <div class="wizard-container">
      <!-- Progress Steps -->
      <el-steps :active="currentStep" align-center finish-status="success">
        <el-step
          v-for="(step, index) in steps"
          :key="index"
          :title="step.title"
          :icon="step.icon"
        />
      </el-steps>

      <!-- Step Content -->
      <div class="step-content">
        <!-- Step 1: Basic Information -->
        <div v-show="currentStep === 0" class="step-panel">
          <div class="step-header">
            <el-icon :size="32" color="#409EFF"><office-building /></el-icon>
            <h3>Basic Company Information</h3>
            <p>Let's start with essential company details</p>
          </div>

          <el-form
            ref="step1FormRef"
            :model="formData"
            :rules="step1Rules"
            label-width="180px"
            label-position="left"
          >
            <el-form-item label="Company Name" prop="companyName" required>
              <el-input
                v-model="formData.companyName"
                placeholder="Enter full legal company name"
                clearable
              >
                <template #prefix>
                  <el-icon><office-building /></el-icon>
                </template>
              </el-input>
              <div class="field-hint">This should match your business registration</div>
            </el-form-item>

            <el-form-item label="Company ID / Registration #" prop="companyId" required>
              <el-input
                v-model="formData.companyId"
                placeholder="Business registration number"
                clearable
              >
                <template #prefix>
                  <el-icon><document /></el-icon>
                </template>
              </el-input>
              <div class="field-hint">Unique identifier from your business license</div>
            </el-form-item>

            <el-form-item
              label="Business Registration #"
              prop="businessRegistrationNumber"
              required
            >
              <el-input
                v-model="formData.businessRegistrationNumber"
                placeholder="Official registration number"
                clearable
              />
            </el-form-item>

            <el-form-item label="Category" prop="category" required>
              <el-select
                v-model="formData.category"
                placeholder="Select business category"
                clearable
                filterable
              >
                <el-option label="Manufacturing" value="manufacturing" />
                <el-option label="Services" value="services" />
                <el-option label="Trading" value="trading" />
                <el-option label="Technology" value="technology" />
                <el-option label="Construction" value="construction" />
                <el-option label="Logistics" value="logistics" />
                <el-option label="Consulting" value="consulting" />
                <el-option label="Other" value="other" />
              </el-select>
            </el-form-item>

            <el-form-item label="Region" prop="region" required>
              <el-select
                v-model="formData.region"
                placeholder="Select primary region"
                clearable
                filterable
              >
                <el-option label="North America" value="north_america" />
                <el-option label="Europe" value="europe" />
                <el-option label="Asia Pacific" value="asia_pacific" />
                <el-option label="China - Mainland" value="china_mainland" />
                <el-option label="China - Hong Kong" value="china_hongkong" />
                <el-option label="Middle East" value="middle_east" />
                <el-option label="Latin America" value="latin_america" />
                <el-option label="Africa" value="africa" />
              </el-select>
            </el-form-item>

            <el-form-item label="Company Address" prop="address" required>
              <el-input
                v-model="formData.address"
                type="textarea"
                :rows="3"
                placeholder="Full registered address"
              />
              <div class="field-hint">
                Include street, city, state/province, postal code, country
              </div>
            </el-form-item>
          </el-form>
        </div>

        <!-- Step 2: Contact Information -->
        <div v-show="currentStep === 1" class="step-panel">
          <div class="step-header">
            <el-icon :size="32" color="#67C23A"><user /></el-icon>
            <h3>Contact Information</h3>
            <p>How can we reach you?</p>
          </div>

          <el-form
            ref="step2FormRef"
            :model="formData"
            :rules="step2Rules"
            label-width="180px"
            label-position="left"
          >
            <el-form-item label="Contact Person" prop="contactPerson" required>
              <el-input
                v-model="formData.contactPerson"
                placeholder="Primary contact name"
                clearable
              >
                <template #prefix>
                  <el-icon><user /></el-icon>
                </template>
              </el-input>
            </el-form-item>

            <el-form-item label="Contact Phone" prop="contactPhone" required>
              <el-input v-model="formData.contactPhone" placeholder="+1 (555) 123-4567" clearable>
                <template #prefix>
                  <el-icon><phone /></el-icon>
                </template>
              </el-input>
              <div class="field-hint">Include country code</div>
            </el-form-item>

            <el-form-item label="Contact Email" prop="contactEmail" required>
              <el-input
                v-model="formData.contactEmail"
                type="email"
                placeholder="contact@company.com"
                clearable
              >
                <template #prefix>
                  <el-icon><message /></el-icon>
                </template>
              </el-input>
            </el-form-item>
          </el-form>

          <el-divider>Additional Contacts (Optional)</el-divider>

          <el-form :model="formData" label-width="180px" label-position="left">
            <el-collapse>
              <el-collapse-item title="Sales Contact" name="sales">
                <el-form-item label="Sales Contact Name">
                  <el-input v-model="formData.salesContactName" placeholder="Optional" clearable />
                </el-form-item>
                <el-form-item label="Sales Email">
                  <el-input v-model="formData.salesContactEmail" placeholder="Optional" clearable />
                </el-form-item>
                <el-form-item label="Sales Phone">
                  <el-input v-model="formData.salesContactPhone" placeholder="Optional" clearable />
                </el-form-item>
              </el-collapse-item>

              <el-collapse-item title="Finance Contact" name="finance">
                <el-form-item label="Finance Contact Name">
                  <el-input
                    v-model="formData.financeContactName"
                    placeholder="Optional"
                    clearable
                  />
                </el-form-item>
                <el-form-item label="Finance Email">
                  <el-input
                    v-model="formData.financeContactEmail"
                    placeholder="Optional"
                    clearable
                  />
                </el-form-item>
                <el-form-item label="Finance Phone">
                  <el-input
                    v-model="formData.financeContactPhone"
                    placeholder="Optional"
                    clearable
                  />
                </el-form-item>
              </el-collapse-item>
            </el-collapse>
          </el-form>
        </div>

        <!-- Step 3: Financial Information -->
        <div v-show="currentStep === 2" class="step-panel">
          <div class="step-header">
            <el-icon :size="32" color="#E6A23C"><wallet /></el-icon>
            <h3>Financial & Payment Details</h3>
            <p>Banking and payment information</p>
          </div>

          <el-form
            ref="step3FormRef"
            :model="formData"
            :rules="step3Rules"
            label-width="180px"
            label-position="left"
          >
            <el-form-item label="Bank Account" prop="bankAccount" required>
              <el-input
                v-model="formData.bankAccount"
                placeholder="Account number or IBAN"
                clearable
              >
                <template #prefix>
                  <el-icon><wallet /></el-icon>
                </template>
              </el-input>
            </el-form-item>

            <el-form-item label="Bank Name">
              <el-input v-model="formData.bankName" placeholder="Name of your bank" clearable />
            </el-form-item>

            <el-form-item label="Payment Terms" prop="paymentTerms" required>
              <el-select v-model="formData.paymentTerms" placeholder="Select payment terms">
                <el-option label="Net 30 days" value="net_30" />
                <el-option label="Net 60 days" value="net_60" />
                <el-option label="Net 90 days" value="net_90" />
                <el-option label="Upon delivery" value="upon_delivery" />
                <el-option label="Advance payment" value="advance" />
                <el-option label="Letter of Credit (L/C)" value="lc" />
                <el-option label="Other" value="other" />
              </el-select>
            </el-form-item>

            <el-form-item label="Payment Currency" prop="paymentCurrency" required>
              <el-select
                v-model="formData.paymentCurrency"
                placeholder="Select currency"
                filterable
              >
                <el-option label="USD - US Dollar" value="USD" />
                <el-option label="EUR - Euro" value="EUR" />
                <el-option label="GBP - British Pound" value="GBP" />
                <el-option label="CNY - Chinese Yuan" value="CNY" />
                <el-option label="JPY - Japanese Yen" value="JPY" />
                <el-option label="HKD - Hong Kong Dollar" value="HKD" />
                <el-option label="AUD - Australian Dollar" value="AUD" />
                <el-option label="CAD - Canadian Dollar" value="CAD" />
              </el-select>
            </el-form-item>
          </el-form>
        </div>

        <!-- Step 4: Review & Submit -->
        <div v-show="currentStep === 3" class="step-panel">
          <div class="step-header">
            <el-icon :size="32" color="#67C23A"><circle-check /></el-icon>
            <h3>Review Your Information</h3>
            <p>Please verify all details before submitting</p>
          </div>

          <div class="review-section">
            <el-descriptions :column="1" border>
              <el-descriptions-item label="Company Name">
                {{ formData.companyName || "-" }}
              </el-descriptions-item>
              <el-descriptions-item label="Company ID">
                {{ formData.companyId || "-" }}
              </el-descriptions-item>
              <el-descriptions-item label="Business Registration #">
                {{ formData.businessRegistrationNumber || "-" }}
              </el-descriptions-item>
              <el-descriptions-item label="Category">
                {{ formatCategory(formData.category) }}
              </el-descriptions-item>
              <el-descriptions-item label="Region">
                {{ formatRegion(formData.region) }}
              </el-descriptions-item>
              <el-descriptions-item label="Address">
                {{ formData.address || "-" }}
              </el-descriptions-item>
            </el-descriptions>

            <el-divider />

            <el-descriptions :column="1" border>
              <el-descriptions-item label="Contact Person">
                {{ formData.contactPerson || "-" }}
              </el-descriptions-item>
              <el-descriptions-item label="Contact Phone">
                {{ formData.contactPhone || "-" }}
              </el-descriptions-item>
              <el-descriptions-item label="Contact Email">
                {{ formData.contactEmail || "-" }}
              </el-descriptions-item>
            </el-descriptions>

            <el-divider />

            <el-descriptions :column="1" border>
              <el-descriptions-item label="Bank Account">
                {{ formData.bankAccount || "-" }}
              </el-descriptions-item>
              <el-descriptions-item label="Payment Terms">
                {{ formatPaymentTerms(formData.paymentTerms) }}
              </el-descriptions-item>
              <el-descriptions-item label="Payment Currency">
                {{ formData.paymentCurrency || "-" }}
              </el-descriptions-item>
            </el-descriptions>
          </div>

          <el-alert type="success" :closable="false" show-icon class="review-alert">
            <template #title> Almost done! Click "Complete Profile" to save. </template>
          </el-alert>
        </div>
      </div>

      <!-- Navigation Buttons -->
      <div class="wizard-footer">
        <div class="footer-left">
          <el-button v-if="currentStep > 0" @click="prevStep">
            <el-icon><arrow-left /></el-icon>
            Previous
          </el-button>
        </div>

        <div class="footer-center">
          <span class="step-indicator">Step {{ currentStep + 1 }} of {{ steps.length }}</span>
        </div>

        <div class="footer-right">
          <el-button @click="saveDraft" :loading="savingDraft">
            <el-icon><document /></el-icon>
            Save Draft
          </el-button>

          <el-button v-if="currentStep < steps.length - 1" type="primary" @click="nextStep">
            Next
            <el-icon><arrow-right /></el-icon>
          </el-button>

          <el-button v-else type="success" :loading="submitting" @click="submit">
            <el-icon><circle-check /></el-icon>
            Complete Profile
          </el-button>
        </div>
      </div>
    </div>
  </el-dialog>
</template>

<script setup lang="ts">



import { ref, reactive, computed, watch, onMounted } from "vue";
import { type FormInstance, type FormRules } from "element-plus";
import {
  OfficeBuilding,
  User,
  Wallet,
  CircleCheck,
  ArrowLeft,
  ArrowRight,
  Document,
  Phone,
  Message,
} from "@element-plus/icons-vue";
import { useService } from "@/core/hooks";
import { useNotification } from "@/composables";
const notification = useNotification();

interface Props {
  modelValue: boolean;
  supplierId?: number;
  initialData?: Record<string, any>;
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: false,
  supplierId: undefined,
  initialData: () => ({}),
});

const emit = defineEmits<{
  (e: "update:modelValue", value: boolean): void;
  (e: "complete", data: Record<string, any>): void;
  (e: "save-draft", data: Record<string, any>): void;
}>();

const visible = computed({
  get: () => props.modelValue,
  set: (val) => emit("update:modelValue", val),
});

const currentStep = ref(0);
const submitting = ref(false);
const savingDraft = ref(false);

const step1FormRef = ref<FormInstance>();
const step2FormRef = ref<FormInstance>();
const step3FormRef = ref<FormInstance>();

const steps = [
  { title: "Basic Info", icon: OfficeBuilding },
  { title: "Contact", icon: User },
  { title: "Financial", icon: Wallet },
  { title: "Review", icon: CircleCheck },
];

const formData = reactive({
  companyName: "",
  companyId: "",
  businessRegistrationNumber: "",
  category: "",
  region: "",
  address: "",
  contactPerson: "",
  contactPhone: "",
  contactEmail: "",
  bankAccount: "",
  bankName: "",
  paymentTerms: "",
  paymentCurrency: "",
  salesContactName: "",
  salesContactEmail: "",
  salesContactPhone: "",
  financeContactName: "",
  financeContactEmail: "",
  financeContactPhone: "",
});

const step1Rules: FormRules = {
  companyName: [
    { required: true, message: "Company name is required", trigger: "blur" },
    { min: 2, max: 200, message: "Length should be 2 to 200 characters", trigger: "blur" },
  ],
  companyId: [{ required: true, message: "Company ID is required", trigger: "blur" }],
  businessRegistrationNumber: [
    { required: true, message: "Business registration number is required", trigger: "blur" },
  ],
  category: [{ required: true, message: "Please select a category", trigger: "change" }],
  region: [{ required: true, message: "Please select a region", trigger: "change" }],
  address: [
    { required: true, message: "Address is required", trigger: "blur" },
    { min: 10, message: "Please provide a complete address", trigger: "blur" },
  ],
};

const step2Rules: FormRules = {
  contactPerson: [{ required: true, message: "Contact person is required", trigger: "blur" }],
  contactPhone: [
    { required: true, message: "Contact phone is required", trigger: "blur" },
    { pattern: /^[\d\s\-\+\(\)]+$/, message: "Invalid phone format", trigger: "blur" },
  ],
  contactEmail: [
    { required: true, message: "Contact email is required", trigger: "blur" },
    { type: "email", message: "Invalid email format", trigger: "blur" },
  ],
};

const step3Rules: FormRules = {
  bankAccount: [{ required: true, message: "Bank account is required", trigger: "blur" }],
  paymentTerms: [{ required: true, message: "Please select payment terms", trigger: "change" }],
  paymentCurrency: [
    { required: true, message: "Please select payment currency", trigger: "change" },
  ],
};

const formatCategory = (value: string): string => {
  const map: Record<string, string> = {
    manufacturing: "Manufacturing",
    services: "Services",
    trading: "Trading",
    technology: "Technology",
    construction: "Construction",
    logistics: "Logistics",
    consulting: "Consulting",
    other: "Other",
  };
  return map[value] || value || "-";
};

const formatRegion = (value: string): string => {
  const map: Record<string, string> = {
    north_america: "North America",
    europe: "Europe",
    asia_pacific: "Asia Pacific",
    china_mainland: "China - Mainland",
    china_hongkong: "China - Hong Kong",
    middle_east: "Middle East",
    latin_america: "Latin America",
    africa: "Africa",
  };
  return map[value] || value || "-";
};

const formatPaymentTerms = (value: string): string => {
  const map: Record<string, string> = {
    net_30: "Net 30 days",
    net_60: "Net 60 days",
    net_90: "Net 90 days",
    upon_delivery: "Upon delivery",
    advance: "Advance payment",
    lc: "Letter of Credit (L/C)",
    other: "Other",
  };
  return map[value] || value || "-";
};

const validateCurrentStep = async (): Promise<boolean> => {
  try {
    if (currentStep.value === 0 && step1FormRef.value) {
      await step1FormRef.value.validate();
    } else if (currentStep.value === 1 && step2FormRef.value) {
      await step2FormRef.value.validate();
    } else if (currentStep.value === 2 && step3FormRef.value) {
      await step3FormRef.value.validate();
    }
    return true;
  } catch (error) {
    notification.warning("Please fill in all required fields");
    return false;
  }
};

const nextStep = async () => {
  const isValid = await validateCurrentStep();
  if (isValid && currentStep.value < steps.length - 1) {
    currentStep.value++;
  }
};

const prevStep = () => {
  if (currentStep.value > 0) {
    currentStep.value--;
  }
};

const saveDraft = async () => {
  savingDraft.value = true;
  try {
    emit("save-draft", { ...formData });
    notification.success("Draft saved successfully");
  } catch (error) {
    notification.error("Failed to save draft");
  } finally {
    savingDraft.value = false;
  }
};

const submit = async () => {
  const isValid = await validateCurrentStep();
  if (!isValid) return;

  try {
    await notification.confirm(
      "Are you sure you want to submit this profile?",
      "Confirm Submission",
      {
        confirmButtonText: "Submit",
        cancelButtonText: "Cancel",
        type: "info",
      },
    );

    submitting.value = true;
    emit("complete", { ...formData });
  } catch (error) {
    if (error !== "cancel") {
      console.error("Submission error:", error);
    }
  } finally {
    submitting.value = false;
  }
};

const handleClose = () => {
  if (currentStep.value > 0) {
    notification.confirm(
      "Your progress will be lost. Save as draft before closing?",
      "Unsaved Changes",
      {
        confirmButtonText: "Save Draft",
        cancelButtonText: "Discard",
        distinguishCancelAndClose: true,
        type: "warning",
      },
    )
      .then(() => {
        saveDraft();
        visible.value = false;
      })
      .catch((action) => {
        if (action === "cancel") {
          visible.value = false;
        }
      });
  } else {
    visible.value = false;
  }
};

// Load initial data when dialog opens
watch(visible, (newVal) => {
  if (newVal && props.initialData) {
    Object.assign(formData, props.initialData);
  }
});

onMounted(() => {
  if (props.initialData) {
    Object.assign(formData, props.initialData);
  }
});



</script>

<style scoped>
.profile-wizard-dialog :deep(.el-dialog__body) {
  padding: 0;
}

.wizard-container {
  padding: 24px;
}

.el-steps {
  margin-bottom: 32px;
}

.step-content {
  min-height: 400px;
  margin-bottom: 24px;
}

.step-panel {
  animation: fadeIn 0.3s ease;
}

@keyframes fadeIn {
  from {
    opacity: 0;
    transform: translateY(10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.step-header {
  text-align: center;
  margin-bottom: 32px;
}

.step-header h3 {
  margin: 12px 0 8px;
  font-size: 20px;
  font-weight: 600;
  color: #303133;
}

.step-header p {
  margin: 0;
  font-size: 14px;
  color: #909399;
}

.field-hint {
  font-size: 12px;
  color: #909399;
  margin-top: 4px;
  font-style: italic;
}

.review-section {
  max-height: 450px;
  overflow-y: auto;
}

.review-alert {
  margin-top: 24px;
}

.wizard-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 24px;
  border-top: 1px solid #ebeef5;
  background: #f5f7fa;
  margin: 0 -24px -24px;
}

.footer-left,
.footer-right {
  display: flex;
  gap: 8px;
}

.footer-center {
  flex: 1;
  text-align: center;
}

.step-indicator {
  font-size: 14px;
  color: #606266;
  font-weight: 500;
}

@media (max-width: 768px) {
  .wizard-footer {
    flex-direction: column;
    gap: 12px;
  }

  .footer-left,
  .footer-center,
  .footer-right {
    width: 100%;
    justify-content: center;
  }
}
</style>
