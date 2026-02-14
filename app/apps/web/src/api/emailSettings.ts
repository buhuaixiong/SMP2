import { apiFetch } from "./http";

export interface SmtpProvider {
  id: string;
  name: string;
  host: string;
  port: number;
  secure: boolean;
  userLabel: string;
  passwordLabel: string;
  docs: string | null;
  note?: string;
}

export interface SmtpConfig {
  configured: boolean;
  provider: string | null;
  host: string;
  port: number;
  secure: boolean;
  user: string;
  from: string;
  fromName: string;
  testMode: boolean;
  hasPassword?: boolean;
}

export interface SmtpConfigUpdate {
  provider: string;
  host: string;
  port: number;
  secure: boolean;
  user: string;
  password?: string;
  from: string;
  fromName: string;
  testMode: boolean;
}

export interface EmailTemplate {
  id: string;
  name: string;
  description: string;
  category: string;
  variables: string[];
}

export interface TestEmailResult {
  success: boolean;
  message: string;
  data?: {
    messageId: string;
    testEmail: string;
    timestamp: string;
  };
  error?: string;
  details?: string;
}

/**
 * Get current SMTP configuration
 */
export async function getSmtpConfig(): Promise<SmtpConfig> {
  const res = await apiFetch<{ data: SmtpConfig }>("/email-settings");
  return res.data;
}

/**
 * Update SMTP configuration
 */
export async function updateSmtpConfig(config: SmtpConfigUpdate): Promise<SmtpConfig> {
  const res = await apiFetch<{ data: SmtpConfig }>("/email-settings", {
    method: "PUT",
    body: config,
  });
  return res.data;
}

/**
 * Test SMTP connection
 */
export async function testSmtpConnection(testEmail: string): Promise<TestEmailResult> {
  return await apiFetch<TestEmailResult>("/email-settings/test", {
    method: "POST",
    body: { testEmail },
  });
}

/**
 * Delete SMTP configuration
 */
export async function deleteSmtpConfig(): Promise<void> {
  await apiFetch("/email-settings", {
    method: "DELETE",
  });
}

/**
 * Get available SMTP providers
 */
export async function getSmtpProviders(): Promise<SmtpProvider[]> {
  const res = await apiFetch<{ data: SmtpProvider[] }>("/email-settings/providers");
  return res.data;
}

/**
 * Get available email templates
 */
export async function getEmailTemplates(): Promise<EmailTemplate[]> {
  const res = await apiFetch<{ data: EmailTemplate[] }>("/email-settings/templates");
  return res.data;
}
