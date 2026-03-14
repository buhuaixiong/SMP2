export const buildRfqQuoteSubmissionUrl = (
  baseUrl: string,
  rfqId: number,
  quoteId?: number,
): string => {
  const normalizedBaseUrl = baseUrl.replace(/\/+$/, "");
  const basePath = `${normalizedBaseUrl}/rfq-workflow/${rfqId}/quotes`;
  return quoteId == null ? basePath : `${basePath}/${quoteId}`;
};

type ResponseLike = Pick<Response, "json" | "text"> & {
  headers?: Pick<Headers, "get"> | null;
};

export const parseRfqQuoteSubmissionError = async (
  response: ResponseLike,
): Promise<string> => {
  const contentType = response.headers?.get("content-type") ?? "";
  const looksLikeJson = /json/i.test(contentType);

  if (looksLikeJson) {
    try {
      const parsed = await response.json();
      if (
        parsed &&
        typeof parsed === "object" &&
        "message" in parsed &&
        typeof parsed.message === "string" &&
        parsed.message.trim()
      ) {
        return parsed.message.trim();
      }
    } catch {}
  }

  try {
    const text = (await response.text()).trim();
    if (!text || /^<!doctype|^<html|^</i.test(text)) {
      return "Failed to submit quote";
    }
    return text;
  } catch {
    return "Failed to submit quote";
  }
};
