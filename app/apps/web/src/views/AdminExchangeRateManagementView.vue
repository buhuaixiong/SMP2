<template>
  <div class="exchange-rate-management">
    <PageHeader>
      <template #title>{{ t('exchangeRate.title') }}</template>
      <template #actions>
        <el-button type="primary" :icon="Refresh" @click="handleRefreshRates">
          {{ t('exchangeRate.refreshRates') }}
        </el-button>
        <el-button type="success" :icon="Edit" @click="showUpdateDialog = true">
          {{ t('exchangeRate.updateRates') }}
        </el-button>
      </template>
    </PageHeader>

    <!-- Reminder Alert -->
    <el-alert
      v-if="shouldShowReminder"
      :title="t('exchangeRate.reminderTitle')"
      type="warning"
      :closable="false"
      show-icon
      class="reminder-alert"
    >
      <template #default>
        <div>{{ t('exchangeRate.reminderMessage') }}</div>
        <el-button size="small" type="warning" @click="showUpdateDialog = true" style="margin-top: 8px">
          {{ t('exchangeRate.updateNow') }}
        </el-button>
      </template>
    </el-alert>

    <div class="management-columns">
      <div class="management-column">
        <!-- Current Rates Card -->
        <el-card class="current-rates-card" shadow="never">
          <template #header>
            <div class="card-header">
              <span class="card-title">{{ t('exchangeRate.currentRates') }}</span>
              <el-tag v-if="config" type="info">
                {{ t('exchangeRate.lastUpdated') }}: {{ formatDate(config.updatedAt) }}
              </el-tag>
            </div>
          </template>

          <div v-loading="loadingConfig">
            <el-table v-if="currentRatesData" :data="currentRatesData" border style="width: 100%">
              <el-table-column :label="t('exchangeRate.currency')" prop="currency" width="120">
                <template #default="{ row }">
                  <el-tag>{{ row.currency }}</el-tag>
                </template>
              </el-table-column>
              <el-table-column :label="t('exchangeRate.rate')" prop="rate" width="180">
                <template #default="{ row }">
                  <span class="rate-value">{{ row.rate.toFixed(7) }}</span>
                </template>
              </el-table-column>
              <el-table-column :label="t('exchangeRate.rateFormula')" min-width="200">
                <template #default="{ row }">
                  <span class="formula">1 {{ row.currency }} = {{ row.rate.toFixed(7) }} USD</span>
                </template>
              </el-table-column>
              <el-table-column :label="t('exchangeRate.inverseRate')" width="180">
                <template #default="{ row }">
                  <span class="rate-value">{{ (1 / row.rate).toFixed(2) }}</span>
                </template>
              </el-table-column>
              <el-table-column :label="t('exchangeRate.inverseFormula')" min-width="200">
                <template #default="{ row }">
                  <span class="formula">1 USD = {{ (1 / row.rate).toFixed(4) }} {{ row.currency }}</span>
                </template>
              </el-table-column>
            </el-table>

            <el-empty v-else :description="t('exchangeRate.noData')" />
          </div>
        </el-card>

        <!-- History Card -->
        <el-card class="history-card" shadow="never">
          <template #header>
            <div class="card-header">
              <span class="card-title">{{ t('exchangeRate.history') }}</span>
              <el-select
                v-model="selectedCurrency"
                :placeholder="t('exchangeRate.selectCurrency')"
                clearable
                @change="loadHistory"
                style="width: 200px"
              >
                <el-option
                  v-for="curr in supportedCurrencies"
                  :key="curr"
                  :label="curr"
                  :value="curr"
                />
              </el-select>
            </div>
          </template>

          <div v-loading="loadingHistory">
            <el-table :data="history" border style="width: 100%">
              <el-table-column :label="t('exchangeRate.effectiveDate')" prop="effectiveDate" width="140">
                <template #default="{ row }">
                  {{ formatDate(row.effectiveDate) }}
                </template>
              </el-table-column>
              <el-table-column :label="t('exchangeRate.currency')" prop="currency" width="100">
                <template #default="{ row }">
                  <el-tag size="small">{{ row.currency }}</el-tag>
                </template>
              </el-table-column>
              <el-table-column :label="t('exchangeRate.rate')" prop="rate" width="150">
                <template #default="{ row }">
                  {{ row.rate.toFixed(7) }}
                </template>
              </el-table-column>
              <el-table-column :label="t('exchangeRate.source')" prop="source" width="100">
                <template #default="{ row }">
                  <el-tag :type="row.source === 'manual' ? 'warning' : 'info'" size="small">
                    {{ row.source }}
                  </el-tag>
                </template>
              </el-table-column>
              <el-table-column :label="t('exchangeRate.notes')" prop="notes" min-width="200" show-overflow-tooltip />
              <el-table-column :label="t('exchangeRate.createdAt')" prop="createdAt" width="160">
                <template #default="{ row }">
                  {{ formatDateTime(row.createdAt) }}
                </template>
              </el-table-column>
              <el-table-column :label="t('common.actions')" width="100" fixed="right">
                <template #default="{ row }">
                  <el-button
                    type="danger"
                    size="small"
                    :icon="Delete"
                    @click="handleDelete(row)"
                    link
                  >
                    {{ t('common.delete') }}
                  </el-button>
                </template>
              </el-table-column>
            </el-table>

            <el-empty v-if="history.length === 0 && !loadingHistory" :description="t('exchangeRate.noHistory')" />
          </div>
        </el-card>
      </div>

      <div class="management-column">
        <!-- Freight Rates Card -->
        <el-card class="freight-rates-card" shadow="never">
          <template #header>
            <div class="card-header">
              <div class="card-title-group">
                <span class="card-title">{{ t("exchangeRate.freightCurrentRates") }}</span>
                <el-select
                  v-model="freightYear"
                  :placeholder="t('exchangeRate.freightSelectYear')"
                  size="small"
                  style="width: 120px"
                  @change="loadFreightRates"
                >
                  <el-option v-for="year in freightAvailableYears" :key="year" :label="year" :value="year" />
                </el-select>
              </div>
              <el-button type="success" :icon="Edit" @click="showFreightUpdateDialog = true">
                {{ t("exchangeRate.freightUpdateRates") }}
              </el-button>
            </div>
          </template>

          <div v-loading="loadingFreight">
            <el-table
              v-if="freightRoutesData.length > 0"
              :data="freightRoutesData"
              border
              style="width: 100%"
            >
              <el-table-column :label="t('exchangeRate.freightRoute')" min-width="160">
                <template #default="{ row }">
                  <el-tag size="small">{{ row.routeCode }}</el-tag>
                </template>
              </el-table-column>
              <el-table-column :label="t('exchangeRate.freightRouteName')" min-width="200">
                <template #default="{ row }">
                  <div class="route-name">
                    <div>{{ row.routeName || row.routeCode }}</div>
                    <div v-if="row.routeNameZh" class="route-name-zh">{{ row.routeNameZh }}</div>
                  </div>
                </template>
              </el-table-column>
              <el-table-column :label="t('exchangeRate.freightRate')" width="140">
                <template #default="{ row }">
                  <span class="rate-value">{{ row.rate.toFixed(6) }}</span>
                </template>
              </el-table-column>
              <el-table-column :label="t('exchangeRate.freightRatePercent')" width="140">
                <template #default="{ row }">
                  {{ formatRatePercent(row.rate) }}
                </template>
              </el-table-column>
            </el-table>

            <el-empty v-else :description="t('exchangeRate.freightNoData')" />
          </div>
        </el-card>

        <!-- Country Freight Rates Card -->
        <el-card class="country-freight-rates-card" shadow="never">
          <template #header>
            <div class="card-header">
              <div class="card-title-group">
                <span class="card-title">{{ t("exchangeRate.countryFreightCurrentRates") }}</span>
                <el-select
                  v-model="countryFreightYear"
                  :placeholder="t('exchangeRate.countryFreightSelectYear')"
                  size="small"
                  style="width: 140px"
                  @change="loadCountryFreightRates"
                >
                  <el-option
                    v-for="year in countryFreightAvailableYears"
                    :key="year"
                    :label="year"
                    :value="year"
                  />
                </el-select>
              </div>
              <el-button type="success" :icon="Edit" @click="showCountryFreightUpdateDialog = true">
                {{ t("exchangeRate.countryFreightUpdateRates") }}
              </el-button>
            </div>
          </template>

          <div v-loading="loadingCountryFreight">
            <el-table
              v-if="countryFreightRatesData.length > 0"
              :data="countryFreightRatesData"
              border
              style="width: 100%"
            >
              <el-table-column :label="t('exchangeRate.countryFreightCountryCode')" min-width="140">
                <template #default="{ row }">
                  <el-tag size="small">{{ row.countryCode }}</el-tag>
                </template>
              </el-table-column>
              <el-table-column :label="t('exchangeRate.countryFreightCountryName')" min-width="160">
                <template #default="{ row }">
                  {{ row.countryName || row.countryCode }}
                </template>
              </el-table-column>
              <el-table-column :label="t('exchangeRate.countryFreightCountryNameZh')" min-width="160">
                <template #default="{ row }">
                  {{ row.countryNameZh || "-" }}
                </template>
              </el-table-column>
              <el-table-column :label="t('exchangeRate.countryFreightProductGroup')" min-width="140">
                <template #default="{ row }">
                  <el-tag size="small" type="info">{{ row.productGroup }}</el-tag>
                </template>
              </el-table-column>
              <el-table-column :label="t('exchangeRate.countryFreightRate')" width="140">
                <template #default="{ row }">
                  <span class="rate-value">{{ row.rate.toFixed(6) }}</span>
                </template>
              </el-table-column>
              <el-table-column :label="t('exchangeRate.countryFreightRatePercent')" width="140">
                <template #default="{ row }">
                  {{ formatRatePercent(row.rate) }}
                </template>
              </el-table-column>
            </el-table>

            <el-empty v-else :description="t('exchangeRate.countryFreightNoData')" />
          </div>
        </el-card>

        <!-- Freight History Card -->
        <el-card class="freight-history-card" shadow="never">
          <template #header>
            <div class="card-header">
              <span class="card-title">{{ t("exchangeRate.freightHistory") }}</span>
              <div class="card-filters">
                <el-select
                  v-model="freightHistoryYear"
                  :placeholder="t('exchangeRate.freightSelectYear')"
                  clearable
                  size="small"
                  style="width: 120px"
                  @change="loadFreightHistory"
                >
                  <el-option v-for="year in freightAvailableYears" :key="year" :label="year" :value="year" />
                </el-select>
                <el-select
                  v-model="selectedFreightRoute"
                  :placeholder="t('exchangeRate.freightSelectRoute')"
                  clearable
                  filterable
                  size="small"
                  style="width: 200px"
                  @change="loadFreightHistory"
                >
                  <el-option
                    v-for="route in freightRoutesData"
                    :key="route.routeCode"
                    :label="route.routeName || route.routeCode"
                    :value="route.routeCode"
                  />
                </el-select>
              </div>
            </div>
          </template>

          <div v-loading="loadingFreightHistory">
            <el-table :data="freightHistory" border style="width: 100%">
              <el-table-column :label="t('exchangeRate.freightYear')" prop="year" width="100" />
              <el-table-column :label="t('exchangeRate.freightRoute')" min-width="160">
                <template #default="{ row }">
                  <el-tag size="small">{{ row.routeCode }}</el-tag>
                </template>
              </el-table-column>
              <el-table-column :label="t('exchangeRate.freightRate')" prop="rate" width="140">
                <template #default="{ row }">
                  {{ row.rate.toFixed(6) }}
                </template>
              </el-table-column>
              <el-table-column :label="t('exchangeRate.freightRatePercent')" width="140">
                <template #default="{ row }">
                  {{ formatRatePercent(row.rate) }}
                </template>
              </el-table-column>
              <el-table-column :label="t('exchangeRate.source')" prop="source" width="100">
                <template #default="{ row }">
                  <el-tag :type="row.source === 'manual' ? 'warning' : 'info'" size="small">
                    {{ row.source }}
                  </el-tag>
                </template>
              </el-table-column>
              <el-table-column :label="t('exchangeRate.notes')" prop="notes" min-width="200" show-overflow-tooltip />
              <el-table-column :label="t('exchangeRate.createdAt')" prop="createdAt" width="160">
                <template #default="{ row }">
                  {{ formatDateTime(row.createdAt) }}
                </template>
              </el-table-column>
            </el-table>

            <el-empty
              v-if="freightHistory.length === 0 && !loadingFreightHistory"
              :description="t('exchangeRate.freightNoHistory')"
            />
          </div>
        </el-card>
      </div>
    </div>

    <!-- Update Dialog -->
    <el-dialog
      v-model="showUpdateDialog"
      :title="t('exchangeRate.updateDialogTitle')"
      width="800px"
      :close-on-click-modal="false"
    >
      <el-form :model="updateForm" label-width="140px">
        <el-form-item :label="t('exchangeRate.effectiveDate')">
          <el-date-picker
            v-model="updateForm.effectiveDate"
            type="date"
            :placeholder="t('exchangeRate.selectDate')"
            value-format="YYYY-MM-DD"
            style="width: 100%"
          />
          <div class="form-help">{{ t('exchangeRate.effectiveDateHelp') }}</div>
        </el-form-item>

        <el-form-item :label="t('exchangeRate.notes')">
          <el-input
            v-model="updateForm.notes"
            type="textarea"
            :rows="2"
            :placeholder="t('exchangeRate.notesPlaceholder')"
          />
        </el-form-item>

        <el-divider />

        <div class="rates-grid">
          <div v-for="(rate, currency) in updateForm.rates" :key="currency" class="rate-input-item">
            <el-form-item :label="currency">
              <el-input-number
                v-model="updateForm.rates[currency]"
                :precision="7"
                :min="0.0000001"
                :step="0.0000001"
                style="width: 100%"
              />
              <div class="rate-preview">
                1 {{ currency }} = {{ updateForm.rates[currency]?.toFixed(7) || 0 }} USD
              </div>
            </el-form-item>
          </div>
        </div>

        <el-alert
          :title="t('exchangeRate.updateWarning')"
          type="warning"
          :closable="false"
          show-icon
          style="margin-top: 16px"
        />
      </el-form>

      <template #footer>
        <el-button @click="showUpdateDialog = false">{{ t('common.cancel') }}</el-button>
        <el-button type="primary" @click="handleUpdateRates" :loading="updating">
          {{ t('exchangeRate.confirmUpdate') }}
        </el-button>
      </template>
    </el-dialog>

    <!-- Country Freight Update Dialog -->
    <el-dialog
      v-model="showCountryFreightUpdateDialog"
      :title="t('exchangeRate.countryFreightUpdateDialogTitle')"
      width="980px"
      :close-on-click-modal="false"
    >
      <el-form :model="countryFreightForm" label-width="160px">
        <el-form-item :label="t('exchangeRate.countryFreightYear')">
          <el-select
            v-model="countryFreightForm.year"
            :placeholder="t('exchangeRate.countryFreightSelectYear')"
            style="width: 180px"
            @change="handleCountryFreightYearChange"
          >
            <el-option
              v-for="year in countryFreightAvailableYears"
              :key="year"
              :label="year"
              :value="year"
            />
          </el-select>
        </el-form-item>

        <el-form-item :label="t('exchangeRate.notes')">
          <el-input
            v-model="countryFreightForm.notes"
            type="textarea"
            :rows="2"
            :placeholder="t('exchangeRate.countryFreightNotesPlaceholder')"
          />
        </el-form-item>

        <el-divider />

        <div class="freight-dialog-actions">
          <el-button :icon="Plus" @click="handleAddCountryFreightRate">
            {{ t("exchangeRate.countryFreightAddRate") }}
          </el-button>
        </div>

        <el-table :data="countryFreightForm.rates" border style="width: 100%">
          <el-table-column :label="t('exchangeRate.countryFreightCountryCode')" min-width="150">
            <template #default="{ row }">
              <el-input v-model="row.countryCode" :disabled="!row.isNew" />
            </template>
          </el-table-column>
          <el-table-column :label="t('exchangeRate.countryFreightCountryName')" min-width="160">
            <template #default="{ row }">
              <el-input v-model="row.countryName" :placeholder="t('exchangeRate.countryFreightCountryNamePlaceholder')" />
            </template>
          </el-table-column>
          <el-table-column :label="t('exchangeRate.countryFreightCountryNameZh')" min-width="160">
            <template #default="{ row }">
              <el-input v-model="row.countryNameZh" :placeholder="t('exchangeRate.countryFreightCountryNameZhPlaceholder')" />
            </template>
          </el-table-column>
          <el-table-column :label="t('exchangeRate.countryFreightProductGroup')" min-width="160">
            <template #default="{ row }">
              <el-select v-model="row.productGroup" :disabled="!row.isNew" filterable>
                <el-option
                  v-for="group in countryFreightProductGroups"
                  :key="group"
                  :label="group"
                  :value="group"
                />
              </el-select>
            </template>
          </el-table-column>
          <el-table-column :label="t('exchangeRate.countryFreightRate')" width="160">
            <template #default="{ row }">
              <el-input-number
                v-model="row.rate"
                :precision="6"
                :min="0.000001"
                :step="0.000001"
                style="width: 100%"
              />
            </template>
          </el-table-column>
          <el-table-column :label="t('common.actions')" width="100">
            <template #default="{ row, $index }">
              <el-button
                v-if="row.isNew"
                type="danger"
                size="small"
                :icon="Delete"
                @click="handleRemoveCountryFreightRate($index)"
                link
              >
                {{ t("common.delete") }}
              </el-button>
            </template>
          </el-table-column>
        </el-table>
      </el-form>

      <template #footer>
        <el-button @click="showCountryFreightUpdateDialog = false">{{ t("common.cancel") }}</el-button>
        <el-button type="primary" @click="handleUpdateCountryFreightRates" :loading="updatingCountryFreight">
          {{ t("exchangeRate.confirmUpdate") }}
        </el-button>
      </template>
    </el-dialog>

    <!-- Freight Update Dialog -->
    <el-dialog
      v-model="showFreightUpdateDialog"
      :title="t('exchangeRate.freightUpdateDialogTitle')"
      width="900px"
      :close-on-click-modal="false"
    >
      <el-form :model="freightForm" label-width="120px">
        <el-form-item :label="t('exchangeRate.freightYear')">
          <el-select
            v-model="freightForm.year"
            :placeholder="t('exchangeRate.freightSelectYear')"
            style="width: 180px"
            @change="handleFreightYearChange"
          >
            <el-option v-for="year in freightAvailableYears" :key="year" :label="year" :value="year" />
          </el-select>
        </el-form-item>

        <el-form-item :label="t('exchangeRate.notes')">
          <el-input
            v-model="freightForm.notes"
            type="textarea"
            :rows="2"
            :placeholder="t('exchangeRate.freightNotesPlaceholder')"
          />
        </el-form-item>

        <el-divider />

        <div class="freight-dialog-actions">
          <el-button :icon="Plus" @click="handleAddFreightRoute">
            {{ t("exchangeRate.freightAddRoute") }}
          </el-button>
        </div>

        <el-table :data="freightForm.routes" border style="width: 100%">
          <el-table-column :label="t('exchangeRate.freightRoute')" min-width="180">
            <template #default="{ row }">
              <el-input
                v-model="row.routeCode"
                :placeholder="t('exchangeRate.freightRoutePlaceholder')"
                :disabled="!row.isNew"
              />
            </template>
          </el-table-column>
          <el-table-column :label="t('exchangeRate.freightRouteName')" min-width="180">
            <template #default="{ row }">
              <el-input
                v-model="row.routeName"
                :placeholder="t('exchangeRate.freightRouteNamePlaceholder')"
              />
            </template>
          </el-table-column>
          <el-table-column :label="t('exchangeRate.freightRouteNameZh')" min-width="180">
            <template #default="{ row }">
              <el-input
                v-model="row.routeNameZh"
                :placeholder="t('exchangeRate.freightRouteNameZhPlaceholder')"
              />
            </template>
          </el-table-column>
          <el-table-column :label="t('exchangeRate.freightRate')" width="160">
            <template #default="{ row }">
              <el-input-number
                v-model="row.rate"
                :precision="6"
                :min="0.000001"
                :step="0.000001"
                style="width: 100%"
              />
            </template>
          </el-table-column>
          <el-table-column :label="t('common.actions')" width="100">
            <template #default="{ row, $index }">
              <el-button
                v-if="row.isNew"
                type="danger"
                size="small"
                :icon="Delete"
                @click="handleRemoveFreightRoute($index)"
                link
              >
                {{ t("common.delete") }}
              </el-button>
            </template>
          </el-table-column>
        </el-table>
      </el-form>

      <template #footer>
        <el-button @click="showFreightUpdateDialog = false">{{ t("common.cancel") }}</el-button>
        <el-button type="primary" @click="handleUpdateFreightRates" :loading="updatingFreight">
          {{ t("exchangeRate.confirmUpdate") }}
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { Refresh, Edit, Delete, Plus } from "@element-plus/icons-vue";
import PageHeader from "@/components/layout/PageHeader.vue";
import { useNotification } from "@/composables";
import {
  fetchExchangeRateConfig,
  fetchExchangeRateHistory,
  fetchSupportedCurrencies,
  updateExchangeRates,
  deleteExchangeRateRecord,
  type ExchangeRate,
  type ExchangeRateConfig,
} from "@/api/exchangeRates";
import {
  fetchFreightRates,
  fetchFreightRateHistory,
  updateFreightRates,
  type FreightRateConfig,
  type FreightRateHistory,
} from "@/api/freightRates";
import {
  fetchCountryFreightRates,
  updateCountryFreightRates,
  type CountryFreightRateConfig,
} from "@/api/countryFreightRates";

