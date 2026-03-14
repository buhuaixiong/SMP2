<template>
  <div class="upload-block">
    <label :for="inputId" class="upload-label" :class="{ disabled }">
      <input
        ref="inputRef"
        type="file"
        :id="inputId"
        :disabled="disabled"
        :accept="accept"
        @change="handleFileChange"
      />
      <span>{{ currentLabel }}</span>
    </label>
    <p class="hint">{{ hint }}</p>
    <p v-if="error" class="error-text">{{ error }}</p>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";

defineOptions({ name: "TemplateUploadPanel" });

const props = withDefaults(
  defineProps<{
    templateCode: string;
    loading?: boolean;
    uploadingCode?: string | null;
    uploadingLabel?: string;
    idleLabel?: string;
    inputId?: string;
    accept?: string;
    hint?: string;
    error?: string | null;
  }>(),
  {
    loading: false,
    uploadingCode: null,
    uploadingLabel: "Uploading…",
    idleLabel: "Upload new version",
    inputId: "",
    accept: ".pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.zip,.rar,.7z,.png,.jpg,.jpeg",
    hint: "",
    error: null,
  },
);

const emit = defineEmits<{
  (event: "fileSelected", payload: { code: string; file: File }): void;
}>();

const inputRef = ref<HTMLInputElement | null>(null);

const disabled = computed(
  () => props.loading || (!!props.uploadingCode && props.uploadingCode === props.templateCode),
);

const currentLabel = computed(() =>
  disabled.value ? props.uploadingLabel : props.idleLabel,
);

const inputId = computed(
  () => props.inputId || `template-${props.templateCode}`,
);

const accept = computed(() => props.accept);
const hint = computed(() => props.hint);
const error = computed(() => props.error);

const resetField = () => {
  if (inputRef.value) {
    inputRef.value.value = "";
  }
};

watch(
  () => props.uploadingCode,
  (value, oldValue) => {
    if (oldValue === props.templateCode && value !== oldValue) {
      resetField();
    }
  },
);

watch(
  () => props.loading,
  (value, oldValue) => {
    if (oldValue && !value) {
      resetField();
    }
  },
);

const handleFileChange = (event: Event) => {
  const target = event.target as HTMLInputElement;
  const file = target.files?.[0];
  if (file) {
    emit("fileSelected", { code: props.templateCode, file });
  }
};
</script>

<style scoped>
.upload-block {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.upload-label {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  background: #eef1ff;
  color: #3743b3;
  border-radius: 10px;
  padding: 8px 14px;
  cursor: pointer;
  border: 1px solid rgba(78, 98, 210, 0.24);
}

.upload-label input[type="file"] {
  display: none;
}

.upload-label.disabled {
  opacity: 0.6;
  cursor: not-allowed;
  pointer-events: none;
}

.hint {
  margin: 0;
  color: #8c91af;
  font-size: 12px;
}

.error-text {
  margin: 0;
  color: #d9534f;
  font-size: 13px;
}
</style>
