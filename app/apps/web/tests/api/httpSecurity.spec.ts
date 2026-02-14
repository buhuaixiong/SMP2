import { describe, expect, it } from "vitest";
import { normalizeToHttpsUrl } from "@/api/http";

describe("normalizeToHttpsUrl", () => {
  it("converts http URLs without corrupting slashes", () => {
    expect(normalizeToHttpsUrl("http://example.com/api", true)).toBe("https://example.com/api");
  });

  it("keeps absolute https and relative URLs unchanged", () => {
    expect(normalizeToHttpsUrl("https://example.com/api", true)).toBe("https://example.com/api");
    expect(normalizeToHttpsUrl("/api/files/1", true)).toBe("/api/files/1");
  });
});

