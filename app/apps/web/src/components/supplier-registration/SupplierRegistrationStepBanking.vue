<template>
  <section class="wizard-step">
    <h2 class="step-title">{{ title }}</h2>
    <p class="step-description">{{ description }}</p>
    <div class="form-grid">
      <label>
        <span>
          {{
            translateOr(
              "supplierRegistration.fields.bankName",
              "Bank Name",
            )
          }}
          *
          <span
            v-if="isCategoryA('bankName')"
            class="category-badge"
            :title="categoryBadgeTooltip"
          >
            {{ categoryBadgeLabel }}
          </span>
        </span>
        <input v-model.trim="form.bankName" type="text" required />
      </label>
      <label>
        <span>
          {{
            translateOr(
              "supplierRegistration.fields.bankAddress",
              "Bank Address",
            )
          }}
          *
          <span
            v-if="isCategoryA('bankAddress')"
            class="category-badge"
            :title="categoryBadgeTooltip"
          >
            {{ categoryBadgeLabel }}
          </span>
        </span>
        <input v-model.trim="form.bankAddress" type="text" required />
      </label>
      <label>
        <span>
          {{
            translateOr(
              "supplierRegistration.fields.bankAccountNumber",
              "Bank Account Number",
            )
          }}
          *
          <span
            v-if="isCategoryA('bankAccountNumber')"
            class="category-badge"
            :title="categoryBadgeTooltip"
          >
            {{ categoryBadgeLabel }}
          </span>
        </span>
        <input v-model.trim="form.bankAccountNumber" type="text" required />
      </label>
      <label>
        <span>
          {{
            translateOr(
              "supplierRegistration.fields.swiftCode",
              "SWIFT / BIC",
            )
          }}
          <span
            v-if="isCategoryA('swiftCode')"
            class="category-badge"
            :title="categoryBadgeTooltip"
          >
            {{ categoryBadgeLabel }}
          </span>
        </span>
        <input v-model.trim="form.swiftCode" type="text" />
      </label>
    </div>
  </section>
</template>

<script setup lang="ts">
import type { SupplierRegistrationFormState } from "./formState";

defineProps<{
  title: string;
  description: string;
  form: SupplierRegistrationFormState;
  translateOr: (key: string, fallback: string) => string;
  isCategoryA: (field: keyof SupplierRegistrationFormState) => boolean;
  categoryBadgeLabel: string;
  categoryBadgeTooltip: string;
}>();
</script>
