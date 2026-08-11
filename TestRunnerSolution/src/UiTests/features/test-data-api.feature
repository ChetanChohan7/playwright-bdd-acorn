@database
Feature: Consume test data through the API

  Scenario: Get available test data tables
    When I request the list of test data tables
    Then the test data tables response should contain at least one table

  Scenario: Get rows from an available table
    Given I know an available test data table
    When I request test data for that table
    Then the response status should be 200
    And the test data rows response should be successful
    And the API response should match the database

  Scenario: Get test plan rows through API
    Given the test data API is available
    When I request rows from table "test_plans"
    Then the response status should be 200
    And the response matches the test plan schema
    And the API response should match the database

# @database @negative
# Scenario: Test plan rows fail validation against an incompatible schema
#   Given the test data API is available
#   When I request rows from table "test_plans"
#   Then the response status should be 200
#   And the response should fail the incompatible schema validation

  @intentionalfail
  Scenario: Test plan rows visibly fail against an incompatible schema
    Given the test data API is available
    When I request rows from table "test_plans"
    Then the response status should be 200
    And the response incorrectly matches the incompatible schema

  Scenario: Get a single bug row through API
    Given the test data API is available
    When I request 1 rows from table "bugs"
    Then the response status should be 200
    And the response matches the bug schema
    And the API response should match the database


# @database @negative
# Scenario: Bug row fails validation against an incompatible schema
#   Given the test data API is available
#   When I request 1 row from table "bugs"
#   Then the response status should be 200
#   And the bug response should fail the incompatible schema validation

  @intentionalfail
  Scenario: Bug row visibly fails against an incompatible schema
    Given the test data API is available
    When I request 1 row from table "bugs"
    Then the response status should be 200
    And the bug response incorrectly matches the incompatible schema
