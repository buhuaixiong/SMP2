<template>
  <el-form-item
    :label="label"
    :prop="prop"
    :required="required"
    :rules="computedRules"
    :class="['form-field-with-help', { 'has-error': hasError, 'is-valid': isValid }]"
  >
    <!-- Slot for the input field -->
    <slot></slot>

    <!-- Help tooltip icon -->
    <template v-if="helpText || helpUrl" #label>
      <span class="field-label-with-help">
        {{ label }}
        <el-tooltip
          :content="helpText"
          placement="top"
          :show-after="300"
          :popper-options="{ modifiers: [{ name: 'offset', options: { offset: [0, 8] } }] }"
        >
          <el-icon class="help-icon" :size="16" color="#909399">
            <question-filled />
          </el-icon>
        </el-tooltip>
      </span>
    </template>

    <!-- Inline validation feedback -->
    <template #error="{ error }">
      <div class="validation-feedback error-feedback">
        <el-icon><circle-close /></el-icon>
        <span>{{ error }}</span>
      </div>
    </template>

    <!-- Success indicator -->
    <transition name="fade">
      <div
        v-if="showSuccessIndicator && isValid && modelValue"
        class="validation-feedback success-feedback"
      >
        <el-icon><circle-check /></el-icon>
        <span>Looks good!</span>
      </div>
    </transition>

    <!-- Field hint/description -->
    <div v-if="hint" class="field-hint">
      <el-icon :size="14"><info-filled /></el-icon>
      <span>{{ hint }}</span>
    </div>

    <!-- Smart suggestions -->
    <transition name="slide-down">
      <div v-if="suggestions.length > 0 && showSuggestions" class="field-suggestions">
        <div class="suggestions-header">
          <el-icon :size="14"><magic-stick /></el-icon>
          <span>Suggestions:</span>
        </div>
        <div class="suggestions-list">
          <div
            v-for="(suggestion, index) in suggestions"
            :key="index"
            class="suggestion-item"
            @click="applySuggestion(suggestion)"
          >
            <el-icon :size="12"><arrow-right /></el-icon>
            <span>{{ suggestion.text }}</span>
          </div>
        </div>
      </div>
    </transition>

    <!-- Help link -->
    <div v-if="helpUrl" class="field-help-link">
      <a :href="helpUrl" target="_blank" rel="noopener noreferrer">
        <el-icon :size="14"><link /></el-icon>
        <span>Learn more</span>
      </a>
    </div>

    <!-- Character counter -->
    <div v-if="showCounter && maxLength" class="character-counter">
      <span :class="{ 'counter-warning': characterCount > maxLength * 0.9 }">
        {{ characterCount }} / {{ maxLength }}
      </span>
    </div>

    <!-- Real-time validation messages -->
    <transition name="fade">
      <div v-if="liveValidationMessage" class="live-validation-message">
        <el-icon
          :size="14"
          :color="liveValidationMessageType === 'warning' ? '#E6A23C' : '#909399'"
        >
          <warning v-if="liveValidationMessageType === 'warning'" />
          <info-filled v-else />
        </el-icon>
        <span>{{ liveValidationMessage }}</span>
      </div>
    </transition>
  </el-form-item>
</template>

<script setup lang="ts">
import { ref, computed, watch } from "vue";
import {
  QuestionFilled,
  CircleClose,
  CircleCheck,
  InfoFilled,
  MagicStick,
  ArrowRight,
  Link,
  Warning,
} from "@element-plus/icons-vue";
import type { FormItemRule } from "element-plus";

interface Suggestion {
  text: string;
  value?: any;
}

interface Props {
  label: string;
  prop?: string;
  required?: boolean;
  rules?: FormItemRule[];
  helpText?: string;
  helpUrl?: string;
  hint?: string;
  modelValue?: any;
  showSuccessIndicator?: boolean;
  showCounter?: boolean;
  maxLength?: number;
  suggestions?: Suggestion[];
  validationType?: "email" | "phone" | "url" | "number" | "text";
  customValidator?: (value: any) => boolean | string;
}

const props = withDefaults(defineProps<Props>(), {
  required: false,
  rules: () => [],
  helpText: "",
  helpUrl: "",
  hint: "",
  modelValue: "",
  showSuccessIndicator: false,
  showCounter: false,
  maxLength: undefined,
  suggestions: () => [],
  validationType: "text",
  customValidator: undefined,
});

const emit = defineEmits<{
  (e: "update:modelValue", value: any): void;
  (e: "suggestion-applied", suggestion: Suggestion): void;
}>();

const hasError = ref(false);
const isValid = ref(false);
const showSuggestions = ref(false);
const liveValidationMessage = ref("");
const liveValidationMessageType = ref<"warning" | "info">("info");

const characterCount = computed(() => {
  return String(props.modelValue || "").length;
});

