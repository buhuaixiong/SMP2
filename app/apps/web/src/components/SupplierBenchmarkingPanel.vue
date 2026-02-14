<template>
  <div class="benchmarking-panel">
    <div class="panel-header">
      <div class="header-left">
        <h3>Supplier Benchmarking</h3>
        <p class="header-subtitle">Compare performance against industry standards</p>
      </div>
      <div class="header-right">
        <el-select
          v-model="selectedCategory"
          placeholder="Category"
          size="small"
          clearable
          style="width: 180px"
          @change="fetchBenchmarks"
        >
          <el-option label="All Categories" value="" />
          <el-option label="Manufacturing" value="manufacturing" />
          <el-option label="Services" value="services" />
          <el-option label="Trading" value="trading" />
          <el-option label="Technology" value="technology" />
          <el-option label="Construction" value="construction" />
        </el-select>
      </div>
    </div>

    <!-- Loading State -->
    <div v-if="loading" class="loading-container">
      <el-skeleton :rows="8" animated />
    </div>

    <!-- Main Content -->
    <div v-else-if="benchmarkData" class="benchmark-content">
      <!-- Overall Score Card -->
      <div class="score-card">
        <div class="score-main">
          <div class="score-circle">
            <el-progress
              type="circle"
              :percentage="currentSupplier.overallScore"
              :width="140"
              :stroke-width="10"
              :color="getScoreColor(currentSupplier.overallScore)"
            >
              <template #default="{ percentage }">
                <div class="score-content">
                  <span class="score-value">{{ percentage }}</span>
                  <span class="score-label">Overall</span>
                </div>
              </template>
            </el-progress>
          </div>

          <div class="score-details">
            <h4>Your Performance</h4>
            <div class="score-rank">
              <el-tag :type="getRankType(currentSupplier.percentile)" size="large">
                Top {{ currentSupplier.percentile }}%
              </el-tag>
            </div>
            <p class="score-description">
              You're performing better than {{ 100 - currentSupplier.percentile }}% of suppliers in
              your category
            </p>
          </div>
        </div>

        <el-divider />

        <div class="score-comparison">
          <div class="comparison-item">
            <span class="label">Category Average</span>
            <span class="value">{{ benchmarkData.categoryAverage.toFixed(1) }}</span>
            <el-icon
              :color="
                currentSupplier.overallScore > benchmarkData.categoryAverage ? '#67C23A' : '#F56C6C'
              "
            >
              <top v-if="currentSupplier.overallScore > benchmarkData.categoryAverage" />
              <bottom v-else />
            </el-icon>
          </div>
          <div class="comparison-item">
            <span class="label">Industry Average</span>
            <span class="value">{{ benchmarkData.industryAverage.toFixed(1) }}</span>
            <el-icon
              :color="
                currentSupplier.overallScore > benchmarkData.industryAverage ? '#67C23A' : '#F56C6C'
              "
            >
              <top v-if="currentSupplier.overallScore > benchmarkData.industryAverage" />
              <bottom v-else />
            </el-icon>
          </div>
        </div>
      </div>

      <!-- Metric Breakdown -->
      <div class="metrics-section">
        <h4>Performance Metrics</h4>
        <div class="metrics-grid">
          <div
            v-for="metric in metrics"
            :key="metric.key"
            class="metric-card"
            :class="getMetricClass(metric)"
          >
            <div class="metric-header">
              <div class="metric-icon" :style="{ background: metric.color + '20' }">
                <el-icon :size="24" :color="metric.color">
                  <component :is="metric.icon" />
                </el-icon>
              </div>
              <div class="metric-info">
                <h5>{{ metric.label }}</h5>
                <p class="metric-description">{{ metric.description }}</p>
              </div>
            </div>

            <div class="metric-score">
              <span class="score-number">{{ metric.yourScore }}</span>
              <span class="score-suffix">/100</span>
            </div>

            <el-progress :percentage="metric.yourScore" :color="metric.color" :show-text="false" />

            <div class="metric-comparison">
              <div class="comparison-row">
                <span class="comparison-label">Category Avg</span>
                <span class="comparison-value">{{ metric.categoryAvg }}</span>
                <el-tag
                  :type="metric.yourScore >= metric.categoryAvg ? 'success' : 'warning'"
                  size="small"
                >
                  {{ metric.yourScore >= metric.categoryAvg ? "+" : ""
                  }}{{ (metric.yourScore - metric.categoryAvg).toFixed(1) }}
                </el-tag>
              </div>
              <div class="comparison-row">
                <span class="comparison-label">Top 10% Avg</span>
                <span class="comparison-value">{{ metric.top10Avg }}</span>
                <span
                  class="gap-indicator"
                  :class="{ 'is-close': metric.yourScore >= metric.top10Avg * 0.9 }"
                >
                  {{
                    metric.yourScore >= metric.top10Avg
                      ? "Achieved"
                      : "Gap: " + (metric.top10Avg - metric.yourScore).toFixed(1)
                  }}
                </span>
              </div>
            </div>

            <!-- Improvement Tips -->
            <div v-if="metric.tips && metric.yourScore < metric.top10Avg" class="metric-tips">
              <el-icon :size="14"><info-filled /></el-icon>
              <span>{{ metric.tips }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- Distribution Chart -->
      <div class="distribution-section">
        <h4>Performance Distribution</h4>
        <p class="section-subtitle">See where you stand among all suppliers</p>

        <div class="distribution-chart">
          <div v-for="(bucket, index) in distributionBuckets" :key="index" class="distribution-bar">
            <div class="bar-container">
              <div
                class="bar-fill"
                :style="{
                  height: (bucket.count / maxBucketCount) * 100 + '%',
                  background: bucket.isYou ? '#409EFF' : '#E4E7ED',
                }"
              >
                <span v-if="bucket.isYou" class="you-marker">YOU</span>
              </div>
            </div>
            <div class="bar-label">{{ bucket.label }}</div>
            <div class="bar-count">{{ bucket.count }}</div>
          </div>
        </div>

        <div class="distribution-legend">
          <div class="legend-item">
            <span class="legend-color" style="background: #409eff"></span>
            <span>Your Position</span>
          </div>
          <div class="legend-item">
            <span class="legend-color" style="background: #e4e7ed"></span>
            <span>Other Suppliers</span>
          </div>
        </div>
      </div>

      <!-- Improvement Recommendations -->
      <div class="recommendations-section">
        <h4>Improvement Recommendations</h4>
        <div class="recommendations-list">
          <div v-for="(rec, index) in recommendations" :key="index" class="recommendation-card">
            <div class="rec-priority" :class="'priority-' + rec.priority">
              <el-icon><flag /></el-icon>
              <span>{{ rec.priority }}</span>
            </div>
            <div class="rec-content">
              <h5>{{ rec.title }}</h5>
              <p>{{ rec.description }}</p>
              <div class="rec-impact">
                <el-icon :size="14"><trend-charts /></el-icon>
                <span>Potential score improvement: +{{ rec.impact }} points</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Historical Trend -->
      <div class="trend-section">
        <h4>Performance Trend</h4>
        <p class="section-subtitle">Your progress over time</p>

        <div class="trend-chart">
          <div class="trend-line">
            <div
              v-for="(point, index) in trendData"
              :key="index"
              class="trend-point"
              :style="{
                left: (index / (trendData.length - 1)) * 100 + '%',
                bottom: (point.score / 100) * 100 + '%',
              }"
            >
              <el-tooltip :content="`${point.period}: ${point.score}`" placement="top">
                <div class="point-marker"></div>
              </el-tooltip>
            </div>
          </div>
          <div class="trend-labels">
            <span v-for="(point, index) in trendData" :key="index" class="trend-label">
              {{ point.period }}
            </span>
          </div>
        </div>

        <div class="trend-summary">
          <el-statistic
            title="Change vs Last Period"
            :value="trendChange"
            :prefix="trendChange > 0 ? '+' : ''"
            suffix="pts"
          >
            <template #suffix>
              <el-icon :color="trendChange > 0 ? '#67C23A' : '#F56C6C'">
                <top v-if="trendChange > 0" />
                <bottom v-else />
              </el-icon>
            </template>
          </el-statistic>
        </div>
      </div>
    </div>

    <!-- Empty State -->
    <div v-else class="empty-state">
      <el-empty description="No benchmarking data available">
        <el-button type="primary" @click="fetchBenchmarks">Refresh Data</el-button>
      </el-empty>
    </div>
  </div>
