<template>
  <div class="rfq-distribution-selector">
    <h3 class="selector-title">{{ t("rfq.distributionForm.selectCategory") }}</h3>
    <p class="selector-description">{{ t("rfq.distributionForm.description") }}</p>

    <div class="category-grid">
      <!-- Equipment Category -->
      <div
        class="category-card"
        :class="{ selected: selectedCard === 'equipment' }"
        @click="selectCategory('equipment')"
      >
        <div class="category-icon">
          <el-icon :size="48"><Setting /></el-icon>
        </div>
        <h4 class="category-title">{{ t("rfq.distributionCategory.equipment") }}</h4>
        <p class="category-desc">{{ t("rfq.distributionForm.equipmentDesc") }}</p>
      </div>

      <!-- Auxiliary Materials Category -->
      <div
        class="category-card"
        :class="{ selected: selectedCard === 'auxiliary_materials' }"
        @click="selectCategory('auxiliary_materials')"
      >
        <div class="category-icon">
          <el-icon :size="48"><Box /></el-icon>
        </div>
        <h4 class="category-title">{{ t("rfq.distributionCategory.auxiliaryMaterials") }}</h4>
        <p class="category-desc">{{ t("rfq.distributionForm.auxiliaryMaterialsDesc") }}</p>
      </div>

      <!-- Fixtures Category -->
      <div
        class="category-card"
        :class="{ selected: selectedCard === 'fixtures' }"
        @click="selectCategory('fixtures')"
      >
        <div class="category-icon">
          <el-icon :size="48"><Tools /></el-icon>
        </div>
        <h4 class="category-title">{{ t("rfq.distributionCategory.fixtures") }}</h4>
        <p class="category-desc">{{ t("rfq.distributionForm.fixturesDesc") }}</p>
      </div>

      <!-- Molds Category -->
      <div
        class="category-card"
        :class="{ selected: selectedCard === 'molds' }"
        @click="selectCategory('molds')"
      >
        <div class="category-icon">
          <el-icon :size="48"><Aim /></el-icon>
        </div>
        <h4 class="category-title">{{ t("rfq.distributionCategory.molds") }}</h4>
        <p class="category-desc">{{ t("rfq.distributionForm.moldsDesc") }}</p>
      </div>

      <!-- Blades Category -->
      <div
        class="category-card"
        :class="{ selected: selectedCard === 'blades' }"
        @click="selectCategory('blades')"
      >
        <div class="category-icon">
          <el-icon :size="48"><Scissor /></el-icon>
        </div>
        <h4 class="category-title">{{ t("rfq.distributionCategory.blades") }}</h4>
        <p class="category-desc">{{ t("rfq.distributionForm.bladesDesc") }}</p>
      </div>

      <!-- Hardware Category -->
      <div
        class="category-card"
        :class="{ selected: selectedCard === 'hardware' }"
        @click="selectCategory('hardware')"
      >
        <div class="category-icon">
          <el-icon :size="48"><Connection /></el-icon>
        </div>
        <h4 class="category-title">{{ t("rfq.distributionCategory.hardware") }}</h4>
        <p class="category-desc">{{ t("rfq.distributionForm.hardwareDesc") }}</p>
      </div>
    </div>

    <!-- Subcategory Selection (shown after category is selected) -->
    <transition name="fade">
      <div v-if="showsSubcategorySelector" class="subcategory-section">
        <h4 class="subcategory-title">{{ t("rfq.distributionForm.selectSubcategory") }}</h4>
        <div class="subcategory-grid">
          <div
            v-for="subcat in availableSubcategories"
            :key="subcat.value"
            class="subcategory-card"
            :class="{ selected: selectedSubcategory === subcat.value }"
            @click="selectSubcategory(subcat.value)"
          >
            <div class="subcategory-check">
              <el-icon v-if="selectedSubcategory === subcat.value" :size="20">
                <Check />
              </el-icon>
            </div>
            <span class="subcategory-label">{{ subcat.label }}</span>
          </div>
        </div>
      </div>
    </transition>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import { Setting, Box, Check, Tools, Aim, Scissor, Connection } from "@element-plus/icons-vue";
import type { RfqDistributionCategory, RfqDistributionSubcategory } from "@/types";

const { t } = useI18n();

interface Props {
  category?: RfqDistributionCategory | string | null;
  subcategory?: RfqDistributionSubcategory | string | null;
}

const props = defineProps<Props>();

const emit = defineEmits<{
  "update:category": [value: string];
  "update:subcategory": [value: string];
}>();

const directCategoryMap: Record<
  string,
  { category: RfqDistributionCategory | string; subcategory: RfqDistributionSubcategory | string }
> = {
  fixtures: { category: "equipment", subcategory: "fixtures" },
  molds: { category: "equipment", subcategory: "molds" },
  blades: { category: "equipment", subcategory: "blades" },
  hardware: { category: "auxiliary_materials", subcategory: "hardware" },
};

const selectedCard = computed(() => {
  const category = props.category || "";
  const subcategory = props.subcategory || "";

  for (const [card, mapping] of Object.entries(directCategoryMap)) {
    if (mapping.category === category && mapping.subcategory === subcategory) {
      return card;
    }
  }

  if (!category) {
    return "";
  }

  if (category === "equipment") {
    return "equipment";
  }

  if (category === "auxiliary_materials") {
    return "auxiliary_materials";
  }

  return category;
});