const notification = useNotification();
const { t } = useI18n();

// State
const config = ref<ExchangeRateConfig | null>(null);
const history = ref<ExchangeRate[]>([]);
const supportedCurrencies = ref<string[]>([]);
const selectedCurrency = ref<string>('');
const loadingConfig = ref(false);
const loadingHistory = ref(false);
const showUpdateDialog = ref(false);
const updating = ref(false);

const freightConfig = ref<FreightRateConfig | null>(null);
const freightHistory = ref<FreightRateHistory[]>([]);
const freightAvailableYears = ref<number[]>([]);
const freightYear = ref<number>(0);
const freightHistoryYear = ref<number | null>(null);
const selectedFreightRoute = ref<string>("");
const loadingFreight = ref(false);
const loadingFreightHistory = ref(false);
const showFreightUpdateDialog = ref(false);
const updatingFreight = ref(false);

const countryFreightConfig = ref<CountryFreightRateConfig | null>(null);
const countryFreightAvailableYears = ref<number[]>([]);
const countryFreightYear = ref<number>(0);
const countryFreightProductGroups = ref<string[]>([]);
const loadingCountryFreight = ref(false);
const showCountryFreightUpdateDialog = ref(false);
const updatingCountryFreight = ref(false);

// Update form
const updateForm = ref({
  effectiveDate: new Date().toISOString().split('T')[0],
  notes: '',
  rates: {
    CNY: 0.1388889,
    HKD: 0.1282051,
    EUR: 1.0989011,
    USD: 1.0,
    KRW: 0.0007576,
    GBP: 1.35,
    JPY: 0.0068376,
    THB: 0.0277778,
  }
});

