import { test, expect } from '@playwright/test';
import { createBdd } from 'playwright-bdd';
import { randomUUID } from 'crypto';
import { TwilioSmsSender } from '../src/twilio/TwilioSmsSender';
import type { TwilioSmsWorld } from '../support/world';

const { Given, When, Then } = createBdd();

type TwilioCredentials = {
  accountSid: string;
  authToken: string;
  phoneNumber: string;
  testRecipientNumber: string;
};

function readTwilioCredentials(): TwilioCredentials | undefined {
  const accountSid = process.env.TWILIO_ACCOUNT_SID;
  const authToken = process.env.TWILIO_AUTH_TOKEN;
  const phoneNumber = process.env.TWILIO_PHONE_NUMBER;
  const testRecipientNumber = process.env.TWILIO_TEST_RECIPIENT_NUMBER;

  if (!accountSid || !authToken || !phoneNumber || !testRecipientNumber) {
    return undefined;
  }

  return { accountSid, authToken, phoneNumber, testRecipientNumber };
}

Given('I generate a uniquely tagged test SMS message', async function (this: TwilioSmsWorld) {
  test.skip(
    !readTwilioCredentials(),
    'TWILIO_ACCOUNT_SID, TWILIO_AUTH_TOKEN, TWILIO_PHONE_NUMBER, and ' +
      'TWILIO_TEST_RECIPIENT_NUMBER must all be set in the environment (.env) to run this scenario.'
  );

  // Unique token lets us confirm the delivered message is the one we just
  // sent, and distinguishes it from any other message on the account.
  this.testToken = randomUUID();
  this.sentBody = `Test message ${this.testToken} sent at ${new Date().toISOString()}`;
});

When(
  'I send the SMS from the Twilio number to the verified test recipient via the Twilio API',
  async function (this: TwilioSmsWorld) {
    const { accountSid, authToken, phoneNumber, testRecipientNumber } = readTwilioCredentials()!;
    const sender = new TwilioSmsSender(accountSid, authToken);

    this.sentSid = await sender.sendSms(phoneNumber, testRecipientNumber, this.sentBody!);
    console.log(`Sent SMS via Twilio API (SID: ${this.sentSid}): ${this.sentBody}`);
  }
);

When('I wait for Twilio to confirm the delivery status via the API', async function (this: TwilioSmsWorld) {
  const { accountSid, authToken } = readTwilioCredentials()!;
  const sender = new TwilioSmsSender(accountSid, authToken);

  const deliveredMessage = await sender.waitForDeliveryStatus(this.sentSid!, 60000);
  this.deliveredBody = deliveredMessage.body;
  this.deliveryStatus = deliveredMessage.status;
});

Then('the message content and delivery status should be printed', async function (this: TwilioSmsWorld) {
  console.log('--------------------------------------------------');
  console.log('Message content (via Twilio API):');
  console.log(this.deliveredBody);
  console.log(`Delivery status: ${this.deliveryStatus}`);
  console.log('--------------------------------------------------');
});

Then('the delivery status should be successful', async function (this: TwilioSmsWorld) {
  expect(this.deliveredBody).toContain(this.testToken);
  expect(['delivered', 'sent']).toContain(this.deliveryStatus);
});
