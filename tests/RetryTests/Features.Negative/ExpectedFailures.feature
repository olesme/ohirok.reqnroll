@negative @sequential
Feature: Expected Failures

# Test: Should fail after exhausting max attempts
# Expected: Fails after 3 attempts with InvalidOperationException
@retry:2 @expected-failure:max_attempts_exhausted
Scenario: FAIL_Max_Attempts
    When operation always fails <TestId_A>
    Then should fail after 3 attempts <TestId_A>

# Test: Should fail due to timeout before max attempts
# Expected: Fails with TimeoutException after ~2 attempts
@retry:4 @timeout:1s @expected-failure:timeout
Scenario: FAIL_Timeout
    When operation sleeps 600ms and always fails <TestId_B>
    Then should fail with timeout <TestId_B>

# Test: Should fail on first attempt due to condition mismatch
# Expected: Fails immediately, no retry
@retry:2 @condition:ArgumentException @expected-failure:condition_mismatch
Scenario: FAIL_Condition_Mismatch
    When operation throws timeout <TestId_C>
    Then should fail on first attempt <TestId_C>

# Test: Negative condition prevents retry
# Expected: Fails on first attempt
@retry:2 @condition:!TimeoutException @expected-failure:negative_condition
Scenario: FAIL_Negative_Condition
    When operation throws timeout <TestId_D>
    Then should fail on first attempt <TestId_D>

# Test: OnRetryDecision hook forces the loop to stop despite a matching condition
# Expected: Fails after 1 attempt even though the condition would normally allow retries
@retry:2 @condition:*Exception @force_stop_test @expected-failure:force_stop
Scenario: FAIL_Force_Stop_Override
    When operation always fails <TestId_E>
    Then should fail on first attempt <TestId_E>

# Test: OnRetryDecision hook forces a retry despite a non-matching condition, but MaxAttempts still caps it
# Expected: Fails after exactly 2 attempts, not more
@retry:1 @condition:!InvalidOperationException @force_retry_test @expected-failure:force_retry_capped
Scenario: FAIL_Force_Retry_Capped_By_MaxAttempts
    When operation always fails <TestId_F>
    Then should fail after 2 attempts <TestId_F>

# Test: OnRetryDecision hook forces a step retry to stop despite a matching condition
# Expected: Fails after 1 attempt
@condition:*Exception @force_stop_step @expected-failure:force_stop_step
Scenario: FAIL_Force_Stop_Step
    #retry:2
    When operation always fails <TestId_G>
    Then should fail on first attempt <TestId_G>

# Test: OnRetryDecision hook forces a step retry despite a non-matching condition, but MaxAttempts still caps it
# Expected: Fails after exactly 2 attempts, not more
@condition:!InvalidOperationException @force_retry_step @expected-failure:force_retry_capped_step
Scenario: FAIL_Force_Retry_Capped_Step
    #retry:1
    When operation always fails <TestId_H>
    Then should fail after 2 attempts <TestId_H>

# Only the first Examples block has a retry tag
Scenario Outline: FAIL_Outline row without a retry tag is not retried <id>
    When operation fails <fails> times <TestId_<id>>

    @retry:1
    Examples: with retry
        | id | fails |
        | a  | 5     |

    Examples: without retry
        | id | fails |
        | b  | 1     |

# A digit-only tag must not become an attempt count (or document that it does)
@retry:1 @7
Scenario: FAIL_Numeric tag does not change the attempt limit
    When operation always fails <TestId_N>

# The attempt count comes from the tag: @retryblock:1 must stop after 2 attempts, not the default 3.
@retryblock:1 @expected-failure:max_attempts_exhausted
Scenario: FAIL_Scenario block honours an attempt count below the default
    When operation fails 2 times <TestId_P>
    Then verify attempt count is 3 <TestId_P>

# Every retry type fails the same way when its time budget runs out: a TimeoutException that states the
# timeout and names the last error (the last error is also its InnerException).
@expected-failure:timeout
Scenario: FAIL_Block retry stops at its timeout
    #retryblock:4 #timeout:1s
    When operation sleeps 600ms and always fails <TestId_TB>
    #end

@expected-failure:timeout
Scenario: FAIL_Step retry stops at its timeout
    #retry:4 #timeout:1s
    When operation sleeps 600ms and always fails <TestId_TS>

# A condition next to #retry applies to the step: a non-matching exception is not retried.
@expected-failure:condition_mismatch
Scenario: FAIL_Step retry condition that does not match is not retried
    #retry:2 #condition:ArgumentException
    When operation throws timeout <TestId_SC>