</template>

<script setup lang="ts">




import { ref, computed, onMounted } from "vue";
import {
  Top,
  Bottom,
  InfoFilled,
  Flag,
  TrendCharts,
  Timer,
  Medal,
  Coin,
  Star,
} from "@element-plus/icons-vue";



import { useNotification } from "@/composables";
const notification = useNotification();

interface Props {
  supplierId: number;
}

const props = defineProps<Props>();

const loading = ref(false);
const selectedCategory = ref("");
const benchmarkData = ref<any>(null);

const currentSupplier = computed(() => ({
  overallScore: 85,
  percentile: 15,
  rank: 45,
  totalSuppliers: 300,
}));

const metrics = computed(() => [
  {
    key: "onTimeDelivery",
    label: "On-Time Delivery",
    description: "Percentage of deliveries made on schedule",
    yourScore: 88,
    categoryAvg: 75,
    top10Avg: 95,
    color: "#67C23A",
    icon: Timer,
    tips: "Focus on better production planning and logistics coordination",
  },
  {
    key: "quality",
    label: "Quality Score",
    description: "Product quality and defect rates",
    yourScore: 92,
    categoryAvg: 80,
    top10Avg: 96,
    color: "#409EFF",
    icon: Medal,
    tips: "Implement stricter quality control processes",
  },
  {
    key: "cost",
    label: "Cost Competitiveness",
    description: "Pricing compared to market rates",
    yourScore: 78,
    categoryAvg: 82,
    top10Avg: 90,
    color: "#E6A23C",
    icon: Coin,
    tips: "Optimize supply chain and reduce operational costs",
  },
  {
    key: "service",
    label: "Service Quality",
    description: "Responsiveness and customer satisfaction",
    yourScore: 85,
    categoryAvg: 78,
    top10Avg: 93,
    color: "#F56C6C",
    icon: Star,
    tips: "Improve response times and communication",
  },
]);

