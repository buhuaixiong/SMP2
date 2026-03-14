<template>
  <div class="rfq-price-comparison-table">
    <div class="comparison-header">
      <h3>{{ t("rfq.priceComparison.title") }}</h3>
    </div>

    <RfqRoundComparisonTable :line-items="lineItems" :quotes="quotes" />
  </div>
</template>

<script setup lang="ts">
import { useI18n } from "vue-i18n";
import RfqRoundComparisonTable from "./RfqRoundComparisonTable.vue";
import type { Quote, RfqItem, RfqQuoteItem } from "@/types";

type LineItemLike = RfqItem & {
  id: number;
};

type QuoteItemLike = RfqQuoteItem & {
  lineItemId?: number | string | null;
  line_item_id?: number | string | null;
  rfqLineItemId?: number | string | null;
  rfq_line_item_id?: number | string | null;
  rfqItemId?: number | string | null;
  rfq_item_id?: number | string | null;
};

type QuoteLike = Quote & {
  items?: QuoteItemLike[];
  quoteItems?: QuoteItemLike[];
};

interface Props {
  lineItems: LineItemLike[];
  quotes: QuoteLike[];
}

defineProps<Props>();

defineEmits(["refresh"]);

const { t } = useI18n();
</script>

<style scoped lang="scss">
.rfq-price-comparison-table {
  margin-top: 20px;

  .comparison-header {
    margin-bottom: 16px;

    h3 {
      font-size: 18px;
      font-weight: 600;
      margin: 0 0 8px 0;
      color: #303133;
    }
  }
}
</style>
