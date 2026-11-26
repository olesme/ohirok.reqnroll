@outline_multiple_examples
Feature: Outline with Multiple Examples Sets

  Background:
    Given test storage is initialized

  @retry:2
  Scenario Outline: Retry wraps all rows across multiple examples sets <id>
    When operation fails <failures> times <TestId_<id>>
    Then verify attempt count is <total> <TestId_<id>>

    Examples: High-failure rows
      | id | failures | total |
      | A1 | 2        | 3     |
      | A2 | 2        | 3     |

    Examples: Low-failure rows
      | id | failures | total |
      | B1 | 1        | 2     |
      | B2 | 0        | 1     |
