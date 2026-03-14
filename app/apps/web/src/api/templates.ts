import { apiFetch } from "./http";
import type { TemplateDefinition, TemplateHistory } from "@/types";

export const fetchTemplates = async (): Promise<TemplateDefinition[]> => {
  const response = await apiFetch<{ data: TemplateDefinition[] }>("/templates");
  return response.data;
};

export const fetchTemplateHistory = async (code: string): Promise<TemplateHistory> => {
  const response = await apiFetch<{ data: TemplateHistory }>(`/templates/history/${code}`);
  return response.data;
};

export const uploadTemplateDocument = async (
  templateCode: string,
  file: File,
): Promise<TemplateDefinition[]> => {
  const form = new FormData();
  form.append("templateCode", templateCode);
  form.append("file", file);

  const response = await apiFetch<{ data: TemplateDefinition[] }>("/templates", {
    method: "POST",
    body: form,
    parseData: true,
  });
  return response.data;
};
