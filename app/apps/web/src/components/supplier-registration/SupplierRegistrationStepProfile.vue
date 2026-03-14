<template>
  <section class="wizard-step">
    <h2 class="step-title">{{ title }}</h2>
    <p class="step-description">{{ description }}</p>
    <div class="form-grid">
      <label>
        <span>
          {{ t("supplier.fields.companyName") }} *
          <span
            v-if="isCategoryA('companyName')"
            class="category-badge"
            :title="categoryBadgeTooltip"
          >
            {{ categoryBadgeLabel }}
          </span>
        </span>
        <input v-model.trim="form.companyName" type="text" required />
      </label>
      <label>
        <span>
          {{
            translateOr(
              "supplierRegistration.fields.englishName",
              "English Name",
            )
          }}
          <span
            v-if="isCategoryA('englishName')"
            class="category-badge"
            :title="categoryBadgeTooltip"
          >
            {{ categoryBadgeLabel }}
          </span>
        </span>
        <input v-model.trim="form.englishName" type="text" />
      </label>
      <label>
        <span>
          {{
            translateOr(
              "supplierRegistration.fields.chineseName",
              "Chinese Name",
            )
          }}
          <span
            v-if="isCategoryA('chineseName')"
            class="category-badge"
            :title="categoryBadgeTooltip"
          >
            {{ categoryBadgeLabel }}
          </span>
        </span>
        <input v-model.trim="form.chineseName" type="text" />
      </label>
      <label>
        <span>{{ t("supplier.fields.category") }} *</span>
        <select v-model="form.companyType" required>
          <option value="limited">
            {{ t("supplierRegistration.companyTypes.limited") }}
          </option>
          <option value="partnership">
            {{ t("supplierRegistration.companyTypes.partnership") }}
          </option>
          <option value="sole_proprietor">
            {{ t("supplierRegistration.companyTypes.soleProprietor") }}
          </option>
          <option value="other">
            {{ t("supplierRegistration.companyTypes.other") }}
          </option>
        </select>
      </label>
      <label v-if="form.companyType === 'other'">
        <span>
          {{
            translateOr(
              "supplierRegistration.fields.companyTypeOther",
              "Specify company type",
            )
          }}
          *
        </span>
        <input v-model.trim="form.companyTypeOther" type="text" required />
      </label>
      <label>
        <span>
          {{
            translateOr(
              "supplierRegistration.fields.businessNature",
              "Business Nature",
            )
          }}
        </span>
        <textarea v-model.trim="form.businessNature" rows="3" />
      </label>
      <label>
        <span>{{ t("rfq.materialType.label") }} *</span>
        <select v-model="form.supplierClassification" required>
          <option value="DM">{{ t("rfq.materialType.dm") }}</option>
          <option value="IDM">{{ t("rfq.materialType.idm") }}</option>
        </select>
      </label>
      <label>
        <span>{{ t("supplier.fields.paymentCurrency") }} *</span>
        <select v-model="form.operatingCurrency" required>
          <option value="RMB">{{ t("supplierRegistration.currencies.RMB") }}</option>
          <option value="USD">{{ t("supplierRegistration.currencies.USD") }}</option>
          <option value="EUR">{{ t("supplierRegistration.currencies.EUR") }}</option>
          <option value="GBP">{{ t("supplierRegistration.currencies.GBP") }}</option>
          <option value="KRW">{{ t("supplierRegistration.currencies.KRW") }}</option>
          <option value="THB">{{ t("supplierRegistration.currencies.THB") }}</option>
          <option value="JPY">{{ t("supplierRegistration.currencies.JPY") }}</option>
        </select>
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
}>();
</script>
