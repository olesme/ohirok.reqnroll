Feature: Tags from steps

    reqnroll.json maps "the mock server returns|responds ..." to @MockServer and "... in the database" to @Database.

Scenario: A step adds its tag
    Given the mock server returns the price "12.50"
    When I add the product to the cart
    Then the scenario is tagged MockServer
    And the mock server hook ran

Scenario: A scenario without such steps is unchanged
    When I add the product to the cart
    Then the scenario is not tagged MockServer
    And the mock server hook did not run

Scenario: Patterns ignore case
    Given THE MOCK SERVER RESPONDS with an error
    Then the scenario is tagged MockServer

Scenario: Several steps add several tags
    Given the mock server returns the price "1.00"
    And an order exists in the database
    Then the scenario is tagged MockServer
    And the scenario is tagged Database

@MockServer
Scenario: A tag written on the scenario is not added again
    Given the mock server returns the price "2.00"
    Then the scenario is tagged MockServer

Scenario Outline: An outline gets the tag in every example
    Given the mock server returns the price "<price>"
    Then the scenario is tagged MockServer

Examples:
    | price |
    | 1.00  |
    | 2.00  |

@Browser:Chrome @Browser:Firefox
Scenario: Every variant gets the tag
    Given the mock server returns the price "3.00"
    Then the scenario is tagged MockServer
    And the mock server hook ran
