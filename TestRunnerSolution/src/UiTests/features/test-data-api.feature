@database
Feature: Consume test data through the API

  Scenario: Get available test data tables
    When I request the list of test data tables
    Then the test data tables response should contain at least one table

  Scenario: Get rows from an available table
    Given I know an available test data table
    When I request test data for that table
    Then the test data rows response should be successful

  Scenario: Get test plan through API
    Given the test data API is available
    When I request rows from table "test_plans"
    Then the response matches the test plan schema

#  @database @negative
#  Scenario: Test plan rows fail validation against an incompatible schema
#    Given the test data API is available
#    When I request rows from table "test_plans"
#    Then the response should fail the incompatible schema validation

  @@intentionalfail
  Scenario: Test plan rows visibly fail against an incompatible schema
    Given the test data API is available
    When I request rows from table "test_plans"
    Then the response incorrectly matches the incompatible schema

  Scenario: Get a single bug row through API
    Given the test data API is available
    When I request 1 row from table "bugs"
    Then the response matches the bug schema

#  @database @negative
#  Scenario: Bug row fails validation against an incompatible schema
#    Given the test data API is available
#    When I request 1 row from table "bugs"
#    Then the bug response should fail the incompatible schema validation

  @intentionalfail
  Scenario: Bug row visibly fails against an incompatible schema
    Given the test data API is available
    When I request 1 row from table "bugs"
    Then the bug response incorrectly matches the incompatible schema
