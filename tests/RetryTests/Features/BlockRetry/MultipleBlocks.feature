@block_retry
Feature: Block Retry Multiple Blocks

Scenario: Two sequential blocks
    #retryblock:1
    When operation fails 1 time <TestId_A1>
    #end
    #retryblock:2
    When operation fails 2 times <TestId_A2>
    #end
    Then verify both blocks succeeded <TestId_A1> <TestId_A2>

Scenario: Three blocks with different retry counts
    #retryblock:1
    When operation fails 1 time <TestId_B1>
    #end
    And success
    #retryblock:2
    When operation fails 2 times <TestId_B2>
    #end
    #retryblock:2
    When operation fails 2 times <TestId_B3>
    #end
    Then verify all blocks succeeded <TestId_B1> <TestId_B2> <TestId_B3>

Scenario: Three blocks without end markers
    #retryblock:1
    When operation fails 1 time <TestId_C1>
    And success
    #retryblock:2
    When operation fails 2 times <TestId_C2>
    #retryblock:2
    When operation fails 2 times <TestId_C3>
    Then verify all blocks succeeded <TestId_C1> <TestId_C2> <TestId_C3>
