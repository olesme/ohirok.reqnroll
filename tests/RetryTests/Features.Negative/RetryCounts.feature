@negative @sequential @retry:2
Feature: Retry counts and switching retry off

# The feature allows 2 retries; each scenario below narrows that, and must fail after exactly the runs it says.

# @noretry on the scenario wins over the feature's @retry: one run, no retry.
@noretry @expected-failure:noretry
Scenario: FAIL_NoRetry_Overrides_Feature
    When operation always fails <TestId_A>
    Then should fail on first attempt <TestId_A>

# @retry:0 means zero retries: one run.
@retry:0 @expected-failure:retry_zero
Scenario: FAIL_Retry_Zero
    When operation always fails <TestId_B>
    Then should fail on first attempt <TestId_B>

# @attempts counts runs in all, the first one included: 2 runs.
@attempts:2 @expected-failure:attempts_two
Scenario: FAIL_Attempts_Two
    When operation always fails <TestId_C>
    Then should fail after 2 attempts <TestId_C>
