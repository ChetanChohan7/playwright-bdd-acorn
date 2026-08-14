/**
 * Shape of an Event Grid event as delivered into an Azure Storage Queue.
 * Azure Communication Services (ACS) has no REST endpoint to list/poll SMS
 * messages directly (unlike Twilio's Messages resource) — inbound SMS and
 * delivery reports are only published as Event Grid events, which an Event
 * Grid subscription must route somewhere. This project routes them to a
 * Storage Queue so the app can poll for them, mirroring the polling pattern
 * used against Twilio's API.
 */
export interface EventGridQueueEvent<TData> {
  id: string;
  topic: string;
  subject: string;
  data: TData;
  eventType: string;
  dataVersion: string;
  metadataVersion: string;
  eventTime: string;
}

/** Data payload of a Microsoft.Communication.SMSReceived event. */
export interface SmsReceivedEventData {
  messageId: string;
  from: string;
  to: string;
  message: string;
  receivedTimestamp: string;
  segmentCount: number;
}

/** Data payload of a Microsoft.Communication.SMSDeliveryReportReceived event. */
export interface SmsDeliveryReportEventData {
  messageId: string;
  from: string;
  to: string;
  deliveryStatus: 'Delivered' | 'Failed';
  deliveryStatusDetails: string;
  receivedTimestamp: string;
}
