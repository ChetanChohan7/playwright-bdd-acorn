import { SmsClient } from '@azure/communication-sms';
import { EventGridQueuePoller } from './EventGridQueuePoller';
import { SmsDeliveryReportEventData } from './EventGridQueueEvent';

const SMS_DELIVERY_REPORT_EVENT_TYPE = 'Microsoft.Communication.SMSDeliveryReportReceived';

/** Result of waiting for a message's delivery report. */
export interface AcsDeliveryReport {
  status: 'Delivered' | 'Failed';
  details: string;
}

/** Queue Event Grid delivers Microsoft.Communication.SMSDeliveryReportReceived events into. */
export interface DeliveryReportQueueConfig {
  storageQueueConnectionString: string;
  queueName: string;
}

/**
 * AcsSmsSender handles sending outbound SMS messages via Azure Communication
 * Services (ACS) and, optionally, tracking their delivery status.
 *
 * ACS has no REST endpoint to fetch a sent message's delivery status by ID
 * the way Twilio does — delivery confirmation is only published as a
 * Microsoft.Communication.SMSDeliveryReportReceived Event Grid event.
 * waitForDeliveryStatus polls an Azure Storage Queue that an Event Grid
 * subscription has been configured to deliver those events into.
 */
export class AcsSmsSender {
  private readonly smsClient: SmsClient;
  private readonly deliveryReportPoller?: EventGridQueuePoller;

  /**
   * Constructor for AcsSmsSender
   * @param connectionString - Azure Communication Services resource connection string
   * @param deliveryReportQueue - Optional Storage Queue config, required only if waitForDeliveryStatus will be used
   * @throws Error if the connection string is missing
   */
  constructor(connectionString: string, deliveryReportQueue?: DeliveryReportQueueConfig) {
    if (!connectionString) {
      throw new Error(
        'Missing required Azure Communication Services configuration: AZURE_COMMUNICATION_CONNECTION_STRING'
      );
    }

    this.smsClient = new SmsClient(connectionString);

    if (deliveryReportQueue) {
      this.deliveryReportPoller = new EventGridQueuePoller(
        deliveryReportQueue.storageQueueConnectionString,
        deliveryReportQueue.queueName
      );
    }
  }

  /**
   * Sends an SMS message via the Azure Communication Services API.
   *
   * @param from - The ACS phone number to send from
   * @param to - The destination phone number
   * @param message - The message text to send
   * @returns Promise resolving to the ID of the sent message
   * @throws Error if the ACS API reports the send as unsuccessful
   */
  public async sendSms(from: string, to: string, message: string): Promise<string> {
    const [result] = await this.smsClient.send({ from, to: [to], message });

    if (!result.successful || !result.messageId) {
      throw new Error(
        `Failed to send SMS: ${result.errorMessage ?? 'unknown error'} (HTTP ${result.httpStatusCode})`
      );
    }

    return result.messageId;
  }

  /**
   * Polls the delivery report Storage Queue for the delivery report matching
   * a previously sent message, until it arrives or the timeout elapses.
   *
   * @param messageId - The ID of the message to check, as returned by sendSms
   * @param timeoutMs - Maximum time in milliseconds to wait (default: 60 seconds)
   * @returns Promise resolving to the delivery status and details
   * @throws Error if constructed without a delivery report queue, or if no report arrives in time
   */
  public async waitForDeliveryStatus(
    messageId: string,
    timeoutMs: number = 60000
  ): Promise<AcsDeliveryReport> {
    if (!this.deliveryReportPoller) {
      throw new Error(
        'AcsSmsSender was constructed without delivery report queue configuration ' +
          '(AZURE_STORAGE_QUEUE_CONNECTION_STRING / AZURE_SMS_DELIVERY_REPORT_QUEUE_NAME)'
      );
    }

    const event = await this.deliveryReportPoller.waitForEvent<SmsDeliveryReportEventData>(
      (candidate) =>
        candidate.eventType === SMS_DELIVERY_REPORT_EVENT_TYPE && candidate.data.messageId === messageId,
      timeoutMs,
      { timeoutMessage: `Delivery report for message ${messageId} was not received within timeout period` }
    );

    return { status: event.data.deliveryStatus, details: event.data.deliveryStatusDetails };
  }
}