type FreightRouteFormItem = {
  routeCode: string;
  routeName: string;
  routeNameZh: string;
  rate: number | null;
  isNew?: boolean;
};

const freightForm = ref({
  year: 0,
  notes: "",
  routes: [] as FreightRouteFormItem[],
});

type CountryFreightRateFormItem = {
  countryCode: string;
  countryName: string;
  countryNameZh: string;
  productGroup: string;
  rate: number | null;
  isNew?: boolean;
};

const countryFreightForm = ref({
  year: 0,
  notes: "",
  rates: [] as CountryFreightRateFormItem[],
});

// Computed
const currentRatesData = computed(() => {
  if (!config.value) return null;

  const year = config.value.defaultYear;
  const rates = config.value.rates[year];

  if (!rates) return null;

  return Object.entries(rates).map(([currency, rate]) => ({
    currency,
    rate
  }));
});

const freightRoutesData = computed(() => freightConfig.value?.routes ?? []);
const countryFreightRatesData = computed(() => countryFreightConfig.value?.rates ?? []);

const shouldShowReminder = computed(() => {
  if (!config.value) return false;

  const lastUpdate = new Date(config.value.updatedAt);
  const now = new Date();
  const daysSinceUpdate = Math.floor((now.getTime() - lastUpdate.getTime()) / (1000 * 60 * 60 * 24));

  // Show reminder if:
  // 1. It's January 3rd or later in a new year
  // 2. Last update was in previous year
  const isJanuaryOrLater = now.getMonth() >= 0; // January is 0
  const isAfterJan3 = now.getMonth() > 0 || now.getDate() >= 3;
  const isDifferentYear = lastUpdate.getFullYear() < now.getFullYear();

  return isAfterJan3 && isDifferentYear;
});

