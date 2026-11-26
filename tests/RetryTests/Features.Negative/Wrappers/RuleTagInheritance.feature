@rule_tag_inheritance
Feature: Rule Tag Inheritance - exhausted retries

  @retry:1 @expected-failure:max_attempts_exhausted
  Rule: Combined retry and xfail tags on rule produce both wrappers

    Background:
      Given test storage is initialized

    Scenario: FAIL_Plain scenario inherits both wrappers from rule
      When operation always fails <TestId_E>
      Then should fail after 2 attempts <TestId_E>

    Scenario Outline: FAIL_Outline rows inherit both wrappers from rule <id>
      When operation always fails <TestId_<id>>
      Then should fail after 2 attempts <TestId_<id>>

      Examples: Always-fail rows batch 1
        | id |
        | F  |
        | G  |

      Examples: Always-fail rows batch 2
        | id |
        | H  |
        | I  |
