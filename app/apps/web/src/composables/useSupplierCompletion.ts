import type { Supplier } from "@/types";

const COMPLETION_LABELS: Record<string, string> = {
  complete: "Complete",
  mostly_complete: "Mostly complete",
  needs_attention: "Needs attention",
};

export const useSupplierCompletion = () => {
  const completionLabel = (supplier: Supplier) => {
    const status = supplier.completionStatus ?? supplier.complianceSummary?.completionCategory;
    if (!status) {
      return "Unknown";
    }
    return COMPLETION_LABELS[status] ?? status;
  };

  const completionPillClass = (supplier: Supplier) => {
    const status = supplier.completionStatus ?? supplier.complianceSummary?.completionCategory;
    return {
      "pill-complete": status === "complete",
      "pill-mostly": status === "mostly_complete",
      "pill-attention": status !== "complete" && status !== "mostly_complete",
    };
  };

  const completionTooltip = (supplier: Supplier) => {
    const profileScore = Math.round(
      supplier.profileCompletion ?? supplier.complianceSummary?.profileScore ?? 0,
    );
    const documentScore = Math.round(
      supplier.documentCompletion ?? supplier.complianceSummary?.documentScore ?? 0,
    );

    const missingRequirements = new Set<string>();
    const pushRequirement = (value?: string | null) => {
      if (!value) return;
      const label = String(value).trim();
      if (label) {
        missingRequirements.add(label);
      }
    };

    if (Array.isArray(supplier.missingRequirements)) {
      supplier.missingRequirements.forEach((item) => {
        if (!item) return;
        if (typeof item === "string") {
          pushRequirement(item);
          return;
        }
        pushRequirement((item as { label?: string; key?: string }).label);
        pushRequirement((item as { label?: string; key?: string }).key);
      });
    }

    const compliance = supplier.complianceSummary;
    if (compliance) {
      compliance.missingItems?.forEach((item) => pushRequirement(item?.label ?? item?.key));
      compliance.missingDocumentTypes?.forEach((item) =>
        pushRequirement(item?.label ?? item?.type),
      );
      compliance.missingProfileFields?.forEach((item) => pushRequirement(item?.label ?? item?.key));
    }

    const parts = [
      `Profile completion: ${profileScore}%`,
      `Document completion: ${documentScore}%`,
    ];

    if (missingRequirements.size) {
      parts.push(`Outstanding requirements: ${Array.from(missingRequirements).join(", ")}`);
    } else {
      parts.push("Outstanding requirements: none");
    }

    return parts.join("\n");
  };

  return {
    completionLabel,
    completionPillClass,
    completionTooltip,
  };
};