// Methods
const loadConfig = async () => {
  loadingConfig.value = true;
  try {
    config.value = await fetchExchangeRateConfig();

    // Update form with current rates
    const year = config.value.defaultYear;
    if (config.value.rates[year]) {
      updateForm.value.rates = { ...config.value.rates[year] } as typeof updateForm.value.rates;
    }
  } catch (error: any) {
    notification.error(t('exchangeRate.loadConfigFailed'));
    console.error('Failed to load config:', error);
  } finally {
    loadingConfig.value = false;
  }
};

const loadHistory = async () => {
  loadingHistory.value = true;
  try {
    history.value = await fetchExchangeRateHistory(selectedCurrency.value, 50);
  } catch (error: any) {
    notification.error(t('exchangeRate.loadHistoryFailed'));
    console.error('Failed to load history:', error);
  } finally {
    loadingHistory.value = false;
  }
};

const loadSupportedCurrencies = async () => {
  try {
    supportedCurrencies.value = await fetchSupportedCurrencies();
  } catch (error: any) {
    console.error('Failed to load currencies:', error);
  }
};

const loadFreightRates = async () => {
  loadingFreight.value = true;
  try {
    const requestYear = freightYear.value > 0 ? freightYear.value : undefined;
    const data = await fetchFreightRates(requestYear);
    freightConfig.value = data;
    freightAvailableYears.value = data.availableYears ?? [];
    freightYear.value = data.year;
    freightForm.value.year = data.year;
    freightForm.value.routes = data.routes.map((route) => ({
      routeCode: route.routeCode,
      routeName: route.routeName ?? "",
      routeNameZh: route.routeNameZh ?? "",
      rate: route.rate,
      isNew: false,
    }));

    if (!freightHistoryYear.value) {
      freightHistoryYear.value = data.year;
    }
  } catch (error: any) {
    notification.error(t("exchangeRate.freightLoadFailed"));
    console.error("Failed to load freight rates:", error);
  } finally {
    loadingFreight.value = false;
  }
};

