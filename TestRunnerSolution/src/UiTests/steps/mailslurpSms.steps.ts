import { test, expect, type Page, type Response } from '@playwright/test';
import { createBdd } from 'playwright-bdd';
import { getMailSlurpClient, extractUrl } from '../support/mailslurp';

const { Given, When, Then } = createBdd();

type MailslurpSmsWorld = {
  smsBody?: string;
  extractedLink?: string;
  linkResponse?: Response | null;
};

type StepFixtures = {
  page: Page;
};

function readMailSlurpSmsConfig(): { phoneId: string } | undefined {
  const phoneId = process.env.MAILSLURP_PHONE_ID;

  if (!process.env.MAILSLURP_API_KEY || !phoneId) {
    return undefined;
  }

  return { phoneId };
}

Given('I am on the invite page', async function (this: MailslurpSmsWorld, { page }: StepFixtures) {
  test.skip(
    !readMailSlurpSmsConfig(),
    'MAILSLURP_API_KEY and MAILSLURP_PHONE_ID must both be set in the environment (.env) to run this scenario.'
  );

  // Placeholder route — point this at the real app's invite page.
  await page.goto('/invite');
});

When('I click the button to send an SMS invite', async function (this: MailslurpSmsWorld, { page }: StepFixtures) {
  // Placeholder selector — replace with the real "send invite" control.
  await page.getByRole('button', { name: 'Send Invite' }).click();
});

When(
  'I wait for the latest SMS sent to the MailSlurp phone number',
  async function (this: MailslurpSmsWorld) {
    const { phoneId } = readMailSlurpSmsConfig()!;
    const client = getMailSlurpClient();

    // unreadOnly avoids picking up a stale SMS left over from a previous run.
    // Note: WaitForSingleSmsOptions has no `from`/sender field to filter on
    // at all, so this works whether the SMS arrives from a numeric or (as
    // here) an alphanumeric sender ID — nothing to configure either way.
    const sms = await client.waitController.waitForLatestSms({
      waitForSingleSmsOptions: {
        phoneNumberId: phoneId,
        timeout: 30000,
        unreadOnly: true,
      },
    });

    this.smsBody = sms.body;
    console.log(`Received SMS: ${this.smsBody}`);
  }
);

Then('the SMS body should contain a link', async function (this: MailslurpSmsWorld) {
  expect(this.smsBody).toBeTruthy();

  this.extractedLink = extractUrl(this.smsBody!);
  console.log(`Extracted link: ${this.extractedLink}`);
});

When('I navigate to the extracted SMS link', async function (this: MailslurpSmsWorld, { page }: StepFixtures) {
  this.linkResponse = await page.goto(this.extractedLink!);
});

Then('the linked page from the SMS should load successfully', async function (this: MailslurpSmsWorld) {
  // Generic placeholder success assertion — replace with a real app-specific
  // check (e.g. a confirmation banner) once pointed at a real app.
  expect(this.linkResponse?.ok()).toBeTruthy();
});
