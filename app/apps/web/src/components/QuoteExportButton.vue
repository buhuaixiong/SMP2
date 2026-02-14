<template>
  <el-dropdown @command="handleExport" trigger="click">
    <el-button :type="type" :size="size" :loading="exporting">
      Export Quote
      <el-icon class="el-icon--right"><Download /></el-icon>
    </el-button>
    <template #dropdown>
      <el-dropdown-menu>
        <el-dropdown-item command="json">
          <el-icon><Document /></el-icon>
          Export as JSON
        </el-dropdown-item>
        <el-dropdown-item command="excel">
          <el-icon><DocumentCopy /></el-icon>
          Export as Excel
        </el-dropdown-item>
        <el-dropdown-item command="pdf" disabled>
          <el-icon><Tickets /></el-icon>
          Export as PDF (Coming Soon)
        </el-dropdown-item>
      </el-dropdown-menu>
    </template>
  </el-dropdown>
</template>

<script setup lang="ts">




import { ref, onMounted, onUnmounted, nextTick } from "vue";

import { Download, Document, DocumentCopy, Tickets } from "@element-plus/icons-vue";
import { exportQuote } from "@/api/rfq";
import ExcelJS from "exceljs";


import { useNotification } from "@/composables";

const notification = useNotification();
interface Props {
  rfqId: number;
  quoteId: number;
  type?: "primary" | "success" | "warning" | "danger" | "info" | "default";
  size?: "large" | "default" | "small";
}

const props = withDefaults(defineProps<Props>(), {
  type: "default",
  size: "default",
});

const exporting = ref(false);
const isMounted = ref(false);

const ensureDomReady = async () => {
  if (!isMounted.value || typeof document === "undefined" || typeof window === "undefined") {
    return false;
  }
  await nextTick();
  return true;
};

onMounted(() => {
  isMounted.value = true;
});

onUnmounted(() => {
  isMounted.value = false;
});

async function handleExport(format: "json" | "excel" | "pdf") {
  exporting.value = true;
  try {
    const data = await exportQuote(props.rfqId, props.quoteId, format);

    if (format === "json") {
      await downloadJSON(data);
    } else if (format === "excel") {
      generateExcel(data);
    } else if (format === "pdf") {
      generatePDF(data);
    }

    notification.success(`Quote exported successfully as ${format.toUpperCase()}`);
  } catch (error: any) {
    notification.error(error.message || "Failed to export quote");
  } finally {
    exporting.value = false;
  }
}

async function downloadJSON(data: any) {
  if (!(await ensureDomReady())) {
    throw new Error("DOM is not available for download.");
  }
  const blob = new Blob([JSON.stringify(data, null, 2)], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `quote-${props.quoteId}-${Date.now()}.json`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}

function generateExcel(data: any) {
  try {
    const workbook = new ExcelJS.Workbook();
    const worksheet = workbook.addWorksheet("Quote Details");

    const quoteData: Array<Array<string | number>> = [
      ["Quote Export"],
      [""],
      ["RFQ Information"],
      ["RFQ ID", data.rfq.id],
      ["RFQ Number", data.rfq.rfq_number],
      ["Title", data.rfq.title],
      ["Material Type", data.rfq.material_type],
      ["Status", data.rfq.status],
      [""],
      ["Quote Information"],
      ["Quote ID", data.quote.id],
      ["Supplier", data.quote.supplier_name || data.quote.supplier_id],
      ["Contact Person", data.quote.contactPerson || "-"],
      ["Unit Price", `${data.quote.currency || "CNY"} ${data.quote.unit_price}`],
      ["Total Amount", `${data.quote.currency || "CNY"} ${data.quote.total_amount}`],
      ["Delivery Time", data.quote.delivery_time || "-"],
      ["Payment Terms", data.quote.payment_terms || "-"],
      ["Warranty", data.quote.warranty || "-"],
      ["Status", data.quote.status],
      ["Submitted At", data.quote.created_at],
      [""],
    ];

    // Add price comparisons if available
    if (data.priceComparisons && data.priceComparisons.length > 0) {
      quoteData.push(["Price Comparisons"]);
      quoteData.push(["Platform", "Online Price", "Variance %", "Product URL", "Notes"]);

      data.priceComparisons.forEach((comp: any) => {
        quoteData.push([
          comp.online_platform,
          `${comp.online_currency} ${comp.online_price}`,
          `${comp.price_variance_percent}%`,
          comp.product_url || "-",
          comp.comparison_notes || "-",
        ]);
      });
      quoteData.push([""]);
    }

    // Add review information if available
    if (data.review) {
      quoteData.push(["Review Information"]);
      quoteData.push(["Selected Quote ID", data.review.selected_quote_id]);
      quoteData.push(["Quality Score", data.review.quality_score || "-"]);
      quoteData.push(["Price Score", data.review.price_score || "-"]);
      quoteData.push(["Delivery Score", data.review.delivery_score || "-"]);
      quoteData.push(["Service Score", data.review.service_score || "-"]);
      quoteData.push(["Overall Score", data.review.overall_score || "-"]);
      quoteData.push(["Comments", data.review.comments || "-"]);
      quoteData.push(["Reviewed By", data.review.reviewed_by_name || data.review.reviewed_by_id]);
      quoteData.push(["Reviewed At", data.review.reviewed_at]);
    }

    // Add export metadata
    quoteData.push([""]);
    quoteData.push(["Export Information"]);
    quoteData.push(["Exported At", data.exportedAt]);
    quoteData.push(["Exported By", data.exportedBy]);

    quoteData.forEach((row) => worksheet.addRow(row));
    worksheet.columns = [
      { width: 20 },
      { width: 30 },
      { width: 20 },
      { width: 40 },
    ];

    workbook.xlsx.writeBuffer().then(async (buffer) => {
      if (!(await ensureDomReady())) {
        throw new Error("DOM is not available for download.");
      }
      const blob = new Blob([buffer], {
        type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
      });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `quote-${props.quoteId}-${Date.now()}.xlsx`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    });
  } catch (error) {
    console.error("Excel generation error:", error);
    notification.error("Failed to generate Excel file");
  }
}

function generatePDF(data: any) {
  // PDF generation using jsPDF will be implemented later
  notification.info("PDF export coming soon");
  console.log("PDF export data:", data);
}




</script>

<style scoped>
/* No additional styles needed */
</style>
