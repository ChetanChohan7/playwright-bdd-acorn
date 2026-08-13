import * as dotenv from 'dotenv';
import { randomUUID } from 'crypto';
import { TwilioSmsSender } from '../TwilioSmsSender';
import { TwilioSmsReader } from '../TwilioSmsReader';

dotenv.config();

const accountSid = process.env.TWILIO_ACCOUNT_SID;
const authToken = process.env.TWILIO_AUTH_TOKEN;
const phoneNumber = process.env.TWILIO_PHONE_NUMBER;

const hasCredentials = Boolean(accountSid && authToken && phoneNumber);

if (!hasCredentials) {
  // eslint-disable-next-line no-console
  console.warn(
    'Skipping Twilio SMS round trip test: TWILIO_ACCOUNT_SID, TWILIO_AUTH_TOKEN, ' +
      'and TWILIO_PHONE_NUMBER must all be set in the environment (.env) to run it.'
  );
}

/**
 * Integration test that sends a real SMS via the Twilio API and then polls the
 * Twilio API to confirm the message was received, printing the received text.
 *
 * This sends from TWILIO_PHONE_NUMBER to itself, then uses TwilioSmsReader to
 * poll the Messages API for a message received after the send. It requires
 * live Twilio credentials on a non-trial (upgraded) account, since trial
 * accounts restrict both custom message content and self-addressed sends.
 * The test is skipped automatically when credentials aren't configured
 * (e.g. CI without secrets).
 */
(hasCredentials ? describe : describe.skip)('Twilio SMS round trip', () => {
  jest.setTimeout(150000); // polling can take up to ~2 minutes

  it('sends an SMS via the API and reads back its content via the API', async () => {
    const sender = new TwilioSmsSender(accountSid as string, authToken as string);
    const reader = new TwilioSmsReader(
      accountSid as string,
      authToken as string,
      phoneNumber as string
    );

    // Unique token so we can confirm the polled message is the one we just sent,
    // not a stale/unrelated message already sitting on the number.
    const testToken = randomUUID();
    const messageBody = `Test message ${testToken} sent at ${new Date().toISOString()}`;

    const sentSid = await sender.sendSms(
      phoneNumber as string,
      phoneNumber as string,
      messageBody
    );
    console.log(`Sent SMS via Twilio API (SID: ${sentSid}): ${messageBody}`);

    const receivedBody = await reader.waitForLatestSms(120000);

    console.log('--------------------------------------------------');
    console.log('Received SMS content (via Twilio API):');
    console.log(receivedBody);
    console.log('--------------------------------------------------');

    expect(receivedBody).toContain(testToken);
  });
});
