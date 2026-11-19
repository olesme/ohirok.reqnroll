@outline_multiple_examples
Feature: Outline with Multiple Examples Sets - exhausted retries

  Background:
    Given test storage is initialized

  @retry:3 @expected-failure:max_attempts_exhausted
  Scenario Outline: FAIL_Both wrappers apply to all rows across multiple examples sets <id>
    When operation always fails <TestId_<id>>
    Then should fail after 3 attempts <TestId_<id>>

    Examples: First exhaustion batch
      | id |
      | C1 |
      | C2 |

    Examples: Second exhaustion batch
      | id |
      | D1 |
      | D2 |
