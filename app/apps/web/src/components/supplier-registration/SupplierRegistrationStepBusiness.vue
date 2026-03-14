<template>
  <section class="wizard-step">
    <h2 class="step-title">{{ title }}</h2>
    <p class="step-description">{{ description }}</p>
    <div class="form-grid">
      <label>
        <span>
          {{
            translateOr(
              "supplierRegistration.fields.deliveryLocation",
              "Delivery Coverage",
            )
          }}
          *
        </span>
        <input v-model.trim="form.deliveryLocation" type="text" required />
      </label>
      <label>
        <span>{{ t("supplier.fields.shipCode") || "Ship Code" }} *</span>
        <select v-model="form.shipCode" required>
          <option value="">{{ t("common.pleaseSelect") }}</option>
          <option
            v-for="(label, code) in incotermOptions"
            :key="code"
            :value="code"
          >
            {{ label }}
          </option>
        </select>
      </label>
      <label>
        <span>
          {{
            translateOr(
              "supplierRegistration.fields.productOrigin",
              "Product Origin",
            )
          }}
          *
        </span>
        <input v-model.trim="form.productOrigin" type="text" required />
      </label>
      <label class="span-2">
        <span>
          {{
            translateOr(
              "supplierRegistration.fields.notes",
              "Additional Background",
            )
          }}
        </span>
        <textarea v-model.trim="form.notes" rows="4" />
      </label>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import type { SupplierRegistrationFormState } from "./formState";

const { t } = useI18n();

const props = defineProps<{
  title: string;
  description: string;
  form: SupplierRegistrationFormState;
  translateOr: (key: string, fallback: string) => string;
}>();

const incotermCodes = [
  "EXW",
  "FOB",
  "CIF",
  "CFR",
  "DDP",
  "DDU",
  "DAP",
  "DAT",
  "FCA",
  "CPT",
  "CIP",
] as const;

const incotermOptions = computed<Record<string, string>>(() => {
  const entries = incotermCodes.map((code) => [
    code,
    props.translateOr(`supplierRegistration.incoterms.${code}`, code),
  ]);
  return Object.fromEntries(entries);
});
</script>
