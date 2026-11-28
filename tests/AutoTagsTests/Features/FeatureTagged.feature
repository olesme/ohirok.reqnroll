@Database
Feature: A tag the feature already has

Scenario: The feature's tag is not added to its scenarios
    Given an order exists in the database
    Then the scenario is tagged Database
