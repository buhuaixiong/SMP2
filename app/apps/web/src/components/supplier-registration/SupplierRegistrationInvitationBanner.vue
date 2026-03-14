<template>
  <div class="invitation-banner">
    <div class="banner-icon">📨</div>
    <div class="banner-content">
      <h3>
        {{
          translateOr(
            "supplierRegistration.invitation.title",
            "RFQ Invitation Linked",
          )
        }}
      </h3>
      <p>
        {{
          translateOr(
            "supplierRegistration.invitation.subtitle",
            "Complete the wizard to unlock RFQ details and tracking.",
          )
        }}
      </p>
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  translateOr: (key: string, fallback: string) => string;
}>();
</script>
