import { test, expect } from '@playwright/test';
import { createBdd } from 'playwright-bdd';
import { randomUUID } from 'crypto';
import { AcsSmsSender } from '../src/acs/AcsSmsSender';
import type { AcsSmsWorld } from '../support/world';

const { Given, When, Then } = createBdd();

type AcsCredentials = {
  connectionString: string;
  phoneNumber: string;
  testRecipientNumber: string;
  storageQueueConnectionString: string;
  deliveryReportQueueName: string;
};

function readAcsCredentials(): AcsCredentials | undefined {
  const connectionString = process.env.AZURE_COMMUNICATION_CONNECTION_STRING;
  const phoneNumber = process.env.ACS_PHONE_NUMBER;
  const testRecipientNumber = process.env.ACS_TEST_RECIPIENT_NUMBER;
  const storageQueueConnectionString = process.env.AZURE_STORAGE_QUEUE_CONNECTION_STRING;
  const deliveryReportQueueName = process.env.AZURE_SMS_DELIVERY_REPORT_QUEUE_NAME;

  if (
    !connectionString ||
    !phoneNumber ||
    !testRecipientNumber ||
    !storageQueueConnectionString ||
    !deliveryReportQueueName
  ) {
    return undefined;
  }

  return { connectionString, phoneNumber, testRecipientNumber, storageQueueConnectionString, deliveryReportQueueName };
}

Given('I generate a uniquely tagged test SMS message', async function (this: AcsSmsWorld) {
  test.skip(
    !readAcsCredentials(),
    'AZURE_COMMUNICATION_CONNECTION_STRING, ACS_PHONE_NUMBER, ACS_TEST_RECIPIENT_NUMBER, ' +
      'AZURE_STORAGE_QUEUE_CONNECTION_STRING, and AZURE_SMS_DELIVERY_REPORT_QUEUE_NAME must all be ' +
      'set in the environment (.env) to run this scenario.'
  );

  // Unique token lets us confirm the delivery report we poll for corresponds
  // to the message we just sent, and distinguishes it from any other
  // message on the account.
  this.testToken = randomUUID();
  this.sentBody = `Test message ${this.testToken} sent at ${new Date().toISOString()}`;
});

When(
  'I send the SMS from the ACS number to the verified test recipient via the ACS API',
  async function (this: AcsSmsWorld) {
    const { connectionString, phoneNumber, testRecipientNumber, storageQueueConnectionString, deliveryReportQueueName } =
      readAcsCredentials()!;
    const sender = new AcsSmsSender(connectionString, {
      storageQueueConnectionString,
      queueName: deliveryReportQueueName,
    });

    this.sentMessageId = await sender.sendSms(phoneNumber, testRecipientNumber, this.sentBody!);
    console.log(`Sent SMS via Azure Communication Services (message ID: ${this.sentMessageId}): ${this.sentBody}`);
  }
);

When('I wait for Azure Communication Services to confirm the delivery status via the API', async function (
  this: AcsSmsWorld
) {
  const { connectionString, storageQueueConnectionString, deliveryReportQueueName } = readAcsCredentials()!;
  const sender = new AcsSmsSender(connectionString, {
    storageQueueConnectionString,
    queueName: deliveryReportQueueName,
  });

  const report = await sender.waitForDeliveryStatus(this.sentMessageId!, 60000);
  this.deliveryStatus = report.status;
  this.deliveryStatusDetails = report.details;
});

Then('the message content and delivery status should be printed', async function (this: AcsSmsWorld) {
  console.log('--------------------------------------------------');
  console.log('Message content (as sent via the ACS API):');
  console.log(this.sentBody);
  console.log(`Delivery status: ${this.deliveryStatus} (${this.deliveryStatusDetails})`);
  console.log('--------------------------------------------------');
});

Then('the delivery status should be successful', async function (this: AcsSmsWorld) {
  expect(this.sentBody).toContain(this.testToken);
  expect(this.deliveryStatus).toBe('Delivered');
});
