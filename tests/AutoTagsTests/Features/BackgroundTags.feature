Feature: Tags from Background steps

Background:
    Given an order exists in the database

Scenario: The feature's Background tags every scenario
    Then the scenario is tagged Database
    And the scenario is not tagged MockServer

Rule: Mocked prices

    Background:
        Given the mock server returns the price "5.00"

    Scenario: A rule's Background tags the scenarios of the rule
        Then the scenario is tagged Database
        And the scenario is tagged MockServer
        And the mock server hook ran
