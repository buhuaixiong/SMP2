import { describe, expect, it } from "vitest";

import en from "@/locales/en/auth.json";
import th from "@/locales/th/auth.json";
import zh from "@/locales/zh/auth.json";

const requiredKeys = [
  "invalidCredentials",
  "timeout",
  "loginInProgress",
  "accountLocked",
  "accountFrozen",
  "accountDeleted",
  "genericFailure",
];

describe("auth login locale messages", () => {
  it("contains required login error keys in zh, en, and th", () => {
    for (const locale of [zh, en, th]) {
      expect(locale).toHaveProperty("loginErrors");
      for (const key of requiredKeys) {
        expect(locale.loginErrors).toHaveProperty(key);
      }
    }
  });
});
