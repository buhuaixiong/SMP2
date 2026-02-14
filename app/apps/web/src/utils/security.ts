export const sanitizeInput = (value?: string | null) =>
  (value ?? "")
    .replace(
      /[&<>'"]/g,
      (char) =>
        ({
          "&": "&amp;",
          "<": "&lt;",
          ">": "&gt;",
          "'": "&#39;",
          '"': "&quot;",
          "": "&#96;",
        })[char] ?? char,
    )
    .trim();

export const safeEmail = (value?: string | null) =>
  sanitizeInput(value)
    .toLowerCase()
    .replace(/[^a-z0-9._%+@-]/g, "");
