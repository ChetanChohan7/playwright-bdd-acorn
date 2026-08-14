import { QueueClient } from '@azure/storage-queue';
import { EventGridQueueEvent } from './EventGridQueueEvent';

/**
 * Polls an Azure Storage Queue that an Event Grid subscription delivers
 * events into, decoding and parsing each message as an Event Grid event.
 *
 * Event Grid base64-encodes event payloads before writing them to a Storage
 * Queue, so each message is decoded before being JSON-parsed.
 *
 * Every dequeued message is deleted from the queue regardless of whether it
 * matches what the caller is looking for, so stale or irrelevant events
 * don't keep resurfacing on later polls once their visibility timeout
 * expires. This assumes the queue is dedicated to this app's own polling —
 * not shared with another consumer that also needs to see those events.
 */
export class EventGridQueuePoller {
  private readonly queueClient: QueueClient;

  constructor(storageQueueConnectionString: string, queueName: string) {
    this.queueClient = new QueueClient(storageQueueConnectionString, queueName);
  }

  /**
   * Polls the queue until an event satisfying `matches` is found, or
   * `timeoutMs` elapses.
   *
   * @param matches - Predicate identifying the event being waited for
   * @param timeoutMs - Maximum time in milliseconds to wait
   * @param options.pollIntervalMs - Delay between polls (default: 5 seconds)
   * @param options.timeoutMessage - Error message thrown on timeout
   * @throws Error if no matching event arrives within the timeout period
   */
  public async waitForEvent<TData>(
    matches: (event: EventGridQueueEvent<TData>) => boolean,
    timeoutMs: number,
    options: { pollIntervalMs?: number; timeoutMessage?: string } = {}
  ): Promise<EventGridQueueEvent<TData>> {
    const pollIntervalMs = options.pollIntervalMs ?? 5000;
    const startTime = Date.now();

    while (Date.now() - startTime < timeoutMs) {
      try {
        const response = await this.queueClient.receiveMessages({ numberOfMessages: 32 });

        for (const item of response.receivedMessageItems) {
          const event = this.parseEvent<TData>(item.messageText);

          await this.queueClient.deleteMessage(item.messageId, item.popReceipt);

          if (event && matches(event)) {
            return event;
          }
        }
      } catch (error) {
        console.error('Error polling Storage Queue for Event Grid events:', error);
      }

      await this.delay(pollIntervalMs);
    }

    throw new Error(options.timeoutMessage ?? 'No matching event received within timeout period');
  }

  private parseEvent<TData>(messageText: string): EventGridQueueEvent<TData> | undefined {
    try {
      const decoded = Buffer.from(messageText, 'base64').toString('utf-8');
      const parsed: unknown = JSON.parse(decoded);
      const event = Array.isArray(parsed) ? parsed[0] : parsed;

      return event as EventGridQueueEvent<TData>;
    } catch {
      // Not a parseable/base64-encoded Event Grid event; ignore and drop it.
      return undefined;
    }
  }

  /**
   * Utility method to create a delay
   * @param ms - Milliseconds to delay
   */
  private delay(ms: number): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }
}
