Feature: Retry auto-discovery smoke test

  @retry:3
  Scenario: Transient failure recovers within the retry budget
    When operation fails 2 times
    Then verify attempt count is 3