const distributionBuckets = computed(() => [
  { label: "0-20", count: 15, isYou: false },
  { label: "21-40", count: 45, isYou: false },
  { label: "41-60", count: 78, isYou: false },
  { label: "61-80", count: 95, isYou: false },
  { label: "81-100", count: 67, isYou: true },
]);

const maxBucketCount = computed(() => Math.max(...distributionBuckets.value.map((b) => b.count)));

const recommendations = computed(() => [
  {
    priority: "high",
    title: "Improve Cost Competitiveness",
    description:
      "Your pricing is 4% above category average. Consider reviewing your cost structure and identifying efficiency improvements.",
    impact: 8,
  },
  {
    priority: "medium",
    title: "Enhance Service Response Time",
    description:
      "Average response time to inquiries is 8 hours above the top performers. Implement automated acknowledgment systems.",
    impact: 5,
  },
  {
    priority: "low",
    title: "Maintain Quality Leadership",
    description:
      "Your quality scores are excellent. Continue current practices and consider documenting best practices for consistency.",
    impact: 2,
  },
]);

const trendData = computed(() => [
  { period: "Q1", score: 78 },
  { period: "Q2", score: 81 },
  { period: "Q3", score: 83 },
  { period: "Q4", score: 85 },
]);

const trendChange = computed(() => {
  if (trendData.value.length < 2) return 0;
  const latest = trendData.value[trendData.value.length - 1].score;
  const previous = trendData.value[trendData.value.length - 2].score;
  return latest - previous;
});

const getScoreColor = (score: number) => {
  if (score >= 80) return "#67C23A";
  if (score >= 60) return "#E6A23C";
  return "#F56C6C";
};

const getRankType = (percentile: number) => {
  if (percentile <= 10) return "success";
  if (percentile <= 25) return "warning";
  return "info";
};

