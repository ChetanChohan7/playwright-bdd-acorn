import * as dotenv from 'dotenv';
import { createInterface } from 'readline/promises';
import { stdin as input, stdout as output } from 'process';
import { TwilioSmsReader } from './twilio/TwilioSmsReader';
import { UrlExtractor } from './utils/UrlExtractor';

/**
 * Main application entry point
 * Prompts user for SMS trigger confirmation, retrieves SMS from Twilio,
 * extracts payment URL, and outputs it to console
 */
async function main(): Promise<void> {
  // Load environment variables from .env file
  dotenv.config();

  // Display application header
  console.log('==================================================');
  console.log('Twilio Payment Link Reader');
  console.log('==================================================\n');

  // Create readline interface for user input
  const rl = createInterface({ input, output });

  try {
    // Prompt user for SMS trigger confirmation
    const userResponse = await rl.question(
      'Have you triggered the SMS?\n(yes/no): '
    );

    // Normalize user input
    const normalizedResponse = userResponse.toLowerCase().trim();

    // Check user response
    if (normalizedResponse === 'no' || normalizedResponse === 'n') {
      console.log('Process cancelled.');
      process.exit(0);
    }

    if (normalizedResponse !== 'yes' && normalizedResponse !== 'y') {
      console.log(
        'Invalid input. Please enter "yes" or "no" (or "y" or "n").'
      );
      process.exit(1);
    }

    // Retrieve environment variables
    const accountSid = process.env.TWILIO_ACCOUNT_SID;
    const authToken = process.env.TWILIO_AUTH_TOKEN;
    const phoneNumber = process.env.TWILIO_PHONE_NUMBER;

    // Validate environment variables
    if (!accountSid || !authToken || !phoneNumber) {
      throw new Error(
        'Missing required environment variables. Please check your .env file.'
      );
    }

    // Create Twilio SMS reader instance
    const smsReader = new TwilioSmsReader(accountSid, authToken, phoneNumber);

    // Wait for SMS (2 minute timeout)
    console.log('\nWaiting for SMS...');
    const smsBody = await smsReader.waitForLatestSms(120000);

    // Extract payment URL from SMS body
    const paymentUrl = UrlExtractor.extractPaymentLink(smsBody);

    // Display success message and payment URL
    console.log('\n==================================================');
    console.log('Payment URL Found');
    console.log('==================================================\n');
    console.log(paymentUrl);
  } catch (error) {
    // Handle and display errors
    if (error instanceof Error) {
      console.error(`\nERROR: ${error.message}`);
    } else {
      console.error('\nERROR: An unexpected error occurred');
    }
    process.exit(1);
  } finally {
    // Close readline interface
    rl.close();
  }
}

// Execute main application
main();
