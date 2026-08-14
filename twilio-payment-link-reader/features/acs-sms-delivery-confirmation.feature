Feature: ACS SMS delivery confirmation

  As a developer integrating with Azure Communication Services (ACS),
  I want to send an SMS via the API and confirm ACS processed it via the API,
  so that I can verify ACS can successfully send SMS content end to end.

  Scenario: Send an SMS to a verified recipient and confirm delivery
    Given I generate a uniquely tagged test SMS message
    When I send the SMS from the ACS number to the verified test recipient via the ACS API
    And I wait for Azure Communication Services to confirm the delivery status via the API
    Then the message content and delivery status should be printed
    And the delivery status should be successful
