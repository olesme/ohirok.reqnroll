@parallel
Feature: Parallel Cleanup Operations

# These tests run in parallel against the same database
# Using scenario-specific identifiers for isolation

@parallel
Scenario: Parallel cleanup execution - Scenario A
    Given I have a unique test identifier
    And I have a test resource "UserA1"
    And I have a test resource "UserA2"
    When I register cleanup for "UserA1"
    And I register cleanup for "UserA2"
    And I perform all cleanups
    Then cleanup should have executed for "UserA1"
    And cleanup should have executed for "UserA2"
    And only my scenario's cleanups should be processed

@parallel
Scenario: Parallel cleanup execution - Scenario B
    Given I have a unique test identifier
    And I have a test resource "UserB1"
    And I have a test resource "UserB2"
    And I have a test resource "UserB3"
    When I register cleanup for "UserB1" with order 5
    And I register cleanup for "UserB2" with order 10
    And I register cleanup for "UserB3" with order 1
    And I perform all cleanups
    Then cleanups should execute in order "UserB3,UserB1,UserB2"
    And only my scenario's cleanups should be processed

@parallel
Scenario: Parallel cleanup with retries - Scenario C
    Given I have a unique test identifier
    And I have a failing resource "UserC1"
    When I register cleanup for "UserC1" with max retries 2
    And I perform all cleanups
    Then cleanup should have been attempted 2 times
    And only my scenario's cleanups should be processed

@parallel
Scenario: Parallel cleanup statistics - Scenario D
    Given I have a unique test identifier
    And I have a test resource "UserD1"
    And I have a test resource "UserD2"
    When I register cleanup for "UserD1"
    And I register cleanup for "UserD2"
    Then my scenario should show 2 pending cleanups
    When I perform all cleanups
    Then my scenario should show 0 pending cleanups

@parallel
Scenario: Concurrent cleanup registration - Scenario E
    Given I have a unique test identifier
    When I register 10 cleanups concurrently
    Then all 10 cleanups should be saved
    When I perform all cleanups
    Then all 10 cleanups should have executed

@parallel
Scenario: Parallel cleanup with mixed outcomes - Scenario F
    Given I have a unique test identifier
    And I have a test resource "UserF1"
    And I have a test resource "UserF2"
    And I have a failing resource "UserF3"
    When I register cleanup for "UserF1"
    And I register cleanup for "UserF2"
    And I register cleanup for "UserF3" with max retries 1
    And I perform all cleanups
    Then cleanup should have executed for "UserF1"
    And cleanup should have executed for "UserF2"
    And my scenario should show 1 failed cleanup
