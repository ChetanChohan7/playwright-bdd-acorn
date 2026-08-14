import { EventGridQueuePoller } from './EventGridQueuePoller';
import { SmsReceivedEventData } from './EventGridQueueEvent';

const SMS_RECEIVED_EVENT_TYPE = 'Microsoft.Communication.SMSReceived';

/**
 * AcsSmsReader handles retrieval of SMS messages received by an Azure
 * Communication Services (ACS) phone number.
 *
 * ACS has no REST endpoint to list/poll received messages the way Twilio
 * does — inbound SMS is only published as a Microsoft.Communication.SMSReceived
 * Event Grid event. This class polls an Azure Storage Queue that an Event
 * Grid subscription on the ACS resource has been configured to deliver those
 * events into (see README for the Event Grid subscription setup).
 */
export class AcsSmsReader {
  private readonly poller: EventGridQueuePoller;
  private readonly phoneNumber: string;

  /**
   * Constructor for AcsSmsReader
   * @param storageQueueConnectionString - Connection string for the Azure Storage account holding the queue
   * @param queueName - Name of the queue Event Grid delivers SMSReceived events into
   * @param phoneNumber - ACS phone number to monitor
   * @throws Error if any required parameter is missing
   */
  constructor(storageQueueConnectionString: string, queueName: string, phoneNumber: string) {
    if (!storageQueueConnectionString || !queueName || !phoneNumber) {
      throw new Error(
        'Missing required configuration: AZURE_STORAGE_QUEUE_CONNECTION_STRING, ' +
          'AZURE_SMS_RECEIVED_QUEUE_NAME, or ACS_PHONE_NUMBER'
      );
    }

    this.poller = new EventGridQueuePoller(storageQueueConnectionString, queueName);
    this.phoneNumber = phoneNumber;
  }

  /**
   * Waits for the latest SMS message received on the configured ACS phone
   * number. Polls the Storage Queue every 5 seconds until a matching
   * SMSReceived event is found or the timeout is reached. Only returns
   * messages received after polling started.
   *
   * @param timeoutMs - Maximum time in milliseconds to wait for an SMS (default: 5 minutes)
   * @returns Promise resolving to the SMS message text
   * @throws Error if no SMS is received within the timeout period
   */
  public async waitForLatestSms(timeoutMs: number = 300000): Promise<string> {
    const startTime = Date.now();

    const event = await this.poller.waitForEvent<SmsReceivedEventData>(
      (candidate) =>
        candidate.eventType === SMS_RECEIVED_EVENT_TYPE &&
        normalisePhoneNumber(candidate.data.to) === normalisePhoneNumber(this.phoneNumber) &&
        new Date(candidate.data.receivedTimestamp).getTime() >= startTime,
      timeoutMs,
      { timeoutMessage: 'No SMS received within timeout period' }
    );

    return event.data.message;
  }
}

/** Normalises a phone number for comparison (ACS event data omits the leading '+'). */
function normalisePhoneNumber(phoneNumber: string): string {
  return phoneNumber.startsWith('+') ? phoneNumber : `+${phoneNumber}`;
}