const selectedSubcategory = computed({
  get: () => props.subcategory || "",
  set: (value) => emit("update:subcategory", value || ""),
});

type DirectCategoryKey = keyof typeof directCategoryMap;

const isDirectCategory = (value: unknown): value is DirectCategoryKey => {
  return (
    typeof value === "string" && Object.prototype.hasOwnProperty.call(directCategoryMap, value)
  );
};

const showsSubcategorySelector = computed(() => {
  const card = selectedCard.value;
  if (!card) {
    return false;
  }
  return !isDirectCategory(card);
});

const availableSubcategories = computed(() => {
  const card = selectedCard.value;

  if (!card || !showsSubcategorySelector.value) {
    return [];
  }

  if (card === "equipment") {
    return [
      { value: "standard", label: t("rfq.distributionSubcategory.standard") },
      { value: "non_standard", label: t("rfq.distributionSubcategory.nonStandard") },
    ];
  }

  if (card === "auxiliary_materials") {
    return [
      { value: "labor_protection", label: t("rfq.distributionSubcategory.laborProtection") },
      { value: "office_supplies", label: t("rfq.distributionSubcategory.officeSupplies") },
      { value: "production_supplies", label: t("rfq.distributionSubcategory.productionSupplies") },
      { value: "accessories", label: t("rfq.distributionSubcategory.accessories") },
      { value: "others", label: t("rfq.distributionSubcategory.others") },
    ];
  }

  return [];
});

function selectCategory(card: string) {
  if (isDirectCategory(card)) {
    const direct = directCategoryMap[card];
    emit("update:category", direct.category);
    emit("update:subcategory", direct.subcategory);
    return;
  }

  emit("update:category", card);

  if (card === "equipment") {
    if (!props.subcategory || isDirectCategory(props.subcategory)) {
      emit("update:subcategory", "");
    }
    return;
  }

  if (card === "auxiliary_materials") {
    if (
      props.subcategory &&
      isDirectCategory(props.subcategory) &&
      directCategoryMap[props.subcategory].category === "auxiliary_materials"
    ) {
      emit("update:subcategory", "");
    }
    return;
  }

  emit("update:subcategory", "");
}

function selectSubcategory(subcategory: string) {
  selectedSubcategory.value = subcategory;
}
</script>

<style scoped>
.rfq-distribution-selector {
  padding: 24px;
  background: #f5f7fa;
  border-radius: 12px;
}

.selector-title {
  font-size: 20px;
  font-weight: 600;
  color: #303133;
  margin: 0 0 8px 0;
}

.selector-description {
  font-size: 14px;
  color: #606266;
  margin: 0 0 24px 0;
}

.category-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: 20px;
  margin-bottom: 32px;
}

.category-card {
  background: white;
  border: 2px solid #dcdfe6;
  border-radius: 12px;
  padding: 32px 24px;
  text-align: center;
  cursor: pointer;
  transition: all 0.3s ease;
}

.category-card:hover {
  border-color: #409eff;
  box-shadow: 0 4px 12px rgba(64, 158, 255, 0.15);
  transform: translateY(-2px);
}

.category-card.selected {
  border-color: #409eff;
  background: linear-gradient(135deg, #f0f7ff 0%, #e8f4ff 100%);
  box-shadow: 0 4px 16px rgba(64, 158, 255, 0.25);
}

.category-icon {
  color: #409eff;
  margin-bottom: 16px;
}

.category-card.selected .category-icon {
  color: #409eff;
  animation: pulse 1.5s ease-in-out infinite;
}

@keyframes pulse {
  0%,
  100% {
    transform: scale(1);
  }
  50% {
    transform: scale(1.1);
  }
}

.category-title {
  font-size: 18px;
  font-weight: 600;
  color: #303133;
  margin: 0 0 8px 0;
}

.category-desc {
  font-size: 14px;
  color: #909399;
  margin: 0;
  line-height: 1.6;
}

.subcategory-section {
  border-top: 2px solid #e4e7ed;
  padding-top: 24px;
}

.subcategory-title {
  font-size: 16px;
  font-weight: 600;
  color: #303133;
  margin: 0 0 16px 0;
}

.subcategory-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 12px;
}

.subcategory-card {
  background: white;
  border: 2px solid #dcdfe6;
  border-radius: 8px;
  padding: 16px;
  cursor: pointer;
  transition: all 0.2s ease;
  display: flex;
  align-items: center;
  gap: 12px;
}

.subcategory-card:hover {
  border-color: #409eff;
  background: #f0f7ff;
}

.subcategory-card.selected {
  border-color: #409eff;
  background: #409eff;
  color: white;
}

.subcategory-check {
  width: 24px;
  height: 24px;
  border-radius: 50%;
  border: 2px solid #dcdfe6;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  transition: all 0.2s ease;
}

.subcategory-card:hover .subcategory-check {
  border-color: #409eff;
}

.subcategory-card.selected .subcategory-check {
  border-color: white;
  background: white;
  color: #409eff;
}

.subcategory-label {
  font-size: 14px;
  font-weight: 500;
  flex: 1;
}

.subcategory-card.selected .subcategory-label {
  color: white;
}

.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.3s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

@media (max-width: 768px) {
  .category-grid {
    grid-template-columns: 1fr;
  }

  .subcategory-grid {
    grid-template-columns: 1fr;
  }
}
</style>