const loadFreightHistory = async () => {
  loadingFreightHistory.value = true;
  try {
    freightHistory.value = await fetchFreightRateHistory(
      selectedFreightRoute.value || undefined,
      freightHistoryYear.value ?? undefined,
      50,
    );
  } catch (error: any) {
    notification.error(t("exchangeRate.freightLoadHistoryFailed"));
    console.error("Failed to load freight history:", error);
  } finally {
    loadingFreightHistory.value = false;
  }
};

const loadCountryFreightRates = async () => {
  loadingCountryFreight.value = true;
  try {
    const requestYear = countryFreightYear.value > 0 ? countryFreightYear.value : undefined;
    const data = await fetchCountryFreightRates(requestYear);
    countryFreightConfig.value = data;
    countryFreightAvailableYears.value = data.availableYears ?? [];
    countryFreightYear.value = data.year;
    countryFreightProductGroups.value = data.productGroups ?? [];
    countryFreightForm.value.year = data.year;
    countryFreightForm.value.rates = data.rates.map((rate) => ({
      countryCode: rate.countryCode,
      countryName: rate.countryName ?? "",
      countryNameZh: rate.countryNameZh ?? "",
      productGroup: rate.productGroup,
      rate: rate.rate,
      isNew: false,
    }));
  } catch (error: any) {
    notification.error(t("exchangeRate.countryFreightLoadFailed"));
    console.error("Failed to load country freight rates:", error);
  } finally {
    loadingCountryFreight.value = false;
  }
};

