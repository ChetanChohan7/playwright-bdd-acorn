import { MailSlurp } from 'mailslurp-client';

let client: MailSlurp | undefined;

/**
 * Returns a shared MailSlurp client, constructed lazily from MAILSLURP_API_KEY.
 *
 * Lazy on purpose: this file is eagerly `require`'d by playwright-bdd for
 * every test run (see playwright.config.ts's `support/**\/*.ts` glob), so
 * constructing the client — or validating the env var — at import time
 * would break every other, unrelated scenario whenever MAILSLURP_API_KEY
 * isn't set, not just the MailSlurp ones.
 */
export function getMailSlurpClient(): MailSlurp {
  if (!client) {
    const apiKey = process.env.MAILSLURP_API_KEY;

    if (!apiKey) {
      throw new Error('MAILSLURP_API_KEY is not set. Point it to a MailSlurp API key from your dashboard.');
    }

    client = new MailSlurp({ apiKey });
  }

  return client;
}

/**
 * Extracts the first HTTPS URL from a message body.
 * @throws Error if no URL is found
 */
export function extractUrl(body: string): string {
  const match = body.match(/https:\/\/[^\s<>"{}\\^`[\]]+/);

  if (!match) {
    throw new Error(`No URL found in message body: ${body}`);
  }

  return match[0].replace(/[.,;:!?)]*$/, '');
}

/**
 * Extracts a numeric OTP code (4-8 digits) from a message body.
 * @throws Error if no matching code is found
 */
export function extractOtpCode(body: string): string {
  const match = body.match(/\b\d{4,8}\b/);

  if (!match) {
    throw new Error(`No 4-8 digit OTP code found in message body: ${body}`);
  }

  return match[0];
}
