<template>
  <section class="wizard-step">
    <h2 class="step-title">{{ title }}</h2>
    <p class="step-description">{{ description }}</p>
    <div class="form-grid">
      <label>
        <span>
          {{
            translateOr(
              "supplierRegistration.fields.invoiceType",
              "Invoice Type",
            )
          }}
          *
          <span
            v-if="isCategoryA('invoiceType')"
            class="category-badge"
            :title="categoryBadgeTooltip"
          >
            {{ categoryBadgeLabel }}
          </span>
        </span>
        <select v-model="form.invoiceType" required>
          <option value="">{{ t("common.pleaseSelect") }}</option>
          <option value="general_vat">
            {{
              translateOr(
                "supplierRegistration.invoice.generalVat",
                "VAT General Invoice",
              )
            }}
          </option>
          <option value="special_vat">
            {{
              translateOr(
                "supplierRegistration.invoice.specialVat",
                "VAT Special Invoice",
              )
            }}
          </option>
          <option value="ordinary">
            {{
              translateOr(
                "supplierRegistration.invoice.ordinary",
                "Ordinary Invoice",
              )
            }}
          </option>
        </select>
      </label>
      <label>
        <span>
          {{
            translateOr(
              "supplierRegistration.fields.billingPeriod",
              "Payment Terms",
            )
          }}
          <span
            v-if="isCategoryA('paymentTermsDays')"
            class="category-badge"
            :title="categoryBadgeTooltip"
          >
            {{ categoryBadgeLabel }}
          </span>
        </span>
        <select v-model="form.paymentTermsDays">
          <option value="">{{ t("common.pleaseSelect") }}</option>
          <option v-for="option in paymentTermOptions" :key="option.value" :value="option.value">
            {{ option.label }}
          </option>
        </select>
      </label>
      <div class="span-2 payment-methods">
        <span>
          {{
            translateOr(
              "supplierRegistration.fields.paymentMethods",
              "Payment Methods",
            )
          }}
          *
          <span
            v-if="isCategoryA('paymentMethods')"
            class="category-badge"
            :title="categoryBadgeTooltip"
          >
            {{ categoryBadgeLabel }}
          </span>
        </span>
        <div class="checkbox-grid">
          <label v-for="option in paymentMethodOptions" :key="option.value">
            <input
              type="checkbox"
              :value="option.value"
              v-model="form.paymentMethods"
            />
            {{ option.label }}
          </label>
        </div>
      </div>
      <label v-if="form.paymentMethods.includes('other')" class="span-2">
        <span>
          {{
            translateOr(
              "supplierRegistration.fields.paymentMethodsOther",
              "Describe other method",
            )
          }}
          *
        </span>
        <input v-model.trim="form.paymentMethodsOther" type="text" required />
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
  paymentMethodOptions: Array<{ value: string; label: string }>;
  paymentTermOptions: Array<{ value: string; label: string }>;
}>();
</script>
