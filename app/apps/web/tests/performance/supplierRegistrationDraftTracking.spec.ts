import { describe, expect, it } from "vitest";
import {
  buildDraftPayloadSnapshot,
  collectChangedFields,
} from "@/utils/formChangeTracker";

describe("supplier registration draft tracking helpers", () => {
  it("collects only changed watched keys", () => {
    const previous = {
      companyName: "Acme",
      contactEmail: "old@acme.test",
      paymentMethods: ["wire"],
    };
    const next = {
      companyName: "Acme",
      contactEmail: "new@acme.test",
      paymentMethods: ["wire"],
    };

    const changed = collectChangedFields(previous, next, [
      "companyName",
      "contactEmail",
      "paymentMethods",
    ]);

    expect(changed).toEqual(["contactEmail"]);
  });

  it("creates detached draft payload snapshot for mutable fields", () => {
    const form = {
      companyName: "Acme",
      paymentMethods: ["wire"],
      businessLicenseFile: {
        name: "license.pdf",
        type: "application/pdf",
        size: 123,
        content: "base64",
      },
    };

    const snapshot = buildDraftPayloadSnapshot(form);
    form.paymentMethods.push("card");
    form.businessLicenseFile.content = "changed";

    expect(snapshot.paymentMethods).toEqual(["wire"]);
    expect(snapshot.businessLicenseFile).toEqual({
      name: "license.pdf",
      type: "application/pdf",
      size: 123,
      content: "base64",
    });
  });
});

