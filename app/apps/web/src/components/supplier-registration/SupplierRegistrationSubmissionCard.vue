<template>
  <section v-if="submission" class="submission-card">
    <h2>
      {{
        translateOr(
          "supplierRegistration.confirmation.title",
          "Registration Submitted",
        )
      }}
    </h2>
    <p>
      {{
        submission.message ||
        translateOr(
          "supplierRegistration.confirmation.defaultMessage",
          "Thank you. We have received your registration and started the approval process.",
        )
      }}
    </p>
    <p v-if="submission.assignedBuyerId">
      {{
        submission.assignmentCreated
          ? translateOr(
              "supplierRegistration.confirmation.assignmentCreated",
              "A purchaser has been assigned to your profile.",
            )
          : translateOr(
              "supplierRegistration.confirmation.assignmentAlreadyLinked",
              "Your existing purchaser relationship remains unchanged.",
            )
      }}
    </p>
    <p v-else>
      {{
        translateOr(
          "supplierRegistration.confirmation.assignmentPending",
          "A purchaser will be assigned during the review.",
        )
      }}
    </p>

    <ul class="submission-details">
      <li>
        <strong>
          {{
            translateOr(
              "supplierRegistration.confirmation.applicationId",
              "Application ID",
            )
          }}:
        </strong>
        {{ submission.applicationId }}
      </li>
      <li>
        <strong>{{ t("supplier.fields.companyId") }}:</strong>
        {{ submission.supplierCode }}
      </li>
      <li>
        <strong>
          {{
            translateOr(
              "supplierRegistration.confirmation.supplierId",
              "Supplier ID",
            )
          }}:
        </strong>
        {{ submission.supplierId }}
      </li>
      <li v-if="submission.assignedBuyerId">
        <strong>
          {{
            translateOr(
              "supplierRegistration.confirmation.assignedBuyer",
              "Assigned Buyer",
            )
          }}:
        </strong>
        {{ submission.assignedBuyerName || submission.assignedBuyerId }}
      </li>
      <li v-if="submission.defaultPassword">
        <strong>{{ t("auth.passwordLabel") }}:</strong>
        {{ submission.defaultPassword }}
      </li>
    </ul>

    <div v-if="tempAccount" class="temp-account-card">
      <h3>
        {{
          translateOr(
            "supplierRegistration.tempAccount.title",
            "Temporary Account Credentials",
          )
        }}
      </h3>
      <div class="temp-account-credentials">
        <div class="credential-row">
          <span class="credential-label">
            {{ translateOr("auth.usernameLabel", "Username") }}
          </span>
          <span class="credential-value">{{ tempAccount.username }}</span>
          <div class="credential-actions">
            <el-button
              size="small"
              @click="copyToClipboard(tempAccount.username, translateOr('auth.usernameLabel', 'Username'))"
            >
              {{ translateOr("common.copy", "Copy") }}
            </el-button>
          </div>
        </div>
        <div class="credential-row">
          <span class="credential-label">{{ t("auth.passwordLabel") }}</span>
          <span class="credential-value">{{ maskedTempPassword }}</span>
          <div class="credential-actions">
            <el-button
              size="small"
              @click="copyToClipboard(tempAccount.password, t('auth.passwordLabel'))"
            >
              {{ translateOr("common.copy", "Copy") }}
            </el-button>
            <el-button size="small" @click="toggleTempPassword">
              {{
                showTempPassword
                  ? translateOr("common.hide", "Hide")
                  : translateOr("common.show", "Show")
              }}
            </el-button>
          </div>
        </div>
        <div class="credential-row">
          <span class="credential-label">
            {{
              translateOr(
                "supplierRegistration.tempAccount.expiresAt",
                "Expires",
              )
            }}
          </span>
          <span class="credential-value">
            {{ formatDateTime(tempAccount.expiresAt) }}
          </span>
        </div>
      </div>
      <div v-if="tempAccountQrUrl" class="temp-account-qr">
        <img :src="tempAccountQrUrl" alt="Temporary account QR code" />
        <p>
          {{
            translateOr(
              "supplierRegistration.tempAccount.qrHint",
              "Scan to prefill username and password.",
            )
          }}
        </p>
      </div>
    </div>
    <p v-else-if="tempAccountError" class="temp-account-error">
      {{ tempAccountError }}
    </p>

    <div class="submission-actions">
      <el-button type="success" @click="viewStatus">
        {{
          translateOr(
            "supplierRegistration.confirmation.viewStatus",
            "View Application Status",
          )
        }}
      </el-button>
      <el-button @click="returnToLogin">
        {{
          translateOr(
            "supplierRegistration.confirmation.returnToLogin",
            "Return to login",
          )
        }}
      </el-button>
    </div>
  </section>
</template>

<script setup lang="ts">
import type { SupplierRegistrationResponse } from "@/api/public";
import type { TempAccountResponse } from "@/api/suppliers";
import { useI18n } from "vue-i18n";

const { t } = useI18n();

defineProps<{
  translateOr: (key: string, fallback: string) => string;
  submission: SupplierRegistrationResponse | null;
  tempAccount: TempAccountResponse | null;
  tempAccountError: string | null;
  tempAccountQrUrl: string;
  maskedTempPassword: string;
  showTempPassword: boolean;
  formatDateTime: (value?: string | null) => string;
  copyToClipboard: (value: string, label: string) => void;
  toggleTempPassword: () => void;
  viewStatus: () => void;
  returnToLogin: () => void;
}>();
</script>
