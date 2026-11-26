@test_retry @test_retry_hooks
Feature: Test Retry Hooks

Background:
    Given test storage is initialized

@retry:2 @backup
Scenario: Before loop hook executes once
    When operation fails 2 times <TestId_A>
    Then test_loop_started hook fires 1 time
    And test_restarted hook fires 2 times

@retry:2 @condition:TimeoutException @backup
Scenario: Tag filtered hooks execute
    When operation throws timeout <TestId_B>
    Then timeout hook executed <TestId_B>

@retry:2 @backup @print_hook_access_count
Scenario: Hooks can access context
    When operation fails 2 times <TestId_C>
    Then verify hook saw 2 attempts <TestId_C>
    And verify hook accessed context <TestId_C>

@retry:1 @print_test_loop_ended
Scenario: After loop hook has final state
    When operation fails 1 time <TestId_D>
