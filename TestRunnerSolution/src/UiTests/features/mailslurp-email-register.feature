Feature: Email registration link

  As a user of the application,
  I want to register with a fresh email address and follow the verification link,
  so that I can verify MailSlurp email delivery end to end.

  @mailslurp @email
  Scenario: Register a new user and follow the link in the verification email
    Given a fresh MailSlurp inbox
    And I am on the registration page
    When I register using the MailSlurp inbox email address
    And I wait for the latest email sent to the inbox
    Then the email body should contain a link
    When I navigate to the extracted email link
    Then the linked page from the email should load successfully
