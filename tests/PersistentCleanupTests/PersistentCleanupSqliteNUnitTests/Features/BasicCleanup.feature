Feature: Basic Cleanup Operations

Scenario: Register and execute simple cleanup
    Given I have a test resource "User123"
    When I register cleanup for "User123"
    And I perform all cleanups
    Then cleanup should have executed for "User123"
    And cleanup status should be "Completed"

Scenario: Multiple cleanups execute in order
    Given I have a test resource "User1"
    And I have a test resource "User2"
    And I have a test resource "User3"
    When I register cleanup for "User1" with order 10
    And I register cleanup for "User2" with order 5
    And I register cleanup for "User3" with order 15
    And I perform all cleanups
    Then cleanups should execute in order "User2,User1,User3"

Scenario: Cleanup with description
    Given I have a test resource "Admin456"
    When I register cleanup for "Admin456" with description "Remove admin user"
    And I perform all cleanups
    Then cleanup should have executed for "Admin456"

Scenario: Failed cleanup retries
    Given I have a failing resource "BadUser"
    When I register cleanup for "BadUser" with max retries 3
    And I perform all cleanups
    Then cleanup should have been attempted 3 times
    And cleanup status should be "Failed"

Scenario: Get cleanup statistics
    Given I have a test resource "User1"
    And I have a test resource "User2"
    And I have a failing resource "BadUser"
    When I register cleanup for "User1"
    And I register cleanup for "User2"
    When I register cleanup for "BadUser" with max retries 3
    Then statistics should show 3 pending cleanups
    When I perform all cleanups
    Then statistics should show 0 pending cleanups
    And statistics should show 1 failed cleanup
