Feature: SMS invite link

  As a user of the application,
  I want to trigger an SMS invite and follow the link it contains,
  so that I can verify MailSlurp SMS delivery end to end.

  @mailslurp @sms
  Scenario: Send an invite and follow the link in the SMS
    Given I am on the invite page
    When I click the button to send an SMS invite
    And I wait for the latest SMS sent to the MailSlurp phone number
    Then the SMS body should contain a link
    When I navigate to the extracted SMS link
    Then the linked page from the SMS should load successfully