const handleRefreshRates = () => {
  loadConfig();
  loadHistory();
  loadFreightRates();
  loadFreightHistory();
  loadCountryFreightRates();
};

const handleUpdateRates = async () => {
  try {
    await notification.confirm(
      t('exchangeRate.confirmUpdateMessage'),
      t('exchangeRate.confirmUpdateTitle'),
      {
        confirmButtonText: t('common.confirm'),
        cancelButtonText: t('common.cancel'),
        type: 'warning'
      }
    );

    updating.value = true;

    await updateExchangeRates({
      rates: updateForm.value.rates,
      effectiveDate: updateForm.value.effectiveDate,
      notes: updateForm.value.notes
    });

    notification.success(t('exchangeRate.updateSuccess'));
    showUpdateDialog.value = false;

    // Reload data
    await loadConfig();
    await loadHistory();
  } catch (error: any) {
    if (error !== 'cancel') {
      notification.error(error.message || t('exchangeRate.updateFailed'));
      console.error('Failed to update rates:', error);
    }
  } finally {
    updating.value = false;
  }
};

const handleDelete = async (record: ExchangeRate) => {
  try {
    await notification.confirm(
      t('exchangeRate.confirmDeleteMessage'),
      t('exchangeRate.confirmDeleteTitle'),
      {
        confirmButtonText: t('common.delete'),
        cancelButtonText: t('common.cancel'),
        type: 'warning'
      }
    );

    await deleteExchangeRateRecord(record.id);
    notification.success(t('exchangeRate.deleteSuccess'));

    // Reload history
    await loadHistory();
  } catch (error: any) {
    if (error !== 'cancel') {
      notification.error(error.message || t('exchangeRate.deleteFailed'));
      console.error('Failed to delete record:', error);
    }
  }
};

