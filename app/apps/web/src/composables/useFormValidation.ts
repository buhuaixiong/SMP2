import { computed, reactive, unref } from "vue";

export interface ValidationRule<TModel = Record<string, unknown>> {
  validator: (value: any, model: TModel) => boolean | string | Promise<boolean | string>;
  message?: string;
}

export type ValidationRules<TModel> = Partial<Record<keyof TModel, ValidationRule<TModel>[]>>;

export function useFormValidation<TModel extends Record<string, any>>(
  model: TModel,
  rules: ValidationRules<TModel>,
) {
  const errors = reactive<Record<string, string[]>>({});
  const validating = reactive<Record<string, boolean>>({});

  const clearField = (field: keyof TModel) => {
    errors[field as string] = [];
    validating[field as string] = false;
  };

  const validateField = async (field: keyof TModel) => {
    const fieldRules = rules[field];
    if (!fieldRules?.length) {
      clearField(field);
      return true;
    }
    validating[field as string] = true;
    const fieldErrors: string[] = [];
    const value = unref(model[field] as any);

    for (const rule of fieldRules) {
      const result = await rule.validator(value, model);
      if (result === true) {
        continue;
      }
      if (result === false) {
        fieldErrors.push(rule.message ?? "Invalid value");
      } else if (typeof result === "string" && result.trim().length > 0) {
        fieldErrors.push(result);
      }
    }

    errors[field as string] = fieldErrors;
    validating[field as string] = false;
    return fieldErrors.length === 0;
  };

  const validateAll = async () => {
    const fields = Object.keys(rules) as Array<keyof TModel>;
    if (!fields.length) return true;
    const results = await Promise.all(fields.map((field) => validateField(field)));
    return results.every(Boolean);
  };

  const setFieldError = (field: keyof TModel, message: string | string[]) => {
    errors[field as string] = Array.isArray(message) ? message : [message];
  };

  const reset = () => {
    Object.keys(rules).forEach((field) => clearField(field as keyof TModel));
  };

  const hasError = computed(() => Object.values(errors).some((messages) => messages?.length));

  return {
    errors,
    validating,
    hasError,
    validateField,
    validateAll,
    reset,
    setFieldError,
  };
}
