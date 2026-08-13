Feature: Twilio SMS delivery confirmation

  As a developer integrating with Twilio,
  I want to send an SMS via the API and confirm Twilio processed it via the API,
  so that I can verify Twilio can successfully send SMS content end to end.

  Scenario: Send an SMS to a verified recipient and confirm delivery
    Given I generate a uniquely tagged test SMS message
    When I send the SMS from the Twilio number to the verified test recipient via the Twilio API
    And I wait for Twilio to confirm the delivery status via the API
    Then the message content and delivery status should be printed
    And the delivery status should be successful