const handleAddFreightRoute = () => {
  freightForm.value.routes.push({
    routeCode: "",
    routeName: "",
    routeNameZh: "",
    rate: null,
    isNew: true,
  });
};

const handleRemoveFreightRoute = (index: number) => {
  freightForm.value.routes.splice(index, 1);
};

const handleAddCountryFreightRate = () => {
  countryFreightForm.value.rates.push({
    countryCode: "",
    countryName: "",
    countryNameZh: "",
    productGroup: countryFreightProductGroups.value[0] ?? "",
    rate: null,
    isNew: true,
  });
};

const handleRemoveCountryFreightRate = (index: number) => {
  countryFreightForm.value.rates.splice(index, 1);
};

const handleUpdateFreightRates = async () => {
  try {
    await notification.confirm(
      t("exchangeRate.freightConfirmUpdateMessage"),
      t("exchangeRate.freightConfirmUpdateTitle"),
      {
        confirmButtonText: t("common.confirm"),
        cancelButtonText: t("common.cancel"),
        type: "warning",
      },
    );

    const routes = freightForm.value.routes
      .filter((route) => route.routeCode.trim().length > 0 && route.rate !== null)
      .map((route) => ({
        routeCode: route.routeCode.trim(),
        routeName: route.routeName.trim().length > 0 ? route.routeName.trim() : null,
        routeNameZh: route.routeNameZh.trim().length > 0 ? route.routeNameZh.trim() : null,
        rate: Number(route.rate),
      }))
      .filter((route) => Number.isFinite(route.rate) && route.rate > 0);

    if (routes.length === 0) {
      notification.warning(t("exchangeRate.freightNoData"));
      return;
    }

    updatingFreight.value = true;

    await updateFreightRates({
      year: freightForm.value.year,
      notes: freightForm.value.notes,
      routes,
    });

    notification.success(t("exchangeRate.freightUpdateSuccess"));
    showFreightUpdateDialog.value = false;

    await loadFreightRates();
    await loadFreightHistory();
  } catch (error: any) {
    if (error !== "cancel") {
      notification.error(error.message || t("exchangeRate.freightUpdateFailed"));
      console.error("Failed to update freight rates:", error);
    }
  } finally {
    updatingFreight.value = false;
  }
};

