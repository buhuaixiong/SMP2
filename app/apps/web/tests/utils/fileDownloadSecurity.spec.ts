import { beforeEach, describe, expect, it } from "vitest";
import { getSecureDownloadUrl, getUploadUrl, resolveUploadDownloadUrl } from "@/utils/fileDownload";

describe("file download URL security", () => {
  beforeEach(() => {
    window.localStorage.clear();
    window.localStorage.setItem("token", "super-secret-token");
  });

  it("does not append token query in secure URL builders", () => {
    const secureUrl = getSecureDownloadUrl("quote.xlsx", "rfq-attachments");
    const uploadUrl = getUploadUrl("quote.xlsx", "rfq-attachments");

    expect(secureUrl).toContain("/uploads/rfq-attachments/quote.xlsx");
    expect(uploadUrl).toContain("/uploads/rfq-attachments/quote.xlsx");
    expect(secureUrl).not.toContain("token=");
    expect(uploadUrl).not.toContain("token=");
  });

  it("does not append token query when resolving legacy upload paths", () => {
    const resolved = resolveUploadDownloadUrl("/uploads/rfq-attachments/legacy.pdf", null, "rfq-attachments");

    expect(resolved).toBeTruthy();
    expect(resolved).toContain("/uploads/rfq-attachments/legacy.pdf");
    expect(String(resolved)).not.toContain("token=");
  });
});

