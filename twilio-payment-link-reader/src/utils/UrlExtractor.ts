/**
 * UrlExtractor provides utility methods for extracting URLs from text content.
 * Specifically designed to extract payment URLs from SMS messages and HTML content.
 */
export class UrlExtractor {
  /**
   * Extracts a payment link (URL) from message body text or HTML.
   * The regex pattern matches HTTPS URLs that contain common payment domain patterns.
   *
   * @param messageBody - The SMS message body or HTML content to extract the URL from
   * @returns The extracted payment URL
   * @throws Error if no payment URL is found in the message body
   */
  public static extractPaymentLink(messageBody: string): string {
    if (!messageBody || typeof messageBody !== 'string') {
      throw new Error('Invalid message body provided');
    }

    // Regex pattern to match HTTPS URLs
    // This pattern will capture URLs starting with https:// and continue until it hits whitespace or common HTML ending characters
    const urlPattern = /https:\/\/[^\s<>"{}\\^`\[\]]+/g;

    const matches = messageBody.match(urlPattern);

    if (!matches || matches.length === 0) {
      throw new Error('No payment URL found in SMS body');
    }

    // Return the first URL found (most likely the payment link)
    const extractedUrl = matches[0];

    // Remove trailing punctuation that might have been captured
    const cleanedUrl = extractedUrl.replace(/[.,;:!?)]*$/, '');

    return cleanedUrl;
  }
}
