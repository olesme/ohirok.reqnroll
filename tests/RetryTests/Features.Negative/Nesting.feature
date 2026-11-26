@negative @sequential
Feature: Retries do not nest

# While a retry is active, step and block retry markers inside it are ignored: the outermost one wins.
# The inner marker alone would absorb both failures; the outer block allows only 2 attempts, so the
# scenario must fail on attempt 2. If an inner marker ever started taking over, this would turn green.

@retryblock:1 @expected-failure:outer_retry_wins
Scenario: FAIL_Step retry inside a block retry is ignored
    #retry:2
    When operation fails 2 times <TestId_A>
    Then verify attempt count is 3 <TestId_A>

@retryblock:1 @expected-failure:outer_retry_wins
Scenario: FAIL_Block retry inside a block retry is ignored
    #retryblock:2
    When operation fails 2 times <TestId_B>
    #end
    Then verify attempt count is 3 <TestId_B>
