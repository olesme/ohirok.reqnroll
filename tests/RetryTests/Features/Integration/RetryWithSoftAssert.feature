@retry_softassert
Feature: Retry with SoftAssert

# Needs a project that references BOTH plugins.
# No step may fail "hard": the soft failure must be the only failure of attempt 1.
# Current code: RED. Expected after the fix: GREEN, "[Retry] Test succeeded on attempt 2" in the output.
@retry:3
Scenario: A scenario that only soft-fails is retried
    #softassert
    When operation fails 1 time <TestId_A>
    Then success

# Retry inner, soft outer: the step is retried first, the soft capture only sees the final result.
Scenario: Step retry runs inside the soft capture
    #retry:3
    #softassert
    When operation fails 2 times <TestId_B>
    Then verify attempt count is 3 <TestId_B>

# A soft failure recorded directly in step code (no #softassert marker): nothing throws through any
# invoker, it only becomes visible when AfterScenario flushes it.
# Current code: RED. Expected after the fix: GREEN.
@retry:3
Scenario: A soft failure recorded directly in step code is retried
    When a soft check fails 1 time <TestId_C>
    Then success