const handleUpdateCountryFreightRates = async () => {
  try {
    await notification.confirm(
      t("exchangeRate.countryFreightConfirmUpdateMessage"),
      t("exchangeRate.countryFreightConfirmUpdateTitle"),
      {
        confirmButtonText: t("common.confirm"),
        cancelButtonText: t("common.cancel"),
        type: "warning",
      },
    );

    const rates = countryFreightForm.value.rates
      .filter((rate) => rate.countryCode.trim().length > 0 && rate.productGroup.trim().length > 0 && rate.rate !== null)
      .map((rate) => ({
        countryCode: rate.countryCode.trim(),
        productGroup: rate.productGroup.trim(),
        countryName: rate.countryName.trim().length > 0 ? rate.countryName.trim() : null,
        countryNameZh: rate.countryNameZh.trim().length > 0 ? rate.countryNameZh.trim() : null,
        rate: Number(rate.rate),
      }))
      .filter((rate) => Number.isFinite(rate.rate) && rate.rate > 0);

    if (rates.length === 0) {
      notification.warning(t("exchangeRate.countryFreightNoData"));
      return;
    }

    updatingCountryFreight.value = true;

    await updateCountryFreightRates({
      year: countryFreightForm.value.year,
      notes: countryFreightForm.value.notes,
      rates,
    });

    notification.success(t("exchangeRate.countryFreightUpdateSuccess"));
    showCountryFreightUpdateDialog.value = false;

    await loadCountryFreightRates();
  } catch (error: any) {
    if (error !== "cancel") {
      notification.error(error.message || t("exchangeRate.countryFreightUpdateFailed"));
      console.error("Failed to update country freight rates:", error);
    }
  } finally {
    updatingCountryFreight.value = false;
  }
};

const handleFreightYearChange = async () => {
  freightYear.value = freightForm.value.year;
  await loadFreightRates();
  await loadFreightHistory();
};

const handleCountryFreightYearChange = async () => {
  countryFreightYear.value = countryFreightForm.value.year;
  await loadCountryFreightRates();
};

const formatDate = (date: string) => {
  if (!date) return '-';
  return new Date(date).toLocaleDateString();
};

const formatDateTime = (datetime: string) => {
  if (!datetime) return '-';
  return new Date(datetime).toLocaleString();
};

const formatRatePercent = (rate: number | null | undefined) => {
  if (rate === null || rate === undefined || Number.isNaN(rate)) return "-";
  return `${(rate * 100).toFixed(2)}%`;
};

// Lifecycle
onMounted(() => {
  loadConfig();
  loadHistory();
  loadSupportedCurrencies();
  loadFreightRates();
  loadFreightHistory();
  loadCountryFreightRates();
});



</script>

<style scoped lang="scss">
.exchange-rate-management {
  padding: 20px;

  .management-columns {
    display: grid;
    grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
    gap: 20px;
  }

  .management-column {
    min-width: 0;
  }

  .card-title-group {
    display: flex;
    align-items: center;
    gap: 12px;
  }

  .card-filters {
    display: flex;
    align-items: center;
    gap: 8px;
  }

  .reminder-alert {
    margin-bottom: 20px;
  }

  .current-rates-card,
  .history-card,
  .freight-rates-card,
  .country-freight-rates-card,
  .freight-history-card {
    margin-bottom: 20px;

    .card-header {
      display: flex;
      justify-content: space-between;
      align-items: center;

      .card-title {
        font-weight: 600;
        font-size: 16px;
      }
    }

    .rate-value {
      font-family: 'Courier New', monospace;
      font-weight: 600;
      color: #409eff;
    }

    .formula {
      color: #606266;
      font-size: 13px;
    }
  }

  .route-name {
    display: flex;
    flex-direction: column;
    gap: 2px;
  }

  .route-name-zh {
    font-size: 12px;
    color: #909399;
  }

  .rates-grid {
    display: grid;
    grid-template-columns: repeat(2, 1fr);
    gap: 16px;

    .rate-input-item {
      .rate-preview {
        margin-top: 4px;
        font-size: 12px;
        color: #909399;
        font-family: 'Courier New', monospace;
      }
    }
  }

  .form-help {
    margin-top: 4px;
    font-size: 12px;
    color: #909399;
  }

  .freight-dialog-actions {
    display: flex;
    justify-content: flex-end;
    margin-bottom: 12px;
  }

  @media (max-width: 1200px) {
    .management-columns {
      grid-template-columns: 1fr;
    }
  }
}
</style>