const getMetricClass = (metric: any) => {
  if (metric.yourScore >= metric.top10Avg) return "metric-excellent";
  if (metric.yourScore >= metric.categoryAvg) return "metric-good";
  return "metric-needs-improvement";
};

const fetchBenchmarks = async () => {
  loading.value = true;
  try {
    // TODO: Replace with actual API call
    // const response = await apiFetch(`/suppliers/benchmarks?supplierId=${props.supplierId}&category=${selectedCategory.value}`)
    // benchmarkData.value = response.data

    // Simulated data for now
    await new Promise((resolve) => setTimeout(resolve, 1000));
    benchmarkData.value = {
      categoryAverage: 75,
      industryAverage: 72,
      totalSuppliers: 300,
      yourRank: 45,
    };
  } catch (error) {
    console.error("Failed to fetch benchmarks:", error);
    notification.error("Failed to load benchmarking data");
  } finally {
    loading.value = false;
  }
};

onMounted(() => {
  fetchBenchmarks();
});




</script>

<style scoped>
.benchmarking-panel {
  background: white;
  border-radius: 12px;
  padding: 24px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.08);
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 24px;
  padding-bottom: 16px;
  border-bottom: 2px solid #ebeef5;
}

.header-left h3 {
  margin: 0 0 4px;
  font-size: 20px;
  font-weight: 600;
  color: #303133;
}

.header-subtitle {
  margin: 0;
  font-size: 14px;
  color: #909399;
}

.loading-container {
  padding: 40px 0;
}

.benchmark-content {
  display: flex;
  flex-direction: column;
  gap: 32px;
}

.score-card {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  border-radius: 12px;
  padding: 32px;
  color: white;
}

.score-main {
  display: flex;
  align-items: center;
  gap: 32px;
}

.score-circle {
  flex-shrink: 0;
}

.score-content {
  display: flex;
  flex-direction: column;
  align-items: center;
}

.score-value {
  font-size: 32px;
  font-weight: 700;
}

.score-label {
  font-size: 12px;
  margin-top: 4px;
}

.score-details h4 {
  margin: 0 0 12px;
  font-size: 18px;
}

.score-rank {
  margin-bottom: 12px;
}

.score-description {
  margin: 0;
  font-size: 14px;
  opacity: 0.9;
}

.score-comparison {
  display: flex;
  gap: 32px;
}

.comparison-item {
  display: flex;
  align-items: center;
  gap: 12px;
}

.comparison-item .label {
  font-size: 14px;
  opacity: 0.9;
}

.comparison-item .value {
  font-size: 20px;
  font-weight: 600;
}

.metrics-section h4,
.distribution-section h4,
.recommendations-section h4,
.trend-section h4 {
  margin: 0 0 16px;
  font-size: 18px;
  font-weight: 600;
  color: #303133;
}

.section-subtitle {
  margin: -8px 0 16px;
  font-size: 14px;
  color: #909399;
}

.metrics-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: 16px;
}

.metric-card {
  border: 1px solid #ebeef5;
  border-radius: 8px;
  padding: 20px;
  background: white;
  transition: all 0.3s;
}

.metric-card:hover {
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
  transform: translateY(-2px);
}

.metric-card.metric-excellent {
  border-color: #67c23a;
}

.metric-card.metric-good {
  border-color: #409eff;
}

.metric-card.metric-needs-improvement {
  border-color: #e6a23c;
}

.metric-header {
  display: flex;
  gap: 12px;
  margin-bottom: 16px;
}

