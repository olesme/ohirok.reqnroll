@block_retry @block_retry_decision
Feature: Block Retry Decision Hook

@condition:TimeoutException @force_retry_block
Scenario: Force retry overrides a non-matching condition in a block
    #retryblock:3
    When operation throws invalid <TestId_A>
    Then success
    #end
    And verify attempt count is 3 <TestId_A>
