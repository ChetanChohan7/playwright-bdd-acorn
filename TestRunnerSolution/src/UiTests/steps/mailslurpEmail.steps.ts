import { test, expect, type Page, type Response } from '@playwright/test';
import { createBdd } from 'playwright-bdd';
import { getMailSlurpClient, extractUrl } from '../support/mailslurp';

const { Given, When, Then } = createBdd();

type MailslurpEmailWorld = {
  inboxId?: string;
  inboxEmailAddress?: string;
  emailBody?: string;
  extractedLink?: string;
  linkResponse?: Response | null;
};

type StepFixtures = {
  page: Page;
};

Given('a fresh MailSlurp inbox', async function (this: MailslurpEmailWorld) {
  test.skip(
    !process.env.MAILSLURP_API_KEY,
    'MAILSLURP_API_KEY must be set in the environment (.env) to run this scenario.'
  );

  const client = getMailSlurpClient();
  const inbox = await client.createInbox();

  this.inboxId = inbox.id;
  this.inboxEmailAddress = inbox.emailAddress;
  console.log(`Created MailSlurp inbox: ${this.inboxEmailAddress}`);
});

Given('I am on the registration page', async function (this: MailslurpEmailWorld, { page }: StepFixtures) {
  // Placeholder route — point this at the real app's registration page.
  await page.goto('/register');
});

When(
  'I register using the MailSlurp inbox email address',
  async function (this: MailslurpEmailWorld, { page }: StepFixtures) {
    // Placeholder selectors — replace with the real registration form's controls.
    await page.getByLabel('Email').fill(this.inboxEmailAddress!);
    await page.getByRole('button', { name: 'Register' }).click();
  }
);

When('I wait for the latest email sent to the inbox', async function (this: MailslurpEmailWorld) {
  const client = getMailSlurpClient();

  // unreadOnly avoids picking up a stale email — though a freshly created
  // inbox shouldn't have any to begin with.
  const email = await client.waitForLatestEmail(this.inboxId!, 30000, true);

  this.emailBody = email.body ?? undefined;
  console.log(`Received email: ${this.emailBody}`);
});

Then('the email body should contain a link', async function (this: MailslurpEmailWorld) {
  expect(this.emailBody).toBeTruthy();

  this.extractedLink = extractUrl(this.emailBody!);
  console.log(`Extracted link: ${this.extractedLink}`);
});

When('I navigate to the extracted email link', async function (this: MailslurpEmailWorld, { page }: StepFixtures) {
  this.linkResponse = await page.goto(this.extractedLink!);
});

Then('the linked page from the email should load successfully', async function (this: MailslurpEmailWorld) {
  // Generic placeholder success assertion — replace with a real app-specific
  // check (e.g. "Account verified" banner) once pointed at a real app.
  expect(this.linkResponse?.ok()).toBeTruthy();
});