const computedRules = computed(() => {
  const rules: FormItemRule[] = [...props.rules];

  // Add built-in validation rules based on type
  if (props.validationType === "email") {
    rules.push({
      type: "email",
      message: "Please enter a valid email address",
      trigger: ["blur", "change"],
    });
  } else if (props.validationType === "phone") {
    rules.push({
      pattern: /^[\d\s\-\+\(\)]+$/,
      message: "Please enter a valid phone number",
      trigger: ["blur", "change"],
    });
  } else if (props.validationType === "url") {
    rules.push({
      type: "url",
      message: "Please enter a valid URL",
      trigger: ["blur", "change"],
    });
  } else if (props.validationType === "number") {
    rules.push({
      type: "number",
      message: "Please enter a valid number",
      trigger: ["blur", "change"],
    });
  }

  // Add required rule
  if (props.required) {
    rules.push({
      required: true,
      message: `${props.label} is required`,
      trigger: ["blur", "change"],
    });
  }

  // Add max length rule
  if (props.maxLength) {
    rules.push({
      max: props.maxLength,
      message: `Maximum ${props.maxLength} characters allowed`,
      trigger: ["blur", "change"],
    });
  }

  // Add custom validator
  if (props.customValidator) {
    rules.push({
      validator: (_rule, value, callback) => {
        const result = props.customValidator!(value);
        if (result === true) {
          callback();
        } else if (typeof result === "string") {
          callback(new Error(result));
        } else {
          callback(new Error("Validation failed"));
        }
      },
      trigger: ["blur", "change"],
    });
  }

  return rules;
});

const applySuggestion = (suggestion: Suggestion) => {
  const value = suggestion.value !== undefined ? suggestion.value : suggestion.text;
  emit("update:modelValue", value);
  emit("suggestion-applied", suggestion);
  showSuggestions.value = false;
};

// Watch for value changes to provide real-time feedback
watch(
  () => props.modelValue,
  (newValue) => {
    if (!newValue) {
      liveValidationMessage.value = "";
      isValid.value = false;
      hasError.value = false;
      showSuggestions.value = false;
      return;
    }

    // Email-specific feedback
    if (props.validationType === "email") {
      const emailValue = String(newValue);
      if (emailValue && !emailValue.includes("@")) {
        liveValidationMessage.value = "Email should contain @";
        liveValidationMessageType.value = "warning";
      } else if (emailValue && emailValue.includes("@") && !emailValue.includes(".")) {
        liveValidationMessage.value = "Email should contain a domain (e.g., @company.com)";
        liveValidationMessageType.value = "warning";
      } else if (emailValue && /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(emailValue)) {
        liveValidationMessage.value = "";
        isValid.value = true;
      }
    }

    // Phone-specific feedback
    if (props.validationType === "phone") {
      const phoneValue = String(newValue);
      const digitCount = phoneValue.replace(/\D/g, "").length;
      if (digitCount < 7) {
        liveValidationMessage.value = "Phone number seems too short";
        liveValidationMessageType.value = "warning";
      } else if (digitCount > 15) {
        liveValidationMessage.value = "Phone number seems too long";
        liveValidationMessageType.value = "warning";
      } else {
        liveValidationMessage.value = "";
        isValid.value = true;
      }
    }

    // Character count warning
    if (props.maxLength && characterCount.value > props.maxLength * 0.9) {
      const remaining = props.maxLength - characterCount.value;
      if (remaining > 0) {
        liveValidationMessage.value = `${remaining} characters remaining`;
        liveValidationMessageType.value = "info";
      }
    }

    // Show suggestions when field has focus and value is short
    if (
      props.suggestions.length > 0 &&
      String(newValue).length > 0 &&
      String(newValue).length < 3
    ) {
      showSuggestions.value = true;
    }
  },
  { immediate: true },
);
</script>

<style scoped>
.form-field-with-help {
  position: relative;
}

.field-label-with-help {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.help-icon {
  cursor: help;
  transition: color 0.3s;
}

.help-icon:hover {
  color: #409eff !important;
}

.validation-feedback {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  margin-top: 4px;
  padding: 4px 0;
}

.error-feedback {
  color: #f56c6c;
}

.success-feedback {
  color: #67c23a;
}

.field-hint {
  display: flex;
  align-items: flex-start;
  gap: 4px;
  font-size: 12px;
  color: #909399;
  margin-top: 6px;
  line-height: 1.4;
}

.field-suggestions {
  margin-top: 8px;
  padding: 12px;
  background: #f0f9ff;
  border: 1px solid #c6e2ff;
  border-radius: 6px;
}

.suggestions-header {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  font-weight: 500;
  color: #409eff;
  margin-bottom: 8px;
}

.suggestions-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.suggestion-item {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 8px;
  background: white;
  border-radius: 4px;
  cursor: pointer;
  font-size: 13px;
  transition: all 0.2s;
}

.suggestion-item:hover {
  background: #e6f4ff;
  color: #409eff;
  transform: translateX(4px);
}

.field-help-link {
  margin-top: 6px;
}

.field-help-link a {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 12px;
  color: #409eff;
  text-decoration: none;
  transition: color 0.3s;
}

.field-help-link a:hover {
  color: #66b1ff;
  text-decoration: underline;
}

.character-counter {
  text-align: right;
  font-size: 12px;
  color: #909399;
  margin-top: 4px;
}

.counter-warning {
  color: #e6a23c;
  font-weight: 500;
}

.live-validation-message {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: #606266;
  margin-top: 6px;
  padding: 4px 8px;
  background: #f5f7fa;
  border-radius: 4px;
}

/* Animations */
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.3s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

.slide-down-enter-active,
.slide-down-leave-active {
  transition: all 0.3s ease;
  max-height: 200px;
  overflow: hidden;
}

.slide-down-enter-from,
.slide-down-leave-to {
  max-height: 0;
  opacity: 0;
}

/* Form item states */
.form-field-with-help.has-error :deep(.el-input__wrapper) {
  box-shadow: 0 0 0 1px #f56c6c inset;
}

.form-field-with-help.is-valid :deep(.el-input__wrapper) {
  box-shadow: 0 0 0 1px #67c23a inset;
}
</style>
