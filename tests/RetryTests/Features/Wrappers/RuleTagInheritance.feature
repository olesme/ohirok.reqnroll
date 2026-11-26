@rule_tag_inheritance
Feature: Rule Tag Inheritance for Interceptor Wrapping

  @retry:2
  Rule: Retry tag on rule is inherited by enclosed scenarios

    Background:
      Given test storage is initialized

    Scenario: Plain scenario inherits retry from rule
      When operation fails 2 times <TestId_A>
      Then verify attempt count is 3 <TestId_A>

    Scenario Outline: Outline rows inherit retry from rule <id>
      When operation fails <failures> times <TestId_<id>>
      Then verify attempt count is <total> <TestId_<id>>

      Examples: All succeed within quota
        | id | failures | total |
        | B  | 2        | 3     |
        | C  | 1        | 2     |
        | D  | 0        | 1     |
