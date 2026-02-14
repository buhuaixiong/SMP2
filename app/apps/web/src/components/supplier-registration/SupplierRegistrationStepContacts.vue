<template>
  <section class="wizard-step">
    <h2 class="step-title">{{ title }}</h2>
    <p class="step-description">{{ description }}</p>
    <div class="form-grid">
      <label>
        <span>{{ t("supplier.fields.address") }} *</span>
        <input v-model.trim="form.registeredOffice" type="text" required />
      </label>
      <label>
        <span>
          {{
            translateOr(
              "supplierRegistration.fields.businessAddress",
              "Operating Address",
            )
          }}
          *
        </span>
        <input v-model.trim="form.businessAddress" type="text" required />
      </label>
      <label>
        <span>{{ t("supplier.fields.businessRegistrationNumber") }} *</span>
        <input
          v-model.trim="form.businessRegistrationNumber"
          type="text"
          required
          @blur="validateCreditCode"
          :class="{ 'error-input': creditCodeError }"
        />
        <span v-if="creditCodeError" class="error-message">{{ creditCodeError }}</span>
      </label>
      <label>
        <span>{{ t("supplier.fields.contactPerson") }} *</span>
        <input v-model.trim="form.contactName" type="text" required />
      </label>
      <label>
        <span>{{ t("supplier.fields.contactEmail") }} *</span>
        <input
          v-model.trim="form.contactEmail"
          type="email"
          required
          @blur="validateContactEmail"
          :class="{ 'error-input': emailError }"
        />
        <span v-if="emailError" class="error-message">{{ emailError }}</span>
      </label>
      <label>
        <span>
          {{
            translateOr(
              "supplierRegistration.fields.procurementEmail",
              "Procurement Email",
            )
          }}
          *
        </span>
        <input
          v-model.trim="form.procurementEmail"
          type="email"
          required
          :class="{ 'error-input': procurementEmailError }"
        />
        <span v-if="procurementEmailError" class="error-message">{{ procurementEmailError }}</span>
      </label>
      <label>
        <span>{{ t("supplier.fields.contactPhone") }} *</span>
        <input v-model.trim="form.contactPhone" type="tel" required />
      </label>
      <label>
        <span>
          {{
            translateOr(
              "supplierRegistration.fields.financeContact",
              "Finance Contact",
            )
          }}
          *
          <span
            v-if="isCategoryA('financeContactName')"
            class="category-badge"
            :title="categoryBadgeTooltip"
          >
            {{ categoryBadgeLabel }}
          </span>
        </span>
        <input v-model.trim="form.financeContactName" type="text" required />
      </label>
      <label>
        <span>
          {{
            translateOr(
              "supplierRegistration.fields.financeContactEmail",
              "Finance Contact Email",
            )
          }}
          <span
            v-if="isCategoryA('financeContactEmail')"
            class="category-badge"
            :title="categoryBadgeTooltip"
          >
            {{ categoryBadgeLabel }}
          </span>
        </span>
        <input v-model.trim="form.financeContactEmail" type="email" />
      </label>
      <label>
        <span>
          {{
            translateOr(
              "supplierRegistration.fields.financeContactPhone",
              "Finance Contact Phone",
            )
          }}
          *
          <span
            v-if="isCategoryA('financeContactPhone')"
            class="category-badge"
            :title="categoryBadgeTooltip"
          >
            {{ categoryBadgeLabel }}
          </span>
        </span>
        <input v-model.trim="form.financeContactPhone" type="tel" required />
      </label>
    </div>
  </section>
</template>

<script setup lang="ts">
import { useI18n } from "vue-i18n";
import type { SupplierRegistrationFormState } from "./formState";

const { t } = useI18n();

defineProps<{
  title: string;
  description: string;
  form: SupplierRegistrationFormState;
  translateOr: (key: string, fallback: string) => string;
  isCategoryA: (field: keyof SupplierRegistrationFormState) => boolean;
  categoryBadgeLabel: string;
  categoryBadgeTooltip: string;
  creditCodeError: string;
  emailError: string;
  procurementEmailError: string;
  validateCreditCode: () => void;
  validateContactEmail: () => void;
}>();
</script>
