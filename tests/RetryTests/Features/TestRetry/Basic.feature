@test_retry
Feature: Test Retry Basic

Background:
    Given test storage is initialized

@retry:2
Scenario: Simple retry succeeds
    When operation fails 1 time <TestId_A>
    Then verify attempt count is 2 <TestId_A>

@retry:3
Scenario: Multiple failures then success
    When operation fails 2 times <TestId_B>
    Then verify attempt count is 3 <TestId_B>

@retry:3 @timeout:5s
Scenario: With timeout parameter
    When operation sleeps 100ms and fails 2 times <TestId_C>
    Then verify attempt count is 3 <TestId_C>

@retry:3 @delay:200ms
Scenario: With delay between attempts
    When operation fails 2 times <TestId_D>
    Then verify attempt count is 3 <TestId_D>
    And verify delay was at least 400ms <TestId_D>

Scenario: No retry without tag
    When operation fails 0 times <TestId_E>
    Then verify attempt count is 1 <TestId_E>
