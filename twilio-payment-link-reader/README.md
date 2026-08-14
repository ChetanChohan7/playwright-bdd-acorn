# ACS Payment Link Reader

A Node.js TypeScript application that connects to Azure Communication Services (ACS), retrieves the latest SMS message, extracts a payment URL using regex, and outputs it to the console.

## Overview

This application automates the process of monitoring an ACS phone number for incoming SMS messages containing payment links. Unlike Twilio, ACS has no REST endpoint to poll for received messages directly — inbound SMS is only published as an **Event Grid event**. This app polls an Azure Storage Queue that an Event Grid subscription delivers those events into, extracts the payment URL from the message, and displays it in a clean format.

**Key Features:**
- Connects securely to Azure Communication Services using credentials from environment variables
- Polls an Azure Storage Queue every 5 seconds for new SMS-received events (see [Architecture](#architecture))
- Automatically times out after 2 minutes with a clear error message
- Uses regex pattern matching to extract URLs from SMS body or HTML content
- Strong TypeScript typing with no use of `any` type
- Clean separation of concerns with dedicated classes for SMS reading, sending, and URL extraction
- Comprehensive error handling and user-friendly messages

## Architecture

Twilio exposes a REST API you can poll for messages sent to/from a number at any time. **ACS does not** — it only publishes SMS activity as asynchronous **Event Grid** events:

| Event type | Published when |
|---|---|
| `Microsoft.Communication.SMSReceived` | An SMS is received by an ACS phone number |
| `Microsoft.Communication.SMSDeliveryReportReceived` | A delivery report is available for an SMS the ACS resource sent |

Event Grid pushes events somewhere — there's no "fetch by ID" to fall back on. This project routes both event types to **Azure Storage Queues**, and polls those queues, which keeps the same polling ergonomics as the original Twilio version without needing a publicly reachable webhook endpoint (the alternative would be an Event Grid → HTTP webhook / Azure Function setup).

```
ACS phone number                Event Grid subscription             Storage Queue            This app
  (receives SMS)  ── fires ──▶  Microsoft.Communication  ── delivers ──▶  sms-received  ◀── polls ── AcsSmsReader
                                 .SMSReceived                                                  (main app)

ACS resource                    Event Grid subscription             Storage Queue            This app
  (sends SMS)     ── fires ──▶  Microsoft.Communication  ── delivers ──▶  sms-delivery-  ◀── polls ── AcsSmsSender
                                 .SMSDeliveryReportReceived              reports              (test only)
```

## Prerequisites

- **Node.js** (v14 or higher)
- **npm** or **yarn** (comes with Node.js)
- An **Azure subscription** with:
  - An **Azure Communication Services** resource with an SMS-capable phone number
  - An **Azure Storage account** with two queues (see [Azure Setup](#azure-setup))
  - **Event Grid subscriptions** routing the two SMS event types to those queues

## Azure Setup

1. **Create an ACS resource** (if you don't have one): [Azure Portal → Create a resource → Communication Services](https://portal.azure.com/#create/Microsoft.CommunicationServicesLegacy)
2. **Get an SMS-capable phone number**: in the resource, go to **Phone numbers → + Get** and acquire a number with SMS capability (two-way, if you want to receive real inbound SMS)
3. **Copy the connection string**: resource → **Keys** → copy the **Connection string**
4. **Create a Storage account** (if you don't have one), then create two queues under **Queues**:
   - `sms-received`
   - `sms-delivery-reports`
   (names are configurable via env vars — see below)
5. **Copy the Storage account connection string**: Storage account → **Access keys** → copy the **Connection string**
6. **Create two Event Grid subscriptions** on the ACS resource (**Events** blade → **+ Event Subscription**):
   - One filtered to `Microsoft.Communication.SMSReceived`, endpoint type **Storage Queues**, pointing at the `sms-received` queue
   - One filtered to `Microsoft.Communication.SMSDeliveryReportReceived`, endpoint type **Storage Queues**, pointing at the `sms-delivery-reports` queue

Equivalent Azure CLI, if you prefer:
```bash
az eventgrid event-subscription create \
  --name sms-received-to-queue \
  --source-resource-id <ACS_RESOURCE_ID> \
  --endpoint-type storagequeue \
  --endpoint "<STORAGE_ACCOUNT_RESOURCE_ID>/queueServices/default/queues/sms-received" \
  --included-event-types Microsoft.Communication.SMSReceived

az eventgrid event-subscription create \
  --name sms-delivery-reports-to-queue \
  --source-resource-id <ACS_RESOURCE_ID> \
  --endpoint-type storagequeue \
  --endpoint "<STORAGE_ACCOUNT_RESOURCE_ID>/queueServices/default/queues/sms-delivery-reports" \
  --included-event-types Microsoft.Communication.SMSDeliveryReportReceived
```

## Environment Variables

Create a `.env` file in the project root by copying `.env.example`:

```bash
cp .env.example .env
```

Then populate the `.env` file:

```
AZURE_COMMUNICATION_CONNECTION_STRING=endpoint=https://your-acs-resource.communication.azure.com/;accesskey=your_access_key_here
ACS_PHONE_NUMBER=your_acs_phone_number_here

AZURE_STORAGE_QUEUE_CONNECTION_STRING=DefaultEndpointsProtocol=https;AccountName=your_account;AccountKey=your_key;EndpointSuffix=core.windows.net
AZURE_SMS_RECEIVED_QUEUE_NAME=sms-received
AZURE_SMS_DELIVERY_REPORT_QUEUE_NAME=sms-delivery-reports

ACS_TEST_RECIPIENT_NUMBER=your_verified_personal_phone_number_here
```

`ACS_TEST_RECIPIENT_NUMBER` and `AZURE_SMS_DELIVERY_REPORT_QUEUE_NAME` are only used by the SMS delivery-confirmation test (see [Testing](#testing)) — not needed to run the main application.

**Important:** Never commit the `.env` file to version control. The `.gitignore` file already excludes it.

## Installation

1. Clone or download the project:

```bash
cd twilio-payment-link-reader
```

2. Install dependencies:

```bash
npm install
```

3. Set up environment variables (see [Environment Variables](#environment-variables) above) and complete the [Azure Setup](#azure-setup)

## Running The Project

### Development Mode (with ts-node)

Run the application directly with TypeScript compilation:

```bash
npm run dev
```

or

```bash
npm start
```

### Build and Run

Build the TypeScript to JavaScript:

```bash
npm run build
```

Run the compiled JavaScript:

```bash
node dist/index.js
```

## Example Output

When you run the application:

```
==================================================
ACS Payment Link Reader
==================================================

Have you triggered the SMS?
(yes/no): yes

Waiting for SMS...

==================================================
Payment URL Found
==================================================

https://pay.dev-acorninsure.co.uk/session?id=1786000915157-065cd3cd-68ef-4d40-9bcf-e98aa0c0e767
```

### If User Cancels:

```
==================================================
ACS Payment Link Reader
==================================================

Have you triggered the SMS?
(yes/no): no

Process cancelled.
```

### Error Scenarios:

**No SMS within timeout period:**
```
ERROR: No SMS received within timeout period
```

**Invalid payment URL in SMS:**
```
ERROR: No payment URL found in SMS body
```

**Missing environment variables:**
```
ERROR: Missing required environment variables. Please check your .env file.
```

## Project Structure

```
twilio-payment-link-reader/
│
├── src/
│   ├── acs/
│   │   ├── AcsSmsReader.ts          # Polls Storage Queue for received SMS (SMSReceived events)
│   │   ├── AcsSmsSender.ts          # Sends SMS + polls for delivery reports
│   │   ├── EventGridQueuePoller.ts  # Shared Storage Queue polling/decoding logic
│   │   └── EventGridQueueEvent.ts   # Event Grid event/data type definitions
│   │
│   ├── utils/
│   │   └── UrlExtractor.ts          # URL extraction using regex
│   │
│   └── index.ts                     # Main application entry point
│
├── features/
│   └── acs-sms-delivery-confirmation.feature   # Gherkin scenario: send SMS, confirm delivery via API
│
├── steps/
│   └── acsSms.steps.ts              # Step definitions for the feature above
│
├── support/
│   ├── world.ts                     # Typed BDD World (state shared across steps in a scenario)
│   └── env.ts                       # Loads .env via dotenv for the test run
│
├── dist/                            # Compiled JavaScript (generated by `npm run build`)
├── .features-gen/                   # Generated Playwright specs from .feature files (gitignored)
│
├── .env.example                     # Environment variables template
├── .env                             # Environment variables (not committed)
├── .gitignore                       # Git ignore rules
├── package.json                     # Project metadata and dependencies
├── playwright.config.ts             # Playwright + playwright-bdd configuration
├── tsconfig.json                    # TypeScript configuration (editor/type-checking)
├── tsconfig.build.json              # TypeScript configuration for `npm run build` (src/ only)
└── README.md                        # This file
```

## Testing

The project uses **Playwright Test + playwright-bdd**, matching the convention used elsewhere in this repo (see `TestRunnerSolution/src/UiTests`): scenarios are written in Gherkin and backed by TypeScript step definitions.

```bash
npm test
```

This runs `bddgen` (compiles `features/**/*.feature` into runnable Playwright specs under `.features-gen/`) followed by `playwright test`. Other useful scripts:
- `npm run test:debug` — same, with Playwright's debug/inspector mode
- `npm run report` — opens the last HTML test report

**Scenario (`features/acs-sms-delivery-confirmation.feature`, implemented in `steps/acsSms.steps.ts`):**
1. Generates a uniquely tagged message body
2. Sends it from `ACS_PHONE_NUMBER` to `ACS_TEST_RECIPIENT_NUMBER` via `AcsSmsSender.sendSms`
3. Polls the `sms-delivery-reports` queue via `AcsSmsSender.waitForDeliveryStatus` until the matching delivery report arrives
4. Prints the message content (as sent) and delivery status to the console
5. Asserts the content contains the unique tag and the status is `Delivered`

This requires live ACS + Azure Storage configuration (`AZURE_COMMUNICATION_CONNECTION_STRING`, `ACS_PHONE_NUMBER`, `ACS_TEST_RECIPIENT_NUMBER`, `AZURE_STORAGE_QUEUE_CONNECTION_STRING`, `AZURE_SMS_DELIVERY_REPORT_QUEUE_NAME`) in `.env`, the Event Grid subscription for delivery reports set up (see [Azure Setup](#azure-setup)), and will incur real SMS usage. If any are missing, the scenario is skipped automatically via `test.skip(...)` in the first step (useful for CI without secrets configured).

**Why delivery status instead of reading the SMS content back on the recipient side:** the recipient here is a personal phone, not an ACS-owned number, so ACS has no visibility into that phone's inbox — only into the delivery report for what it sent. `Microsoft.Communication.SMSDeliveryReportReceived` events don't echo back the message text either, so the printed content is what was sent (`sentBody`), not something independently re-fetched from ACS. Reading *received* content back via the API (as opposed to confirming delivery of what was sent) requires the recipient to also be an ACS number in the same resource with its own Event Grid subscription — `AcsSmsReader.waitForLatestSms` (used by the main application) is what does that.

## Code Quality

The project follows best practices:

- **Strong TypeScript Typing:** All types are explicitly defined; `any` type is not used
- **Async/Await:** Modern asynchronous code patterns
- **Object-Oriented Design:** Functionality organized into classes with clear responsibilities
- **Error Handling:** Comprehensive error handling with meaningful error messages
- **Documentation:** All methods include JSDoc comments explaining purpose, parameters, and return values
- **Strict TypeScript Configuration:** Enabled strict mode and related compiler options for maximum type safety

## Class Documentation

### AcsSmsReader

Polls an Azure Storage Queue for SMS messages received by the configured ACS phone number.

**Constructor:**
```typescript
constructor(storageQueueConnectionString: string, queueName: string, phoneNumber: string)
```

**Methods:**
- `waitForLatestSms(timeoutMs: number = 300000): Promise<string>`
  - Waits for the latest SMS message received on the configured phone number
  - Polls every 5 seconds until a matching `SMSReceived` event is found or timeout is reached
  - Returns the SMS message text
  - Throws an error if no SMS is received within the timeout period

### AcsSmsSender

Sends outbound SMS messages via Azure Communication Services and, optionally, polls for their delivery report.

**Constructor:**
```typescript
constructor(connectionString: string, deliveryReportQueue?: { storageQueueConnectionString: string; queueName: string })
```

**Methods:**
- `sendSms(from: string, to: string, message: string): Promise<string>`
  - Sends an SMS message via the ACS API
  - Returns the ID of the sent message
  - Throws an error if the ACS API reports the send as unsuccessful
- `waitForDeliveryStatus(messageId: string, timeoutMs: number = 60000): Promise<AcsDeliveryReport>`
  - Polls the delivery report queue for the report matching `messageId`, until it arrives or the timeout elapses
  - Returns `{ status: 'Delivered' | 'Failed', details: string }`
  - Throws an error if constructed without delivery report queue config, or if no report arrives in time

### EventGridQueuePoller

Shared polling logic used by both classes above: retrieves messages from a Storage Queue, base64-decodes and JSON-parses each as an Event Grid event, and deletes every dequeued message (matched or not) so stale events don't keep resurfacing.

**Constructor:**
```typescript
constructor(storageQueueConnectionString: string, queueName: string)
```

**Methods:**
- `waitForEvent<TData>(matches: (event: EventGridQueueEvent<TData>) => boolean, timeoutMs: number, options?: { pollIntervalMs?: number; timeoutMessage?: string }): Promise<EventGridQueueEvent<TData>>`
  - Polls until an event satisfying `matches` is found, or `timeoutMs` elapses
  - Throws an error (using `options.timeoutMessage` if provided) if no matching event arrives in time

### UrlExtractor

Provides utility methods for extracting URLs from text and HTML content.

**Methods:**
- `static extractPaymentLink(messageBody: string): string`
  - Extracts an HTTPS URL from the provided message body
  - Uses regex pattern matching to identify URLs
  - Returns the first URL found
  - Throws an error if no URL is found

## Troubleshooting

### "Missing required environment variables"
- Ensure your `.env` file exists in the project root
- Verify all required variables are set (see [Environment Variables](#environment-variables))

### "No SMS received within timeout period"
- Confirm the Event Grid subscription for `Microsoft.Communication.SMSReceived` exists and points at the queue named in `AZURE_SMS_RECEIVED_QUEUE_NAME`
- Verify the ACS phone number actually has SMS receive capability enabled
- Check the Storage Queue in the Azure Portal to see if the event is arriving at all (if it's stuck in the queue, something downstream isn't draining/matching it)
- Ensure your SMS contains an HTTPS URL

### "No payment URL found in SMS body"
- Verify the SMS contains an HTTPS URL (not HTTP)
- Check that the URL is properly formatted (starts with `https://`)
- The URL should not contain HTML tags or excessive whitespace

### Delivery report / test never arrives
- Confirm the Event Grid subscription for `Microsoft.Communication.SMSDeliveryReportReceived` exists and points at `AZURE_SMS_DELIVERY_REPORT_QUEUE_NAME`
- Confirm `ACS_TEST_RECIPIENT_NUMBER` is a real, reachable number (delivery reports only fire once the carrier responds)

### Module not found errors
- Ensure all dependencies are installed: `npm install`
- Verify your `tsconfig.json` settings
- Check that you're running the command from the project root directory

### TypeScript compilation errors
- Ensure you have TypeScript installed: `npm install --save-dev typescript`
- Run `npm run build` to compile and see detailed error messages
- Check the TypeScript version: `npx tsc --version`

## Dependencies

### Production
- **@azure/communication-sms** (^1.1.0) - Official Azure SDK for sending SMS via ACS
- **@azure/storage-queue** (^12.31.0) - Official Azure SDK for polling the Storage Queues that Event Grid delivers SMS events into
- **dotenv** (^16.3.1) - Load environment variables from .env file

### Development
- **typescript** (^5.2.2) - TypeScript compiler
- **@types/node** (^20.8.0) - Type definitions for Node.js
- **ts-node** (^10.9.1) - TypeScript execution engine for Node.js
- **@playwright/test** (^1.62.1) - Test runner, assertions (`expect`), and test execution engine
- **playwright-bdd** (^9.2.0) - Compiles Gherkin `.feature` files into Playwright specs, provides `createBdd()` for step definitions

**Known issue:** `@azure/communication-sms@1.1.0` depends on an old `uuid` package with a moderate-severity advisory ([GHSA-w5hq-g745-h8pq](https://github.com/advisories/GHSA-w5hq-g745-h8pq)). The only fix `npm audit` currently offers is force-installing `@azure/communication-sms@1.2.0-beta.4` (unstable) — not done here. Revisit once Microsoft ships a stable release with the bump.

## License

ISC

## Support

For issues related to:
- **Azure Communication Services:** Visit [https://learn.microsoft.com/en-us/azure/communication-services/](https://learn.microsoft.com/en-us/azure/communication-services/)
- **Azure Event Grid:** Visit [https://learn.microsoft.com/en-us/azure/event-grid/](https://learn.microsoft.com/en-us/azure/event-grid/)
- **TypeScript:** Visit [https://www.typescriptlang.org/docs](https://www.typescriptlang.org/docs)
- **Node.js:** Visit [https://nodejs.org/docs](https://nodejs.org/docs)
