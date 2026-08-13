import twilio from 'twilio';

/**
 * TwilioSmsReader handles connection to Twilio and retrieval of SMS messages.
 * Polls Twilio API at regular intervals to check for new SMS messages.
 */
export class TwilioSmsReader {
  private readonly phoneNumber: string;
  private readonly client: twilio.Twilio;

  /**
   * Constructor for TwilioSmsReader
   * @param accountSid - Twilio Account SID
   * @param authToken - Twilio Authentication Token
   * @param phoneNumber - Twilio phone number to monitor
   * @throws Error if any required parameter is missing
   */
  constructor(accountSid: string, authToken: string, phoneNumber: string) {
    if (!accountSid || !authToken || !phoneNumber) {
      throw new Error(
        'Missing required Twilio credentials: TWILIO_ACCOUNT_SID, TWILIO_AUTH_TOKEN, or TWILIO_PHONE_NUMBER'
      );
    }

    this.phoneNumber = phoneNumber;
    this.client = twilio(accountSid, authToken);
  }

  /**
   * Waits for the latest SMS message received to the configured Twilio phone number.
   * Polls the Twilio API every 5 seconds until a message is found or timeout is reached.
   * Only returns messages received after polling started.
   *
   * @param timeoutMs - Maximum time in milliseconds to wait for an SMS (default: 5 minutes)
   * @returns Promise resolving to the SMS body text
   * @throws Error if no SMS is received within the timeout period
   */
  public async waitForLatestSms(timeoutMs: number = 300000): Promise<string> {
    const startTime = Date.now();
    const startTimeDate = new Date(startTime);
    const pollIntervalMs = 5000; // 5 seconds

    while (Date.now() - startTime < timeoutMs) {
      try {
        const messages = await this.client.messages.list({
          to: this.phoneNumber,
          limit: 20,
        });

        if (messages.length > 0) {
          // Find the first message received after polling started
          for (const message of messages) {
            if (message.dateCreated && new Date(message.dateCreated) >= startTimeDate) {
              return message.body || '';
            }
          }
        }
      } catch (error) {
        // Log error but continue polling
        console.error('Error querying Twilio API:', error);
      }

      // Wait before next poll
      await this.delay(pollIntervalMs);
    }

    throw new Error('No SMS received within timeout period');
  }

  /**
   * Utility method to create a delay
   * @param ms - Milliseconds to delay
   */
  private delay(ms: number): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }
}
