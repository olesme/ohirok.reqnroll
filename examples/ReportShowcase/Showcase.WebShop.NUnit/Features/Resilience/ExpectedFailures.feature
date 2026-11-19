@resilience
Feature: Expected and allowed failures, skipped steps
  #xfail marks a step that is expected to fail (a known bug): the scenario stays green while the bug is there and
  turns red the day the step passes, so the fix is noticed. #afail allows a step to fail or pass. #ignore skips a
  step. Scenarios with steps that are undefined, ambiguous or pending show up as incomplete.

  @bug:412
  Scenario: The returning-guest discount bug is known and tracked
    Given a returning-guest customer
    And the cart contains 1 x TEA-003
    #xfail:ShouldAssertException
    Then the discount is 0.00 EUR

  @bug:421
  Scenario: The free-shipping boundary bug only fails with an assertion
    Given the customer ships to ES
    And the cart contains 2 x GFT-031
    #xfail:ShouldAssertException
    Then shipping is free

  Scenario: A known bug that was fixed in the meantime
    Given a member customer
    And the cart contains 1 x TEA-003
    #xfail
    Then the discount is 3.40 EUR

  Scenario: A best-effort live quote may fail without failing the order
    Given the customer ships to JP
    And the cart contains 1 x TEA-001
    #afail:TimeoutException|HttpRequestException
    When the carrier is asked for a live quote
    Then the subtotal is 12.50 EUR

  Scenario: A step skipped while its dependency is being replaced
    Given the cart contains 1 x CUP-011
    # The new carrier API is not connected yet: #ignore
    When the carrier is asked for a live quote
    Then the subtotal is 42.00 EUR

  @ignore
  Scenario: A scenario switched off for this release
    Given the cart contains 1 x CUP-010
    Then the subtotal is 18.00 EUR

  Scenario: A step that has no implementation yet
    Given a member customer
    And the cart contains 1 x TEA-002
    Then the loyalty points are credited to the customer account

  Scenario: A step that matches two step definitions
    Given the cart contains 1 x TEA-002
    When the cart is emptied
    Then the subtotal is 0.00 EUR

  @teardown-bug
  Scenario: Every step passes but the cleanup hook fails
    Given the cart contains 1 x ACC-021
    Then the subtotal is 6.50 EUR

  # Not an assertion failure: the domain throws, and the report shows the exception and its stack trace.
  @bug:433
  Scenario: A negative quantity is shown as a validation message
    When the customer adds 2 x TEA-001 with a negative quantity