.metric-icon {
  width: 48px;
  height: 48px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.metric-info h5 {
  margin: 0 0 4px;
  font-size: 15px;
  font-weight: 600;
  color: #303133;
}

.metric-description {
  margin: 0;
  font-size: 12px;
  color: #909399;
}

.metric-score {
  margin-bottom: 12px;
}

.score-number {
  font-size: 32px;
  font-weight: 700;
  color: #303133;
}

.score-suffix {
  font-size: 16px;
  color: #909399;
  margin-left: 2px;
}

.metric-comparison {
  margin-top: 16px;
  padding-top: 16px;
  border-top: 1px solid #ebeef5;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.comparison-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: 13px;
}

.comparison-label {
  color: #606266;
}

.comparison-value {
  font-weight: 500;
  color: #303133;
  margin-right: 8px;
}

.gap-indicator {
  font-size: 12px;
  color: #e6a23c;
}

.gap-indicator.is-close {
  color: #67c23a;
}

.metric-tips {
  margin-top: 12px;
  padding: 8px 12px;
  background: #fdf6ec;
  border-radius: 4px;
  display: flex;
  align-items: flex-start;
  gap: 6px;
  font-size: 12px;
  color: #e6a23c;
}

.distribution-chart {
  display: flex;
  align-items: flex-end;
  justify-content: space-around;
  height: 200px;
  padding: 20px;
  background: #fafafa;
  border-radius: 8px;
  margin-bottom: 16px;
}

.distribution-bar {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
}

.bar-container {
  width: 40px;
  height: 160px;
  background: #f5f7fa;
  border-radius: 4px;
  display: flex;
  align-items: flex-end;
  overflow: hidden;
}

.bar-fill {
  width: 100%;
  transition: height 0.5s ease;
  border-radius: 4px 4px 0 0;
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
}

.you-marker {
  position: absolute;
  top: -20px;
  font-size: 10px;
  font-weight: 700;
  color: #409eff;
}

.bar-label {
  font-size: 12px;
  color: #606266;
}

.bar-count {
  font-size: 11px;
  color: #909399;
}

.distribution-legend {
  display: flex;
  justify-content: center;
  gap: 24px;
}

.legend-item {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  color: #606266;
}

.legend-color {
  width: 16px;
  height: 16px;
  border-radius: 3px;
}

.recommendations-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.recommendation-card {
  display: flex;
  gap: 16px;
  padding: 16px;
  border: 1px solid #ebeef5;
  border-radius: 8px;
  background: white;
}

.rec-priority {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
  padding: 8px;
  border-radius: 6px;
  font-size: 11px;
  font-weight: 600;
  text-transform: uppercase;
  min-width: 70px;
}

.rec-priority.priority-high {
  background: #fef0f0;
  color: #f56c6c;
}

.rec-priority.priority-medium {
  background: #fdf6ec;
  color: #e6a23c;
}

.rec-priority.priority-low {
  background: #f0f9ff;
  color: #409eff;
}

.rec-content {
  flex: 1;
}

.rec-content h5 {
  margin: 0 0 8px;
  font-size: 15px;
  font-weight: 600;
  color: #303133;
}

.rec-content p {
  margin: 0 0 12px;
  font-size: 13px;
  color: #606266;
  line-height: 1.6;
}

.rec-impact {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  color: #67c23a;
  font-weight: 500;
}

.trend-chart {
  position: relative;
  height: 200px;
  padding: 20px;
  background: #fafafa;
  border-radius: 8px;
  margin-bottom: 16px;
}

.trend-line {
  position: relative;
  height: 160px;
}

.trend-point {
  position: absolute;
  transform: translate(-50%, 50%);
}

.point-marker {
  width: 12px;
  height: 12px;
  background: #409eff;
  border: 3px solid white;
  border-radius: 50%;
  cursor: pointer;
  transition: all 0.3s;
}

.point-marker:hover {
  transform: scale(1.5);
  box-shadow: 0 0 0 4px rgba(64, 158, 255, 0.2);
}

.trend-labels {
  display: flex;
  justify-content: space-around;
  margin-top: 8px;
}

.trend-label {
  font-size: 12px;
  color: #909399;
}

.trend-summary {
  text-align: center;
}

.empty-state {
  padding: 60px 20px;
  text-align: center;
}

@media (max-width: 768px) {
  .score-main {
    flex-direction: column;
    text-align: center;
  }

  .score-comparison {
    flex-direction: column;
    gap: 16px;
  }

  .metrics-grid {
    grid-template-columns: 1fr;
  }

  .recommendation-card {
    flex-direction: column;
  }
}
</style>
