@test_retry @test_retry_decision
Feature: Test Retry Decision Hook

Background:
    Given test storage is initialized

@retry:3 @condition:TimeoutException @force_retry_test
Scenario: Force retry overrides a non-matching condition
    When operation throws invalid <TestId_A>
    Then verify attempt count is 3 <TestId_A>

@retry:3 @backup
Scenario: Decision hook fires once per failed attempt
    When operation fails 2 times <TestId_B>
    Then test_decision_fired hook fires 2 times
