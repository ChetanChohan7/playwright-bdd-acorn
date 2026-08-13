import twilio from 'twilio';
import { MessageInstance } from 'twilio/lib/rest/api/v2010/account/message';

/**
 * Terminal message statuses that indicate Twilio has finished processing
 * a message and will not update its status further.
 */
const TERMINAL_STATUSES = new Set(['delivered', 'sent', 'failed', 'undelivered']);

/**
 * TwilioSmsSender handles sending outbound SMS messages via the Twilio API
 * and tracking their delivery status.
 */
export class TwilioSmsSender {
  private readonly client: twilio.Twilio;

  /**
   * Constructor for TwilioSmsSender
   * @param accountSid - Twilio Account SID
   * @param authToken - Twilio Authentication Token
   * @throws Error if any required parameter is missing
   */
  constructor(accountSid: string, authToken: string) {
    if (!accountSid || !authToken) {
      throw new Error(
        'Missing required Twilio credentials: TWILIO_ACCOUNT_SID or TWILIO_AUTH_TOKEN'
      );
    }

    this.client = twilio(accountSid, authToken);
  }

  /**
   * Sends an SMS message via the Twilio API.
   *
   * @param from - The Twilio phone number to send from
   * @param to - The destination phone number
   * @param body - The message text to send
   * @returns Promise resolving to the SID of the created message
   * @throws Error if the Twilio API call fails
   */
  public async sendSms(from: string, to: string, body: string): Promise<string> {
    const message = await this.client.messages.create({ from, to, body });
    return message.sid;
  }

  /**
   * Polls the Twilio API for a message's delivery status until it reaches a
   * terminal state (delivered, sent, failed, or undelivered) or the timeout
   * elapses. Useful for confirming Twilio/the carrier processed a message
   * sent to a non-Twilio destination, where the content can't be read back
   * via an inbound message lookup.
   *
   * @param messageSid - The SID of the message to check, as returned by sendSms
   * @param timeoutMs - Maximum time in milliseconds to wait (default: 60 seconds)
   * @returns Promise resolving to the fetched message resource once its status is terminal
   * @throws Error if the status doesn't reach a terminal state within the timeout
   */
  public async waitForDeliveryStatus(
    messageSid: string,
    timeoutMs: number = 60000
  ): Promise<MessageInstance> {
    const startTime = Date.now();
    const pollIntervalMs = 3000; // 3 seconds

    while (Date.now() - startTime < timeoutMs) {
      const message = await this.client.messages(messageSid).fetch();

      if (TERMINAL_STATUSES.has(message.status)) {
        return message;
      }

      await this.delay(pollIntervalMs);
    }

    throw new Error(`Message ${messageSid} did not reach a terminal status within timeout period`);
  }

  /**
   * Utility method to create a delay
   * @param ms - Milliseconds to delay
   */
  private delay(ms: number): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }
}
