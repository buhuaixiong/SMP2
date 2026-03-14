import { describe, expect, it } from "vitest";
import { reactive } from "vue";
import { useFormValidation } from "@/composables/useFormValidation";

describe("useFormValidation", () => {
  it("validates fields and reports errors", async () => {
    const model = reactive({ name: "", amount: 0 });
    const { validateField, errors, setFieldError, reset } = useFormValidation(model, {
      name: [
        {
          validator: (value) => value.trim().length > 0 || "name-required",
        },
      ],
      amount: [
        {
          validator: (value) => (value > 0 ? true : "amount-invalid"),
        },
      ],
    });

    await validateField("name");
    expect(errors.name[0]).toBe("name-required");

    model.name = "Demo";
    await validateField("name");
    expect(errors.name?.length).toBe(0);

    setFieldError("amount", "custom-error");
    expect(errors.amount[0]).toBe("custom-error");

    reset();
    expect(errors.amount?.length).toBe(0);
  });
});
